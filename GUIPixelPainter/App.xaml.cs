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
        private RoutedEventHandler onBotStartup;

        public App() : this(true, null) { }

        public App(bool checkLicense, RoutedEventHandler onStartup) : base()
        {
            this.checkLicense = checkLicense;
            this.onBotStartup = onStartup;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
            if (onBotStartup != null)
                botWindow.Loaded += onBotStartup;
            botWindow.Closed += (a, b) => this.Shutdown();
            botWindow.Show();
        }
    }
}
