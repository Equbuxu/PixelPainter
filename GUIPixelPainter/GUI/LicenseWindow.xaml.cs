using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Interaction logic for LicenseWindow.xaml
    /// </summary>
    public partial class LicenseWindow : Window
    {
        public string HardwareID
        {
            get; private set;
        }

        public string LicenseKey
        {
            get; set;
        }

        public bool success
        {
            get; private set;
        }
        public LicenseWindow()
        {
            HardwareID = LicenseHelper.GetHwId();

            InitializeComponent();
            mainPanel.DataContext = this;

        }

        private void Check(object sender, RoutedEventArgs e)
        {
            if (LicenseHelper.CheckKey(HardwareID, LicenseKey))
            {
                LicenseHelper.SaveKey(LicenseKey);
                success = true;
                this.Close();
            }
            else
            {
                invalidKeyBlock.Visibility = Visibility.Visible;
            }
        }
    }
}
