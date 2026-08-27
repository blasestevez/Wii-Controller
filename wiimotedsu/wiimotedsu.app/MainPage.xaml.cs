#if ANDROID
using Android.Content;
using Android.Net.Wifi;
#endif

using System.Buffers.Binary;
using System.Net.Sockets;
using wiimotedsu.core;

namespace wiimotedsu.app
{
    public partial class MainPage : ContentPage
    {
        private bool _isServerRunning = false;
        private uint _packetNumber = 0;
        private CancellationTokenSource _udpCts;
        private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private float _accX, _accY, _accZ, _gyroP, _gyroY, _gyroR;
        private System.Net.IPEndPoint? _connectedClient = null;

        public MainPage()
        {
            InitializeComponent();

            IpLabel.Text = $"IP Address: {GetLocalIPAddress()}";
        }

        private void OnServerBtnClicked(object sender, EventArgs e)
        {
            _isServerRunning = !_isServerRunning;

            if (_isServerRunning)
            { 
                ServerBtn.Text = "Stop Server";
                StartSensors();
                _stopwatch.Restart();

                _udpCts = new CancellationTokenSource();
                Task.Run(() => StartUdpServer(_udpCts.Token));
            }
            else
            {
                ServerBtn.Text = "Start Server";
                StopSensors();
                _stopwatch.Stop();

                _udpCts.Cancel();
            }
        }

        private async Task StartUdpServer(CancellationToken token)
        {
            using var udpClient = new UdpClient(26760);

            _ = Task.Run(() => BroadcastLoop(udpClient, token));
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receivedResult = await udpClient.ReceiveAsync();
                    var messageType = BinaryPrimitives.ReadUInt32LittleEndian(receivedResult.Buffer.AsSpan(16, 4));
                    _connectedClient = receivedResult.RemoteEndPoint;

                    if (messageType == 0x100000)
                    {
                        System.Diagnostics.Debug.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100000");
                        byte[] responseBuffer = new byte[24];
                        DSUPacketBuilder.WriteProtocolVersionResponse(responseBuffer);
                        await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
                    }
                    else if (messageType == 0x100001)
                    {
                        System.Diagnostics.Debug.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100001");
                        byte[] responseBuffer = new byte[32];
                        DSUPacketBuilder.WritePortsInfoResponse(responseBuffer, 0);
                        await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
                    }
                    else if (messageType == 0x100002)
                    {
                        System.Diagnostics.Debug.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100002");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        }

        private async Task BroadcastLoop(UdpClient udpClient, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_connectedClient != null)
                    {
                        byte[] responseBuffer = new byte[100];
                        _packetNumber++;
                        ulong timestamp = (ulong)(_stopwatch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency);
                        DSUPacketBuilder.WriteControllerDataResponse(responseBuffer, 0, _packetNumber, timestamp, _accX, _accY, _accZ, _gyroP, _gyroY, _gyroR);
                        await udpClient.SendAsync(responseBuffer, responseBuffer.Length, _connectedClient);
                    }
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BroadcastLoop exception: " + ex.Message);
            }
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

        private void StartSensors()
        {
            if (Accelerometer.Default.IsSupported)
            {
                if(!Accelerometer.Default.IsMonitoring) 
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
    }
}
