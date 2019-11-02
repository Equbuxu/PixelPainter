using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for BotWindow.xaml
    /// </summary>
    public partial class BotWindow : Window
    {
        private Dictionary<int, LinkedList<long>> lastUserPlaceTimes = new Dictionary<int, LinkedList<long>>();
        private Dictionary<int, Label> nicksLabel = new Dictionary<int, Label>();
        private Dictionary<int, Label> speedsLabel = new Dictionary<int, Label>();
        private bool ignoreEvents = true;
        private long lastUpdateTime = -1;
        private string halfNickname = "";

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

            chatLocal.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            chatGlobal.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            globalChatMode.IsChecked = true;

            speedPanelGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));

            textShadow.Color = System.Windows.Media.Color.FromRgb(0, 0, 0);
            textShadow.Direction = 320;
            textShadow.ShadowDepth = 1;
            textShadow.Opacity = 0.5;
            textShadow.BlurRadius = 0.5;

            var updateTimer = new Timer(500);
            updateTimer.Elapsed += (a, b) => { try { Dispatcher.Invoke(() => DataExchange.CreateUpdate()); } catch (TaskCanceledException) { } };
            updateTimer.Start();

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

        public void SetWindowState(double width, double height, WindowState state)
        {
            this.Width = width;
            this.Height = height;
            this.WindowState = state;
            OnWindowSizeStateChange(null, null);
        }

        public void SetSettings(bool overlayTasks, bool overlayAllTasks, bool overlaySelectedTask, double overlayTranslucency, int canvasId, PlacementMode placementMode)
        {
            ignoreEvents = true;
            overlay.IsChecked = overlayTasks;
            overlayAll.IsChecked = overlayAllTasks;
            overlaySelected.IsChecked = overlaySelectedTask;
            overlayNothing.IsChecked = !overlayTasks && !overlayAllTasks && !overlaySelectedTask;
            this.overlayTranslucency.Value = overlayTranslucency;
            this.canvasId.Text = canvasId.ToString();
            this.placementMode.SelectedIndex = placementMode == PlacementMode.DENOISE ? 1 : 0;

            ignoreEvents = false;
            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        public void ClearChat()
        {
            chatLocal.Children.Clear();
            chatGlobal.Children.Clear();
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

        public void UpdateSpeed(int x, int y, System.Windows.Media.Color c, int userId, bool myOwnPixel)
        {
            if (myOwnPixel)
                halfNickname = Helper.GetUsernameById(userId);

            if (!lastUserPlaceTimes.ContainsKey(userId))
            {
                lastUserPlaceTimes.Add(userId, new LinkedList<long>());
                Label lableNick = new Label();
                lableNick.FontWeight = FontWeights.Bold;
                lableNick.Effect = textShadow;

                Label labelSpeed = new Label();
                labelSpeed.FontWeight = FontWeights.Bold;
                labelSpeed.Effect = textShadow;

                nicksLabel.Add(userId, lableNick);
                speedsLabel.Add(userId, labelSpeed);

                speedPanelName.Children.Add(lableNick);
                speedPanelSpeed.Children.Add(labelSpeed);
            }
            var time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            lastUserPlaceTimes[userId].AddLast(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            nicksLabel[userId].Foreground = new SolidColorBrush(c);
            speedsLabel[userId].Foreground = new SolidColorBrush(c);
            while (time - lastUserPlaceTimes[userId].First.Value > 10000)
                lastUserPlaceTimes[userId].RemoveFirst();

            if (time - lastUpdateTime > 100)
            {
                lastUpdateTime = time;
                List<int> toDelete = new List<int>();

                foreach (KeyValuePair<int, LinkedList<long>> userTime in lastUserPlaceTimes)
                {
                    if (time - userTime.Value.Last.Value > 11000)
                    {
                        toDelete.Add(userTime.Key);
                        continue;
                    }
                    double dT = (time - userTime.Value.First.Value) / 1000.0;
                    double speed = userTime.Value.Count / (dT == 0 ? 1 : dT);
                    string userName = Helper.GetUsernameById(userTime.Key);
                    if (userName == halfNickname)
                        speed /= 2;
                    nicksLabel[userTime.Key].Content = userName.ToString() + ':';
                    speedsLabel[userTime.Key].Content = speed.ToString("0.00") + " px/s";
                }

                foreach (int id in toDelete)
                {
                    lastUserPlaceTimes.Remove(id);
                    speedPanelName.Children.Remove(nicksLabel[id]);
                    speedPanelSpeed.Children.Remove(speedsLabel[id]);

                    nicksLabel.Remove(id);
                    speedsLabel.Remove(id);
                }
            }
        }

        public void SetLoadingState(bool loading)
        {
            enabled.IsEnabled = !loading;
            canvasId.IsEnabled = !loading;
            enabled.IsChecked = loading ? false : enabled.IsChecked;
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
            if (DataExchange.CreateChatMessage(chatTextBox.Text, 0, (bool)globalChatMode.IsChecked ? 0 : GetCanvasId()))
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
            foreach (KeyValuePair<int, LinkedList<long>> pair in lastUserPlaceTimes)
            {
                long last = pair.Value.Last.Value;
                pair.Value.Clear();
                pair.Value.AddLast(last);
            }
        }

        private void OnTranslucencyValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DataExchange.UpdateTranslucencyFromGUI();
        }

        private void OnWindowSizeStateChange(object sender, EventArgs e)
        {
            DataExchange?.UpdateWindowStateFromUI();
        }

        private void OnExportNicknames(object sender, RoutedEventArgs e)
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
    }
}
