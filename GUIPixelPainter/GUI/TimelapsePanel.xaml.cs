using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        }

        public void SetViewModel(TimelapsePanelViewModel model)
        {
            border.DataContext = model;
        }
    }

    public class TimelapsePanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private TimelapsePanelModel model;

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

        public string RecTime
        {
            get { return TimeSpan.FromSeconds(model.targetRecTime).ToString(); }
            set
            {
                TimeSpan result;
                if (TimeSpan.TryParse(value, out result))
                {
                    model.targetRecTime = (int)result.TotalSeconds;
                    if (model.targetRecTime < 1)
                        model.targetRecTime = 1;
                    else if (model.targetRecTime > 3000000)
                        model.targetRecTime = 3000000;
                    RaisePropertyChanged("RecTime");
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

        public bool? RestartRec
        {
            get => model.restartOnCompletion;
            set
            {
                model.restartOnCompletion = (bool)value;
                RaisePropertyChanged("RestartRec");
            }
        }

        public bool NotRecording { get { return !Recording; } }

        public ICommand StartRec
        {
            get
            {
                return new ButtonCommand(StartRecording);
            }
        }

        public ICommand StopRec
        {
            get
            {
                return new ButtonCommand(StopRecording);
            }
        }

        public string StatusText
        {
            get
            {
                if (NotRecording)
                    return "Not recording.";
                return String.Format("Recording...\n Total frames: {0} \n Video length: {1} \n Time passed: {2}",
                    model.TotalFrames,
                    TimeSpan.FromSeconds(model.TotalSecondsVideo).ToString(),
                    TimeSpan.FromSeconds(model.TotalSecondsReal).ToString()
                    );
            }
        }

        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void StartRecording()
        {
            if (!File.Exists("ffmpeg.exe"))
            {
                MessageBox.Show("Couldn't start ffmpeg");
                return;
            }
            Recording = true;
            model.StartRecording(false);
        }

        public void StopRecording()
        {
            Recording = false;
            model.StopRecording();
        }

        public TimelapsePanelViewModel(TimelapsePanelModel model)
        {
            this.model = model;
            model.PropertyChanged += OnModelPropertyChanged;
            model.fps = 60;
            model.speedMult = 20;
            model.restartOnCompletion = false;
            model.targetRecTime = 600;
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TotalFrames" || e.PropertyName == "TotalSecondsVideo" || e.PropertyName == "TotalSecondsReal")
                RaisePropertyChanged("StatusText");
            else if (e.PropertyName == "Recording")
            {
                this.Recording = model.Recording;
            }
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

    public class TimelapsePanelModel : INotifyPropertyChanged
    {
        public int fps;
        public double speedMult;
        public int targetRecTime;
        public bool restartOnCompletion;

        private Timer frameTimer = new Timer();
        private Timer restartDelayTimer = new Timer();

        private bool recording = false;
        public bool Recording
        {
            get
            {
                return recording;
            }
            private set
            {
                recording = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Recording"));
            }
        }

        private Process ffmpeg;

        private GUIDataExchange dataExchange;

        public event PropertyChangedEventHandler PropertyChanged;

        private int totalFrames;
        public int TotalFrames
        {
            get
            {
                return totalFrames;
            }
            private set
            {
                totalFrames = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TotalFrames"));
            }
        }

        private int totalSecondsVideo;
        public int TotalSecondsVideo
        {
            get
            {
                return totalSecondsVideo;
            }
            private set
            {
                totalSecondsVideo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TotalSecondsVideo"));
            }
        }

        private int totalSecondsReal;
        public int TotalSecondsReal
        {
            get
            {
                return totalSecondsReal;
            }
            private set
            {
                totalSecondsReal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TotalSecondsReal"));
            }
        }

        private long startButtonPressMs;
        private long startMs;
        private int selectedFps;
        private int selectedTargetRecTime;
        private int restartCount;

        public TimelapsePanelModel(GUIDataExchange dataExchange)
        {
            frameTimer.Elapsed += SaveFrame;
            frameTimer.AutoReset = true;
            restartDelayTimer.Elapsed += (a, b) => StartRecording(true);
            restartDelayTimer.AutoReset = false;

            this.dataExchange = dataExchange;
        }

        public void StartRecording(bool restart)
        {
            if (Recording)
                return;
            Recording = true;

            TotalFrames = 0;
            TotalSecondsReal = 0;
            TotalSecondsVideo = 0;

            StartFFMpeg(fps);

            if (!restart)
            {
                startMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                startButtonPressMs = startMs;

                selectedFps = fps;
                selectedTargetRecTime = targetRecTime;
                restartCount = 0;
            }
            else
            {
                restartCount++;
                startMs = startButtonPressMs + selectedTargetRecTime * 1000 * restartCount;
            }

            int frameDelay = (int)(1000.0 / selectedFps * speedMult);
            frameTimer.Interval = frameDelay;
            frameTimer.Start();
        }

        private void StartFFMpeg(int selFps)
        {
            var size = dataExchange.GetCanvasSize();

            if (!Directory.Exists("videoout"))
                Directory.CreateDirectory("videoout");
            string filename = "videoout//" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = @"ffmpeg.exe";
            ffmpeg.StartInfo.Arguments = "-f rawvideo -r " + selFps + " -video_size " + size.Width + "x" + size.Height + " -pixel_format bgra -i pipe:0 -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2:color=Black\" -r " + selFps + " -y -pix_fmt yuv420p " + filename + ".mp4";
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardInput = true;
            ffmpeg.StartInfo.RedirectStandardOutput = true;
            //ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.Start();
        }

        private void SaveFrame(object sender, ElapsedEventArgs args)
        {
            TotalFrames++;

            TotalSecondsReal = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerSecond - startMs / 1000);
            TotalSecondsVideo = TotalFrames / selectedFps;

            long totalMsReal = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startMs;
            long targetMs = selectedTargetRecTime * 1000;
            long remainingMs = targetMs - totalMsReal;

            dataExchange.SaveBitmapToStream(ffmpeg.StandardInput.BaseStream);

            if (remainingMs < frameTimer.Interval)
            {
                StopRecording();
                if (restartOnCompletion)
                {
                    //recalculate time since StopRecording wait for ffmpeg to exit
                    totalMsReal = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startMs;
                    remainingMs = targetMs - totalMsReal;

                    if (remainingMs > 0)
                    {
                        restartDelayTimer.Interval = remainingMs;
                        restartDelayTimer.Start();
                    }
                    else
                    {
                        StartRecording(true);
                    }
                }
            }
        }

        public void StopRecording()
        {
            if (!Recording)
                return;
            frameTimer.Stop();

            ffmpeg.StandardInput.BaseStream.Close();
            //ffmpeg.StandardError.BaseStream.Close();
            ffmpeg.WaitForExit();
            Recording = false;
        }
    }
}
