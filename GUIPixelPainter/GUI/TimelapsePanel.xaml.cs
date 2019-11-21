using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for TimelapsePanel.xaml
    /// </summary>
    public partial class TimelapsePanel : UserControl
    {
        public TimelapsePanel()
        {
            InitializeComponent();
            TimelapsePanelViewModel model = new TimelapsePanelViewModel();
            border.DataContext = model;
        }
    }

    public class TimelapsePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private TimelapsePanelModel model = new TimelapsePanelModel();

        public string FPS
        {
            get { return model.fps.ToString(); }
            set
            {
                int result;
                if (int.TryParse(value, out result))
                {
                    model.fps = result;
                    if (model.fps < 5)
                        model.fps = 5;
                    else if (model.fps > 60)
                        model.fps = 60;
                    RaisePropertyChanged("FPS");
                }
            }
        }

        public string SpeedMult
        {
            get { return model.speedMult.ToString("F", CultureInfo.InvariantCulture) + "x"; }
            set
            {
                double result;
                if (double.TryParse(value.Replace("x", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                {
                    model.speedMult = result;
                    if (model.speedMult < 10)
                        model.speedMult = 10;
                    else if (model.speedMult > 3600)
                        model.speedMult = 3600;
                    RaisePropertyChanged("SpeedMult");
                }
            }
        }

        public string FragLength
        {
            get { return model.fragLength.ToString(); }
            set
            {
                int result;
                if (int.TryParse(value, out result))
                {
                    model.fragLength = result;
                    if (model.fragLength < 5)
                        model.fragLength = 5;
                    else if (model.fragLength > 900)
                        model.fragLength = 900;
                    RaisePropertyChanged("FragLength");
                }
            }
        }

        public bool recording;
        public bool Recording
        {
            get => recording;
            private set
            {
                recording = value;
                RaisePropertyChanged("Recording");
                RaisePropertyChanged("NotRecording");
                RaisePropertyChanged("StatusText");
            }
        }

        public bool NotRecording { get { return !Recording; } }

        public ICommand StartRec
        {
            get
            {
                return new ButtonCommand(() =>
                    {
                        Recording = true;
                        model.StartRecording();
                    });
            }
        }

        public ICommand StopRec
        {
            get
            {
                return new ButtonCommand(() =>
                    {
                        Recording = false;
                        model.StopRecording();
                    });
            }
        }

        public string StatusText
        {
            get
            {
                if (NotRecording)
                    return "Not recording.";
                return "Recording...\n Total frames: 10 \n Video length: 10 min 5 sec \n Time passed: 112 min 42 sec";
            }
        }

        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public TimelapsePanelViewModel()
        {
            model.fps = 60;
            model.speedMult = 20;
            model.fragLength = 300;
        }
    }

    public class ButtonCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private Action action;

        public ButtonCommand(Action act)
        {
            action = act;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            action();
        }
    }

    public class TimelapsePanelModel
    {
        public int fps;
        public double speedMult;
        public int fragLength;

        private bool recording = false;

        public void StartRecording()
        {
            if (recording)
                return;
            recording = true;
            Record(fps, speedMult, fragLength);
        }

        private void Record(int fps, double mult, int fragLength)
        {
            int totalFrames = fps * fragLength;
            int frameDelay = (int)(1000.0 / fps * mult);

            long lastFrameTime = 0;
            for (int i = 0; i < totalFrames; i++)
            {
                if (!recording)
                    break;
                lastFrameTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;


                //get frame
                //send frame

                long time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                long toWait = frameDelay - (time - lastFrameTime);
                if (toWait > 0)
                    Thread.Sleep((int)toWait);
            }
        }

        public void StopRecording(Action callback)
        {
            recording = false;

        }
    }
}
