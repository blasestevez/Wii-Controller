#if ANDROID
#endif
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using wiimotedsu.core;

namespace wiimotedsu.app
{
    public partial class MainPage : ContentPage
    {
        private const byte BatteryStatusFull = 0x05;
        private const float RadToDeg = (float)(180.0 / Math.PI);
        private static readonly byte[] _macAddress = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

        private bool _isServerRunning = false;
        private uint _packetNumber = 0;
        private readonly uint _serverId = (uint)Random.Shared.Next(1, int.MaxValue);
        private CancellationTokenSource? _udpCts;
        private readonly System.Diagnostics.Stopwatch _stopwatch = new();

        private float _accX, _accY, _accZ, _gyroP, _gyroY, _gyroR;
        private volatile byte _buttons1 = 0;
        private volatile byte _buttons2 = 0;
        private volatile byte _homeButton = 0;
        private volatile byte _touchButton = 0;
        private readonly ConcurrentDictionary<System.Net.IPEndPoint, DateTime> _subscribers = new();

        public MainPage()
        {
            InitializeComponent();
            IpLabel.Text = $"IP Address: {GetLocalIPAddress()}";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartServer();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopServer();
        }

        private void StartServer()
        {
            if (_isServerRunning) return;
            _isServerRunning = true;

            try { DeviceDisplay.Current.KeepScreenOn = true; } catch { }

            StartSensors();
            _stopwatch.Restart();

            _udpCts = new CancellationTokenSource();
            Task.Run(() => StartUdpServer(_udpCts.Token));
        }

        private void StopServer()
        {
            if (!_isServerRunning) return;
            _isServerRunning = false;

            try { DeviceDisplay.Current.KeepScreenOn = false; } catch { }

            StopSensors();
            _stopwatch.Stop();
            _udpCts?.Cancel();
            _subscribers.Clear();
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
                    if (receivedResult.Buffer.Length < 20) continue;

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
                        DSUPacketBuilder.WritePortsInfoResponse(responseBuffer, 0, _macAddress, BatteryStatusFull, _serverId);
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
                            _macAddress, BatteryStatusFull, _serverId);

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

        private static string GetLocalIPAddress()
        {
            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !System.Net.IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void StartSensors()
        {
            if (Accelerometer.Default.IsSupported && !Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.Default.ReadingChanged += Accelerometer_ReadingChanged;
                Accelerometer.Default.Start(SensorSpeed.Game);
            }

            if (Gyroscope.Default.IsSupported && !Gyroscope.Default.IsMonitoring)
            {
                Gyroscope.Default.ReadingChanged += Gyroscope_ReadingChanged;
                Gyroscope.Default.Start(SensorSpeed.Game);
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
            _accX = -(float)e.Reading.Acceleration.X;
            _accY = -(float)e.Reading.Acceleration.Z;
            _accZ = (float)e.Reading.Acceleration.Y;
        }

        private void Gyroscope_ReadingChanged(object? sender, GyroscopeChangedEventArgs e)
        {
            _gyroP = (float)e.Reading.AngularVelocity.X * RadToDeg;
            _gyroY = -(float)e.Reading.AngularVelocity.Z * RadToDeg;
            _gyroR = (float)e.Reading.AngularVelocity.Y * RadToDeg;
        }

        private static void TriggerHaptic()
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }

        // Unified Button Event Handlers
        private void OnButtonPressed(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string key)
            {
                TriggerHaptic();
                SetButtonState(key, true);
            }
        }

        private void OnButtonReleased(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string key)
            {
                SetButtonState(key, false);
            }
        }

        private void SetButtonState(string key, bool isPressed)
        {
            switch (key)
            {
                case "Up":       UpdateButtons1(0x10, isPressed); break;
                case "Down":     UpdateButtons1(0x40, isPressed); break;
                case "Left":     UpdateButtons1(0x80, isPressed); break;
                case "Right":    UpdateButtons1(0x20, isPressed); break;
                case "Plus":     UpdateButtons1(0x08, isPressed); break;
                case "Minus":    UpdateButtons1(0x01, isPressed); break;
                case "A":        UpdateButtons2(0x40, isPressed); break;
                case "B":        UpdateButtons2(0x80, isPressed); break;
                case "1":        UpdateButtons2(0x10, isPressed); break;
                case "2":        UpdateButtons2(0x20, isPressed); break;
                case "Home":     _homeButton = (byte)(isPressed ? 1 : 0); break;
                case "Recenter": _touchButton = (byte)(isPressed ? 1 : 0); break;
            }
        }

        private void UpdateButtons1(byte mask, bool set)
        {
            if (set) _buttons1 |= mask;
            else _buttons1 &= unchecked((byte)~mask);
        }

        private void UpdateButtons2(byte mask, bool set)
        {
            if (set) _buttons2 |= mask;
            else _buttons2 &= unchecked((byte)~mask);
        }
    }
}
