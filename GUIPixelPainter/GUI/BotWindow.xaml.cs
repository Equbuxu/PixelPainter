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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for BotWindow.xaml
    /// </summary>
    public partial class BotWindow : Window
    {
        public GUIDataExchange DataExchange { get; set; }

        public BotWindow()
        {
            InitializeComponent();

            Launcher laucher = new Launcher();
            laucher.Launch(this);

            superimpose.IsChecked = true;

            var updateTimer = new Timer(500);
            updateTimer.Elapsed += (a, b) => Dispatcher.Invoke(() => DataExchange.CreateUpdate());
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
