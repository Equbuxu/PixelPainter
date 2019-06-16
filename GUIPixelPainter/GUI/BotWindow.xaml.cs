using System;
using System.Collections.Generic;
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
        private Dictionary<int, StackPanel> speedLabels = new Dictionary<int, StackPanel>();
        long lastUpdateTime = -1;
        private Dictionary<int, string> knownUsernames = new Dictionary<int, string>()
        {
            {21246, "Equbuxu"},
            {29297, "Uncertain" },
            {21235, "Powerlay" },
            {29396, "Kisalena" },
        };
        DropShadowEffect textShadow = new DropShadowEffect();

        public GUIDataExchange DataExchange { get; set; }

        public BotWindow()
        {
            InitializeComponent();

            Launcher laucher = new Launcher();
            laucher.Launch(this);

            superimpose.IsChecked = true;
            (speedPanel.Parent as Border).Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xDD, 0xDD, 0xDD));

            textShadow.Color = System.Windows.Media.Color.FromRgb(0, 0, 0);
            textShadow.Direction = 320;
            textShadow.ShadowDepth = 1;
            textShadow.Opacity = 0.5;
            textShadow.BlurRadius = 0.5;


            var updateTimer = new Timer(500);
            updateTimer.Elapsed += (a, b) => Dispatcher.Invoke(() => DataExchange.CreateUpdate()); //TODO exception on close: task cancelled
            updateTimer.Start();
        }

        public bool IsBotEnabled()
        {
            return enabled.IsChecked == true;
        }

        public bool IsSuperimpositionEnabled()
        {
            return superimpose.IsChecked == true;
        }

        public int GetCanvasId()
        {
            int id;
            return int.TryParse(canvasId.Text, out id) ? id : 7;
        }

        /*public void OnChatMessage(object sender, MessageEventArgs args)
        {
            if (String.IsNullOrWhiteSpace(args.guild))
                AddChatText(String.Format("{0}: {1}", args.username, args.message));
            else
                AddChatText(String.Format("<{0}>{1}: {2}", args.guild, args.username, args.message));
        }*/

        public void AddChatText(string text)
        {
            chat.Text += "\n" + text;
            if (chat.Text.Length > 1000)
                chat.Text = chat.Text.Substring(chat.Text.Length - 1000);
            chatScroll.ScrollToBottom();
        }

        public void UpdateSpeed(int x, int y, System.Windows.Media.Color c, int userId)
        {
            if (!lastUserPlaceTimes.ContainsKey(userId))
            {
                lastUserPlaceTimes.Add(userId, new LinkedList<long>());
                Label lableNick = new Label();
                lableNick.FontWeight = FontWeights.Bold;
                lableNick.Effect = textShadow;
                lableNick.Width = 125;

                Label labelSpeed = new Label();
                labelSpeed.FontWeight = FontWeights.Bold;
                labelSpeed.Effect = textShadow;

                StackPanel rowUserSpeed = new StackPanel();
                rowUserSpeed.Orientation = Orientation.Horizontal;
                rowUserSpeed.Children.Add(lableNick);
                rowUserSpeed.Children.Add(labelSpeed);
                
                speedLabels.Add(userId, rowUserSpeed);
                speedPanel.Children.Add(rowUserSpeed);
            }
            var time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            lastUserPlaceTimes[userId].AddLast(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            (speedLabels[userId].Children[0] as Label).Foreground = new SolidColorBrush(c);
            (speedLabels[userId].Children[1] as Label).Foreground = new SolidColorBrush(c);
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
                    string userName = knownUsernames.ContainsKey(userTime.Key) ? knownUsernames[userTime.Key] : userTime.Key.ToString();
                    (speedLabels[userTime.Key].Children[0] as Label).Content = userName.ToString() + ':';
                    (speedLabels[userTime.Key].Children[1] as Label).Content = speed.ToString("0.00") + " px/s";
                }

                foreach (int id in toDelete)
                {
                    lastUserPlaceTimes.Remove(id);
                    speedPanel.Children.Remove(speedLabels[id]);
                    speedLabels.Remove(id);
                }
            }
        }

        private void OnGeneralSettingChange(object sender, RoutedEventArgs e)
        {
            DataExchange.UpdateGeneralSettingsFromGUI();
        }

        private void OnChatSend(object sender, RoutedEventArgs e)
        {
            if (DataExchange.CreateChatMessage(chatTextBox.Text, 0))
                chatTextBox.Text = "";
        }

        private void OnChatTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            OnChatSend(null, null);
        }
    }
}
