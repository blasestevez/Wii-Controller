#if ANDROID
using Android.Content;
using Android.Net.Wifi;
#endif

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using wiimotedsu.core;

namespace wiimotedsu.app
{
    public partial class MainPage : ContentPage
    {
        private bool _isServerRunning = false;
        private uint _packetNumber = 0;
        private readonly uint _serverId = (uint)Random.Shared.Next(1, int.MaxValue);
        private readonly byte[] _macAddress = GetOrCreatePersistentMacAddress();
        private CancellationTokenSource? _udpCts;
        private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private float _accX, _accY, _accZ, _gyroP, _gyroY, _gyroR;
        private volatile byte _buttons1 = 0;
        private volatile byte _buttons2 = 0;
        private volatile byte _homeButton = 0;
        private volatile byte _touchButton = 0;
        private byte _cachedBatteryStatus = 0x05;
        private readonly ConcurrentDictionary<System.Net.IPEndPoint, DateTime> _subscribers = new();

        // === STATE & COMMUNICATION ===
        public MainPage()
        {
            InitializeComponent();

            IpLabel.Text = $"IP Address: {GetLocalIPAddress()}";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _cachedBatteryStatus = GetBatteryStatus();
            StartServer();

            if (Window != null)
            {
                Window.Deactivated -= Window_Deactivated;
                Window.Deactivated += Window_Deactivated;
                Window.Activated -= Window_Activated;
                Window.Activated += Window_Activated;
                Window.Stopped -= Window_Stopped;
                Window.Stopped += Window_Stopped;
                Window.Resumed -= Window_Resumed;
                Window.Resumed += Window_Resumed;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopServer();
        }

        private void Window_Deactivated(object? sender, EventArgs e) => StopServer();
        private void Window_Activated(object? sender, EventArgs e) => StartServer();
        private void Window_Stopped(object? sender, EventArgs e) => StopServer();
        private void Window_Resumed(object? sender, EventArgs e) => StartServer();

        private void StartServer()
        {
            if (_isServerRunning) return;
            _isServerRunning = true;

            try
            {
                DeviceDisplay.Current.KeepScreenOn = true;
            }
            catch { }

            try
            {
                Battery.Default.BatteryInfoChanged += Battery_BatteryInfoChanged;
                _cachedBatteryStatus = GetBatteryStatus();
            }
            catch { }

            StartSensors();
            _stopwatch.Restart();

            _udpCts = new CancellationTokenSource();
            Task.Run(() => StartUdpServer(_udpCts.Token));
        }

        private void StopServer()
        {
            if (!_isServerRunning) return;
            _isServerRunning = false;

            try
            {
                DeviceDisplay.Current.KeepScreenOn = false;
            }
            catch { }

            try
            {
                Battery.Default.BatteryInfoChanged -= Battery_BatteryInfoChanged;
            }
            catch { }

            StopSensors();
            _stopwatch.Stop();
            _udpCts?.Cancel();
            _subscribers.Clear();
        }

        private void Battery_BatteryInfoChanged(object? sender, BatteryInfoChangedEventArgs e)
        {
            _cachedBatteryStatus = GetBatteryStatus();
        }

        private async Task StartUdpServer(CancellationToken token)
        {
            using var udpClient = new UdpClient(26760);
            _ = Task.Run(() => BroadcastLoop(udpClient, token));

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receivedResult = await udpClient.ReceiveAsync(token);
                    var messageType = BinaryPrimitives.ReadUInt32LittleEndian(receivedResult.Buffer.AsSpan(16, 4));

                    if (messageType == 0x100000)
                    {
                        byte[] responseBuffer = new byte[24];
                        DSUPacketBuilder.WriteProtocolVersionResponse(responseBuffer, _serverId);
                        await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
                    }
                    else if (messageType == 0x100001)
                    {
                        byte[] responseBuffer = new byte[32];
                        DSUPacketBuilder.WritePortsInfoResponse(responseBuffer, 0, _macAddress, _cachedBatteryStatus, _serverId);
                        await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
                    }
                    else if (messageType == 0x100002)
                    {
                        _subscribers[receivedResult.RemoteEndPoint] = DateTime.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
            catch (Exception) { }
        }

        private async Task BroadcastLoop(UdpClient udpClient, CancellationToken token)
        {
            byte[] responseBuffer = new byte[100];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Expire subscribers older than 5 seconds (standard Cemuhook lease timeout)
                    var now = DateTime.UtcNow;
                    foreach (var kvp in _subscribers)
                    {
                        if ((now - kvp.Value).TotalSeconds > 5.0)
                        {
                            _subscribers.TryRemove(kvp.Key, out _);
                        }
                    }

                    var activeClients = _subscribers.Keys.ToArray();

                    if (activeClients.Length > 0)
                    {
                        _packetNumber++;
                        ulong timestamp = (ulong)(_stopwatch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency);

                        DSUPacketBuilder.WriteControllerDataResponse(
                            responseBuffer,
                            0,
                            _packetNumber,
                            timestamp,
                            _accX, _accY, _accZ,
                            _gyroP, _gyroY, _gyroR,
                            _buttons1, _buttons2, _homeButton, _touchButton,
                            _macAddress, _cachedBatteryStatus, _serverId);

                        foreach (var client in activeClients)
                        {
                            try
                            {
                                await udpClient.SendAsync(responseBuffer, responseBuffer.Length, client);
                            }
                            catch { }
                        }
                    }

                    await Task.Delay(16, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        private string GetLocalIPAddress()
        {
#if ANDROID
        try 
        {
            var context = Android.App.Application.Context;

            var wifiManager = (WifiManager)context.GetSystemService(Context.WifiService);

            int ipInt = wifiManager.ConnectionInfo.IpAddress;

            if (ipInt != 0)
            {
                var ipBytes = BitConverter.GetBytes(ipInt);
                var ipAddress = new System.Net.IPAddress(ipBytes);
                return ipAddress.ToString();
            }
        }
        catch
        {

        }
#endif
            return "127.0.0.1";
        }

        private static byte[] GetOrCreatePersistentMacAddress()
        {
            string savedMac = Preferences.Default.Get("device_mac", string.Empty);
            if (!string.IsNullOrEmpty(savedMac) && savedMac.Length == 12)
            {
                try
                {
                    return Convert.FromHexString(savedMac);
                }
                catch { }
            }

            byte[] newMac = new byte[6];
            Random.Shared.NextBytes(newMac);
            newMac[0] &= 0xFE; // Unicast

            Preferences.Default.Set("device_mac", Convert.ToHexString(newMac));
            return newMac;
        }

        private static byte GetBatteryStatus()
        {
            try
            {
                if (Battery.Default.State == BatteryState.Charging)
                    return 0xEE; // Charging
                if (Battery.Default.State == BatteryState.Full)
                    return 0xEF; // Charged

                double level = Battery.Default.ChargeLevel;
                if (level > 0.9) return 0x05; // Full
                if (level > 0.6) return 0x04; // High
                if (level > 0.3) return 0x03; // Medium
                if (level > 0.1) return 0x02; // Low
                if (level >= 0.0) return 0x01; // Dying
            }
            catch { }

            return 0x00; // Not applicable
        }

        private void StartSensors()
        {
            if (Accelerometer.Default.IsSupported)
            {
                if (!Accelerometer.Default.IsMonitoring) 
                {
                    Accelerometer.Default.ReadingChanged += Accelerometer_ReadingChanged;
                    Accelerometer.Default.Start(SensorSpeed.Game);
                }
            }

            if (Gyroscope.Default.IsSupported)
            {
                if (!Gyroscope.Default.IsMonitoring)
                {
                    Gyroscope.Default.ReadingChanged += Gyroscope_ReadingChanged;
                    Gyroscope.Default.Start(SensorSpeed.Game);
                }
            }
        }

        private void StopSensors()
        {
            if (Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.Default.ReadingChanged -= Accelerometer_ReadingChanged;
                Accelerometer.Default.Stop();
            }
            if (Gyroscope.Default.IsMonitoring)
            {
                Gyroscope.Default.ReadingChanged -= Gyroscope_ReadingChanged;
                Gyroscope.Default.Stop();
            }
        }

        private void Accelerometer_ReadingChanged(object? sender, AccelerometerChangedEventArgs e)
        {
            // Accelerometer Y and Z axes are swapped to match the expected orientation of the DSU protocol. The values are also negated to match the expected direction.
            _accX = -(float)(e.Reading.Acceleration.X);
            _accY = -(float)(e.Reading.Acceleration.Z);
            _accZ = (float)(e.Reading.Acceleration.Y);
        }

        private void Gyroscope_ReadingChanged(object? sender, GyroscopeChangedEventArgs e)
        {
            // DSU expects gyro data in degrees per second, but the Gyroscope sensor provides it in radians per second so we need to convert it
            // Gyroscope Y and Z axes are swapped to match the expected orientation of the DSU protocol. The value of Z is also negated to match the expected direction.
            _gyroP = (float)(e.Reading.AngularVelocity.X * (180.0 / Math.PI));
            _gyroY = -(float)(e.Reading.AngularVelocity.Z * (180.0 / Math.PI));
            _gyroR = (float)(e.Reading.AngularVelocity.Y * (180.0 / Math.PI));
        }

        private void TriggerHaptic()
        {
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch
            {
                // Fallback / ignore on unsupported platforms
            }
        }

        // Utility Handlers
        private void OnBtnRecenterPressed(object? sender, EventArgs e) { TriggerHaptic(); _touchButton = 1; }
        private void OnBtnRecenterReleased(object? sender, EventArgs e) { _touchButton = 0; }

        // D-Pad Handlers (Byte 16: Up=0x10, Down=0x40, Left=0x80, Right=0x20)
        private void OnDpadUpPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x10; }
        private void OnDpadUpReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x10); }

        private void OnDpadDownPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x40; }
        private void OnDpadDownReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x40); }

        private void OnDpadLeftPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x80; }
        private void OnDpadLeftReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x80); }

        private void OnDpadRightPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x20; }
        private void OnDpadRightReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x20); }

        // Action Buttons Handlers (Byte 17: A=0x40 (Cross), B=0x80 (Square), 1=0x10 (Triangle), 2=0x20 (Circle))
        private void OnBtnAPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons2 |= 0x40; }
        private void OnBtnAReleased(object? sender, EventArgs e) { _buttons2 &= unchecked((byte)~0x40); }

        private void OnBtnBPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons2 |= 0x80; }
        private void OnBtnBReleased(object? sender, EventArgs e) { _buttons2 &= unchecked((byte)~0x80); }

        private void OnBtn1Pressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons2 |= 0x10; }
        private void OnBtn1Released(object? sender, EventArgs e) { _buttons2 &= unchecked((byte)~0x10); }

        private void OnBtn2Pressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons2 |= 0x20; }
        private void OnBtn2Released(object? sender, EventArgs e) { _buttons2 &= unchecked((byte)~0x20); }

        // Navigation Buttons Handlers (+ = 0x08, - = 0x01 on Byte 16; Home = 0x01 on Byte 18)
        private void OnBtnPlusPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x08; }
        private void OnBtnPlusReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x08); }

        private void OnBtnMinusPressed(object? sender, EventArgs e) { TriggerHaptic(); _buttons1 |= 0x01; }
        private void OnBtnMinusReleased(object? sender, EventArgs e) { _buttons1 &= unchecked((byte)~0x01); }

        private void OnBtnHomePressed(object? sender, EventArgs e) { TriggerHaptic(); _homeButton = 1; }
        private void OnBtnHomeReleased(object? sender, EventArgs e) { _homeButton = 0; }
    }
}
