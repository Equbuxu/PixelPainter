using System;
using System.Windows;

namespace GUIPixelPainter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool checkLicense = false;
        private RoutedEventHandler onStartup;

        public App(bool checkLicense, RoutedEventHandler onStartup) : base()
        {
            this.checkLicense = checkLicense;
            this.onStartup = onStartup;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (checkLicense)
            {
                GUI.LicenseWindow licenseCheck = new GUI.LicenseWindow();
                licenseCheck.ShowDialog();
                if (!licenseCheck.success)
                {
                    this.Shutdown();
                    return;
                }
            }
            GUI.BotWindow botWindow = new GUI.BotWindow();
            botWindow.Loaded += onStartup;
        }
    }
}
