#if ANDROID
using Android.Content;
using Android.Net.Wifi;
#endif

namespace wiimotedsu.app
{
    public partial class MainPage : ContentPage
    {
        private bool _isServerRunning = false;

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
                //todo: Start the server logic here
            }
            else
            {
                ServerBtn.Text = "Start Server";
                //todo: Stop the server logic here
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
    }
}
