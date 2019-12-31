using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GUIPixelPainter
{
    public class Launcher
    {
        App app;

        bool startupExecuted = false;

        GUI.BotWindow window;
        GUIDataExchange dataExchange;
        GUIHelper helper;
        UsefulDataRepresentation representation;
        GUIUpdater updater;
        UserManager manager;
        DataLoader loader;

        private static Launcher launcher;
        [STAThread]
        public static void Main()
        {
            launcher = new Launcher();

            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += launcher.GlobalUnhandledExceptionHandler;

            launcher.Launch();
        }

        private void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;
            File.AppendAllText("PixelPainterError.txt", ex.Message + "\n" + ex.StackTrace + "\n");
            Save();
        }

        public void Launch()
        {
            EnsureBrowserEmulationEnabled();

            bool lisenceCheckSuc = CheckLicense();
            app = new App(!lisenceCheckSuc, Startup);
            app.Run();
            if (startupExecuted)
                Save();
        }

        private bool CheckLicense()
        {
            string savedKey = GUI.LicenseHelper.LoadSavedKey();
            string hwId = GUI.LicenseHelper.GetHwId();
            if (!string.IsNullOrEmpty(savedKey) && GUI.LicenseHelper.CheckKey(hwId, savedKey))
            {
                return true;
            }
            return false;
        }

        //from https://weblog.west-wind.com/posts/2011/May/21/Web-Browser-Control-Specifying-the-IE-Version
        private void EnsureBrowserEmulationEnabled(string exename = "GUIPixelPainter.exe", bool uninstall = false)
        {
            try
            {
                using (
                    var rk = Registry.CurrentUser.OpenSubKey(
                            @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
                )
                {
                    if (!uninstall)
                    {
                        dynamic value = rk.GetValue(exename);
                        if (value == null)
                            rk.SetValue(exename, (uint)11001, RegistryValueKind.DWord);
                    }
                    else
                        rk.DeleteValue(exename);
                }
            }
            catch
            {
            }
        }

        private void Startup(object sender, System.Windows.RoutedEventArgs e)
        {

            //Create palettes
            var palette = new Dictionary<int, Dictionary<int, System.Drawing.Color>>()
            {
                {
                    7, new Dictionary<int, System.Drawing.Color>() {
                        { 0, System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF)},
                        { 1, System.Drawing.Color.FromArgb(0xC4, 0xC4, 0xC4)},
                        { 2, System.Drawing.Color.FromArgb(0x88, 0x88, 0x88)},
                        { 17, System.Drawing.Color.FromArgb(0x55, 0x55, 0x55)},
                        { 3, System.Drawing.Color.FromArgb(0x22, 0x22, 0x22)},
                        { 18, System.Drawing.Color.FromArgb(0x00, 0x00, 0x00)},
                        { 4, System.Drawing.Color.FromArgb(0xFF, 0xA7, 0xD1)},
                        { 14, System.Drawing.Color.FromArgb(0xCF, 0x6E, 0xE4)},
                        { 19, System.Drawing.Color.FromArgb(0xec, 0x08, 0xec)},
                        { 15, System.Drawing.Color.FromArgb(0x82, 0x00, 0x80)},
                        { 20, System.Drawing.Color.FromArgb(0x6b, 0x00, 0x00)},
                        { 5, System.Drawing.Color.FromArgb(0xE5, 0x00, 0x00)},
                        { 21, System.Drawing.Color.FromArgb(0xff, 0x39, 0x04)},
                        { 6, System.Drawing.Color.FromArgb(0xE5, 0x95, 0x00)},
                        { 8, System.Drawing.Color.FromArgb(0xE5, 0xD9, 0x00)},
                        { 16, System.Drawing.Color.FromArgb(0xff, 0xdf, 0xcc)},
                        { 7, System.Drawing.Color.FromArgb(0xA0, 0x6A, 0x42)},
                        { 22, System.Drawing.Color.FromArgb(0x63, 0x3c, 0x1f)},
                        { 9, System.Drawing.Color.FromArgb(0x94, 0xE0, 0x44)},
                        { 23, System.Drawing.Color.FromArgb(0x51, 0xe1, 0x19)},
                        { 10, System.Drawing.Color.FromArgb(0x02, 0xBE, 0x01)},
                        { 24, System.Drawing.Color.FromArgb(0x00, 0x66, 0x00)},
                        { 11, System.Drawing.Color.FromArgb(0x00, 0xD3, 0xDD)},
                        { 25, System.Drawing.Color.FromArgb(0x36, 0xba, 0xff)},
                        { 12, System.Drawing.Color.FromArgb(0x00, 0x83, 0xC7)},
                        { 26, System.Drawing.Color.FromArgb(0x04, 0x4b, 0xff)},
                        { 13, System.Drawing.Color.FromArgb(0x00, 0x00, 0xEA)},
                        { 27, System.Drawing.Color.FromArgb(0xbb, 0x4f, 0x00)},//premium 1
                        { 28, System.Drawing.Color.FromArgb(0x45, 0xff, 0xc8)}//premium 2
                    }
                },
                {
                    6, new Dictionary<int, System.Drawing.Color>() {
                         {0, System.Drawing.Color.FromArgb(0xCA, 0xDC, 0x9F) },
                         {1, System.Drawing.Color.FromArgb(0x9B, 0xBC, 0x0F) },
                         {2, System.Drawing.Color.FromArgb(0x8B, 0xAC, 0x0F) },
                         {3, System.Drawing.Color.FromArgb(0x30, 0x92, 0x30) },
                         {4, System.Drawing.Color.FromArgb(0x0F, 0x38, 0x0F) }
                    }
                },
                {
                    666, new Dictionary<int, System.Drawing.Color>() {
                         {0, System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF) },
                         {1, System.Drawing.Color.FromArgb(0xC4, 0xC4, 0xC4) },
                         {2, System.Drawing.Color.FromArgb(0x88, 0x88, 0x88) },
                         {3, System.Drawing.Color.FromArgb(0x22, 0x22, 0x22) }
                    }
                }
            };
            Dictionary<int, List<System.Drawing.Color>> GUIPalette = palette.Select((a) =>
            {
                return new KeyValuePair<int, List<System.Drawing.Color>>(a.Key, a.Value.Select((b) => b.Value).ToList());
            }).ToDictionary((a) => a.Key, (a) => a.Value);
            GUIPalette[7].RemoveRange(27, 2); //dont show premium color in UI

            //Create or get everything
            foreach (object w in app.Windows)
            {
                if (w is GUI.BotWindow)
                {
                    window = w as GUI.BotWindow;
                    break;
                }
            }

            helper = new GUIHelper(GUIPalette);
            dataExchange = new GUIDataExchange(window.usersPanel, window.taskList, window.pixelCanvas, window, window.timelapsePanel, helper);
            representation = new UsefulDataRepresentation(dataExchange);
            updater = new GUIUpdater(palette);
            manager = new UserManager(representation, updater, palette);
            loader = new DataLoader(dataExchange, helper);

            //Set window properties
            window.Helper = helper;
            window.taskList.Helper = helper;
            window.pixelCanvas.Helper = helper;
            window.DataExchange = dataExchange;
            window.Launcher = this;

            //Set window children properties
            window.taskList.DataExchange = dataExchange;
            window.usersPanel.DataExchange = dataExchange;
            window.usersPanel.Launcher = this;
            window.pixelCanvas.DataExchange = dataExchange;

            //Set updater properties
            updater.DataExchange = dataExchange;
            updater.Manager = manager;

            //Set dataexchange properties
            dataExchange.UsefulData = representation;
            dataExchange.Updater = updater;

            //Set helper properties
            helper.DataExchange = dataExchange;

            //Load saved data
            loader.Load();

            //Start loading data
            window.Run();
            window.pixelCanvas.Run();

            startupExecuted = true;
        }

        public void Save()
        {
            loader.Save();
        }

        public void ExportNicknames(string path)
        {
            loader.SaveUsernames(path);
        }
    }
}
