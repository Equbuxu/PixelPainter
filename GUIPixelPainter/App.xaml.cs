using System;
using System.Collections.Generic;
using System.Windows;

namespace GUIPixelPainter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool checkLicense = false;
        private Dictionary<ArgsParser.ArgName, Dictionary<string, object>> args;
        private Action<Dictionary<ArgsParser.ArgName, Dictionary<string, object>>> onBotStartup;

        public App() : this(true, null, null) { }

        public App(bool checkLicense, Dictionary<ArgsParser.ArgName, Dictionary<string, object>> args, Action<Dictionary<ArgsParser.ArgName, Dictionary<string, object>>> onStartup) : base()
        {
            this.checkLicense = checkLicense;
            this.onBotStartup = onStartup;
            this.args = args;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            /*Uri uri = new Uri("PresentationFramework.Luna;V3.0.0.0;31bf3856ad364e35;component\\themes/Luna.Homestead.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Add(Application.LoadComponent(uri) as ResourceDictionary);*/

            //Due to a bug in WPF, even though this dictionary is defined in App.xaml, it only gets loaded in desing time and not in runtime. To get around this bug it has to be added again here
            //see https://stackoverflow.com/questions/543414/app-xaml-file-does-not-get-parsed-if-my-app-does-not-set-a-startupuri
            Uri uri2 = new Uri("GUI/DarkTheme.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Add(Application.LoadComponent(uri2) as ResourceDictionary);


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
                botWindow.Loaded += (a, b) => onBotStartup(args);
            botWindow.Closed += (a, b) => this.Shutdown();
            botWindow.Show();
        }
    }
}
