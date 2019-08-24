using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
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
        long lastUpdateTime = -1;

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

        public void SetSettings(bool overlayTasks, int canvasId)
        {
            ignoreEvents = true;
            overlay.IsChecked = overlayTasks;
            this.canvasId.Text = canvasId.ToString();
            ignoreEvents = false;
        }

        public int GetCanvasId()
        {
            return int.TryParse(canvasId.Text, out int id) ? id : 7;
        }

        public void AddChatText(string text, System.Windows.Media.Color c)
        {
            chat.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));
            TextBlock msgBlock = new TextBlock();
            msgBlock.Padding = new Thickness(5);
            msgBlock.TextWrapping = TextWrapping.Wrap;
            msgBlock.Text = text;
            msgBlock.Foreground = new SolidColorBrush(c);
            msgBlock.Effect = textShadow;

            chat.Children.Add(msgBlock);
            chatScroll.ScrollToBottom();

            if (chat.Children.Count > 35)
            {
                chat.Children.RemoveAt(0);
            }
        }

        public void UpdateSpeed(int x, int y, System.Windows.Media.Color c, int userId, bool myOwnPixel)
        {
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
                    if (myOwnPixel)
                        speed /= 2;
                    string userName = Helper.GetUsernameById(userTime.Key);
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
            if (DataExchange.CreateChatMessage(chatTextBox.Text, 0))
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

    }
}
