using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for Browser.xaml
    /// </summary>
    public partial class Browser : Window
    {
        public string AuthKey { get; set; }
        public string AuthToken { get; set; }
        public string PHPSESSID { get; set; }
        public bool Success { get; set; }

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InternetSetOption(int hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InternetGetCookie(string lpszUrl, string lpszCookieName, StringBuilder lpszCookieData, ref int lpdwSize);

        //from https://stackoverflow.com/questions/43231921/how-can-i-clear-cookies-in-the-wpf-webbrowser-for-a-specific-site/43232360
        private static unsafe void SuppressWininetBehavior()
        {
            int option = 3/* INTERNET_SUPPRESS_COOKIE_PERSIST*/;
            int* optionPtr = &option;

            InternetSetOption(0, 81/*INTERNET_OPTION_SUPPRESS_BEHAVIOR*/, new IntPtr(optionPtr), sizeof(int));
        }

        //from https://social.msdn.microsoft.com/Forums/en-US/36995f7b-5eed-4d4f-9ead-12fad1ed40f7/how-to-get-cookie-from-wpfs-webbrowser-control?forum=wpf
        private string getCookie(string url, string cookieName)
        {
            int size = 0;
            InternetGetCookie(url, cookieName, null, ref size);

            StringBuilder cookie = new StringBuilder(size);
            InternetGetCookie(url, cookieName, cookie, ref size);

            return cookie.ToString();
        }

        //from https://stackoverflow.com/questions/1298255/how-do-i-suppress-script-errors-when-using-the-wpf-webbrowser-control
        private void HideScriptErrors(WebBrowser wb, bool hide)
        {
            var fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiComWebBrowser == null) return;
            var objComWebBrowser = fiComWebBrowser.GetValue(wb);
            if (objComWebBrowser == null)
            {
                wb.Loaded += (o, s) => HideScriptErrors(wb, hide); //In case we are to early
                return;
            }
            objComWebBrowser.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, objComWebBrowser, new object[] { hide });
        }

        public Browser()
        {
            InitializeComponent();
            SetupBrowser();
            Closed += OnClose;
        }

        private void OnClose(object sender, EventArgs e)
        {
            string authKey = getCookie("https://pixelplace.io", "authKey");
            string authToken = getCookie("https://pixelplace.io", "authToken");
            string phpSessId = getCookie("https://pixelplace.io", "PHPSESSID");
            if (string.IsNullOrEmpty(authKey) || string.IsNullOrEmpty(authKey) || string.IsNullOrEmpty(phpSessId))
            {
                Success = false;
                return;
            }

            AuthKey = authKey.Split('=').Last();
            AuthToken = authToken.Split('=').Last();
            PHPSESSID = phpSessId.Split('=').Last();
            Success = true;
        }

        private void SetupBrowser()
        {
            SuppressWininetBehavior();
            HideScriptErrors(wb, true);
            wb.Navigate("https://pixelplace.io");
        }
    }
}
