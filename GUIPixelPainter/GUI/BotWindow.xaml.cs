using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for BotWindow.xaml
    /// </summary>
    public partial class BotWindow : Window
    {
        private System.Timers.Timer speedLabelTimer;
        private List<Tuple<int, long, Color>> recentPixels = new List<Tuple<int, long, Color>>();
        private bool ignoreEvents = true;

        DropShadowEffect textShadow = new DropShadowEffect();

        public GUIDataExchange DataExchange { get; set; }
        public GUIHelper Helper { get; set; }
        public Launcher Launcher { get; set; }

        public BotWindow()
        {
            InitializeComponent();
        }

        public void Run()
        {
            Debug.Assert(DataExchange != null && Helper != null && Launcher != null);

            //chatLocal.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            //chatGlobal.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            globalChatMode.IsChecked = true;

            //speedPanelGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));

            textShadow.Color = System.Windows.Media.Color.FromRgb(255, 255, 255);
            textShadow.Direction = 320;
            textShadow.ShadowDepth = 0.5;
            textShadow.Opacity = 1.0;
            textShadow.BlurRadius = 2.0;
            textShadow.Freeze(); //possibly fix memory leak

            var updateTimer = new Timer(500);
            updateTimer.Elapsed += (a, b) => { try { Dispatcher.Invoke(() => DataExchange.CreateUpdate()); } catch (TaskCanceledException) { } };
            updateTimer.Start();

            speedLabelTimer = new System.Timers.Timer(1000);
            speedLabelTimer.AutoReset = true;
            speedLabelTimer.Elapsed += (a, b) => { try { Dispatcher.Invoke(RecalculateSpeedLabels); } catch (TaskCanceledException) { } };
            speedLabelTimer.Start();

            ignoreEvents = true;
            if (canvasId.Text.Length == 0)
                canvasId.Text = "7";
            ignoreEvents = false;

            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        public bool IsBotEnabled()
        {
            return enabled.IsChecked == true;
        }

        public bool IsOverlayEnabled()
        {
            return overlay.IsChecked == true;
        }

        public bool IsOverlayAllEnabled()
        {
            return overlayAll.IsChecked == true;
        }

        public bool IsOverlaySelectedEnabled()
        {
            return overlaySelected.IsChecked == true;
        }

        public bool IsTrackingEnabled()
        {
            return trackUsers.IsChecked == true;
        }

        public double GetOverlayTranslucency()
        {
            return overlayTranslucency.Value;
        }

        public double GetWindowWidth()
        {
            return Width;
        }

        public double GetWindowHeight()
        {
            return Height;
        }

        public WindowState GetWindowState()
        {
            return this.WindowState;
        }

        public PlacementMode GetPlacementMode()
        {
            switch (placementMode.SelectedIndex)
            {
                default:
                case 0:
                    return PlacementMode.TOPDOWN;
                case 1:
                    return PlacementMode.DENOISE;
            }
        }

        public int GetCanvasId()
        {
            return int.TryParse(canvasId.Text, out int id) ? id : 7;
        }

        public double GetPlacementSpeed()
        {
            double speed = 11.2;
            if (!double.TryParse(placementSpeed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
            {
                if (!double.TryParse(placementSpeed.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
                    speed = 11.2;
            }

            if (speed > 15)
                speed = 15;
            else if (speed < 1)
                speed = 1;

            return speed;
        }

        public void SetWindowState(double width, double height, WindowState state)
        {
            this.Width = width;
            this.Height = height;
            this.WindowState = state;
            OnWindowSizeStateChange(null, null);
        }

        public void SetSettings(bool overlayTasks, bool overlayAllTasks, bool overlaySelectedTask, double overlayTranslucency, int canvasId, PlacementMode placementMode, double placementSpeed)
        {
            ignoreEvents = true;
            overlay.IsChecked = overlayTasks;
            overlayAll.IsChecked = overlayAllTasks;
            overlaySelected.IsChecked = overlaySelectedTask;
            overlayNothing.IsChecked = !overlayTasks && !overlayAllTasks && !overlaySelectedTask;
            this.overlayTranslucency.Value = overlayTranslucency;
            this.canvasId.Text = canvasId.ToString();
            this.placementMode.SelectedIndex = placementMode == PlacementMode.DENOISE ? 1 : 0;
            this.placementSpeed.Text = placementSpeed.ToString(CultureInfo.InvariantCulture);

            ignoreEvents = false;
            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        public void SetCanvasId(int id)
        {
            ignoreEvents = true;
            this.canvasId.Text = id.ToString();
            ignoreEvents = false;

            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        public void ClearChatAndSpeed()
        {
            chatLocal.Children.Clear();
            chatGlobal.Children.Clear();
            recentPixels.Clear();
        }

        public void AddChatText(string text, bool isLocal, System.Windows.Media.Color c)
        {
            TextBlock msgBlock = new TextBlock();
            msgBlock.Padding = new Thickness(5);
            msgBlock.TextWrapping = TextWrapping.Wrap;
            msgBlock.Foreground = new SolidColorBrush(c);
            msgBlock.Effect = textShadow;

            //Highlight hyperlinks
            Regex regex = new Regex(@"(http(s)?:\/\/.)?(www\.)?[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)");
            MatchCollection matches = regex.Matches(text);

            List<string> splitLinks = new List<string>();
            int lastMatchEnd = 0;
            foreach (Match match in matches)
            {
                string segment = text.Substring(lastMatchEnd, match.Index - lastMatchEnd);
                splitLinks.Add(segment);
                lastMatchEnd = match.Index + match.Length;
            }
            string finalSegment = text.Substring(lastMatchEnd, text.Length - lastMatchEnd);
            splitLinks.Add(finalSegment);

            int lastSegment = 0;
            foreach (Match match in matches)
            {
                msgBlock.Inlines.Add(splitLinks[lastSegment]);
                lastSegment++;
                Hyperlink link;
                try
                {
                    link = new Hyperlink(new Run(match.Value))
                    {
                        NavigateUri = new Uri(match.Value)
                    };
                }
                catch (UriFormatException)
                {
                    try
                    {
                        string withhttp = "http://" + match.Value;
                        link = new Hyperlink(new Run(match.Value))
                        {
                            NavigateUri = new Uri(withhttp),
                        };
                    }
                    catch (UriFormatException)
                    {
                        link = new Hyperlink(new Run(match.Value));
                    }
                }
                link.RequestNavigate += (sender, e) =>
                {
                    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                    e.Handled = true;
                };

                msgBlock.Inlines.Add(link);
            }

            msgBlock.Inlines.Add(splitLinks[lastSegment]);

            if (isLocal)
            {
                chatLocal.Children.Add(msgBlock);
                chatScrollLocal.ScrollToBottom();
                if (chatLocal.Children.Count > 35)
                    chatLocal.Children.RemoveAt(0);
            }
            else
            {
                chatGlobal.Children.Add(msgBlock);
                chatScrollGlobal.ScrollToBottom();
                if (chatGlobal.Children.Count > 35)
                    chatGlobal.Children.RemoveAt(0);
            }
        }

        public void UpdateSpeed(System.Windows.Media.Color c, int userId)
        {
            long curTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            recentPixels.Add(new Tuple<int, long, Color>(userId, curTime, c));
        }

        public void SetLoadingState(bool loading)
        {
            enabled.IsEnabled = !loading;
            canvasId.IsEnabled = !loading;
            enabled.IsChecked = loading ? false : enabled.IsChecked;
        }

        private void RecalculateSpeedLabels()
        {
            int timeFrameMs = 11000;
            long curTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            Dictionary<int, Color> userColor = new Dictionary<int, Color>();
            Dictionary<int, int> userPixelCounts = new Dictionary<int, int>();
            for (int i = recentPixels.Count - 1; i >= 0; i--)
            {
                var pixel = recentPixels[i];
                if (pixel.Item2 < curTime - timeFrameMs)
                {
                    recentPixels.RemoveAt(i);
                    continue;
                }
                if (!userColor.ContainsKey(pixel.Item1))
                {
                    userColor.Add(pixel.Item1, pixel.Item3);
                    userPixelCounts.Add(pixel.Item1, 0);
                }
                userPixelCounts[pixel.Item1]++;
            }


            List<Tuple<string, string, SolidColorBrush>> labels = new List<Tuple<string, string, SolidColorBrush>>();

            foreach (KeyValuePair<int, int> pair in userPixelCounts)
            {
                labels.Add(new Tuple<string, string, SolidColorBrush>(Helper.GetUsernameById(pair.Key), (pair.Value / (timeFrameMs / 1000.0)).ToString("0.00") + " px/s", new SolidColorBrush(userColor[pair.Key])));
            }

            labels.Sort((a, b) => a.Item1.CompareTo(b.Item1));


            if (speedPanelName.Children.Count > labels.Count)
            {
                int diff = speedPanelName.Children.Count - labels.Count;
                speedPanelName.Children.RemoveRange(labels.Count, diff);
                speedPanelSpeed.Children.RemoveRange(labels.Count, diff);
            }
            else if (speedPanelName.Children.Count < labels.Count)
            {
                int diff = labels.Count - speedPanelName.Children.Count;

                for (int i = 0; i < diff; i++)
                {
                    Label username = new Label();
                    username.FontWeight = FontWeights.Bold;
                    username.Effect = textShadow;

                    Label speed = new Label();
                    speed.FontWeight = FontWeights.Bold;
                    speed.Effect = textShadow;

                    speedPanelName.Children.Add(username);
                    speedPanelSpeed.Children.Add(speed);
                }
            }

            for (int i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                (speedPanelName.Children[i] as Label).Foreground = label.Item3;
                (speedPanelName.Children[i] as Label).Content = label.Item1;
                (speedPanelSpeed.Children[i] as Label).Content = label.Item2;
            }
        }

        private void OnGeneralSettingChange(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            canvasId.SelectAll();
        }

        private void OnChatSend(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            if (DataExchange.CreateChatMessage(chatTextBox.Text, (bool)globalChatMode.IsChecked ? 0 : GetCanvasId()))
                chatTextBox.Text = "";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            Launcher.Save();
        }

        private void OnChatTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (ignoreEvents)
                return;
            if (e.Key != Key.Enter)
                return;
            OnChatSend(null, null);
        }

        private void OnSpeedRefresh(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            recentPixels.Clear();
        }

        private void OnTranslucencyValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DataExchange.UpdateTranslucencyFromGUI();
        }

        private void OnWindowSizeStateChange(object sender, EventArgs e)
        {
            DataExchange?.UpdateWindowStateFromUI();
        }

        private void OnExportUsernames(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON|*.json";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (saveFileDialog.ShowDialog() == true)
                Launcher.ExportNicknames(saveFileDialog.FileName);
        }

        private void OnChangeChatMode(object sender, RoutedEventArgs e)
        {
            if (sender == globalChatMode)
            {
                chatScrollGlobal.Visibility = Visibility.Visible;
                chatScrollLocal.Visibility = Visibility.Collapsed;
            }
            else
            {
                chatScrollGlobal.Visibility = Visibility.Collapsed;
                chatScrollLocal.Visibility = Visibility.Visible;
            }
        }

        private void OnImportUsernames(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON|*.json";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (openFileDialog.ShowDialog() == false)
                return;

            Dictionary<int, string> names;
            try
            {
                names = JsonConvert.DeserializeObject<Dictionary<int, string>>(File.ReadAllText(openFileDialog.FileName));
            }
            catch (Exception)
            {
                MessageBox.Show("Could not load usernames");
                return;
            }
            foreach (KeyValuePair<int, string> pair in names)
            {
                Helper.AddUsername(pair.Key, pair.Value);
            }
        }

        private void OnClearBrushQueue(object sender, RoutedEventArgs e)
        {
            DataExchange.CreateClearManualTask();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.System)
            {
                e.Handled = true;
            }
        }
    }
}
