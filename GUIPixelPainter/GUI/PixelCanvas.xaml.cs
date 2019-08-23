using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for PixelCanvas.xaml
    /// </summary>
    public partial class PixelCanvas : UserControl
    {
        enum Tools
        {
            MOVE,
            PENCIL,
            BRUSH,
            HISTORYBRUSH
        }

        class Pixel
        {
            public Color c = new Color();
            public int x = 0;
            public int y = 0;
        }

        private ScaleTransform scale = new ScaleTransform();
        private TranslateTransform translate = new TranslateTransform();
        private Point mouseDownPoint = new Point();
        private bool shiftDirectionDeterm = false;
        private bool shiftDirHor = false;

        private Tools tool = Tools.MOVE;
        private Color selectedColor = Color.FromArgb(0, 1, 2, 3);
        private int scalingPower = 0;

        private Dictionary<int, Border> nameLabels = new Dictionary<int, Border>();
        private Dictionary<int, long> userPlaceTime = new Dictionary<int, long>();
        private long lastUpdateTime = -1;

        private WriteableBitmap bitmap;
        private System.Drawing.Bitmap revertState;
        private int canvasId = -1;

        private List<Pixel> manualTask = new List<Pixel>();

        private GUIHelper helper;
        public GUIHelper Helper { get { return helper; } set { helper = value; CreatePalette(); } }
        public GUIDataExchange DataExchange { get; set; }

        public PixelCanvas()
        {
            InitializeComponent();

            TransformGroup group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            MainCanvas.RenderTransform = group;


            OnToolClick(moveTool, null);
        }

        public void ReloadCanvas(int id)
        {
            canvasId = id;
            Console.WriteLine("loading canvas {0}", id);
            try
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create("https://pixelplace.io/canvas/" + id.ToString() + ".png");
                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream responseStream = response.GetResponseStream();
                using (var loadedBitmap = new System.Drawing.Bitmap(responseStream))
                {
                    bitmap = new WriteableBitmap(Helper.Convert(loadedBitmap));
                    MainImage.Source = bitmap;
                }
                CreatePalette();
            }
            catch (System.Net.WebException)
            {
                Console.WriteLine("invalid canvas in pixelcanvas");
            }
            OnSaveRevertStateClick(null, null);
        }

        public void OverlayTasks(List<GUITask> tasks)
        {
            for (int i = MainCanvas.Children.Count - 1; i >= 0; i--)
            {
                FrameworkElement elem = MainCanvas.Children[i] as FrameworkElement;
                if ((string)elem.Tag == "taskOverlay")
                    MainCanvas.Children.Remove(elem);
            }
            foreach (GUITask task in tasks)
            {
                Image image = new Image();
                image.Source = Helper.Convert(task.Dithering == true ? task.DitheredConvertedBitmap : task.ConvertedBitmap);
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                image.Opacity = 0.5;
                Canvas.SetLeft(image, task.X);
                Canvas.SetTop(image, task.Y);
                image.Tag = "taskOverlay";
                MainCanvas.Children.Add(image);
            }
        }

        public void SetPixel(int x, int y, Color color, int userId)
        {
            bitmap.SetPixel(x, y, color);
            UpdateNameLabel(x, y, color, userId);
        }

        private void UpdateNameLabel(int x, int y, Color c, int userId)
        {
            if (!userPlaceTime.ContainsKey(userId))
            {
                userPlaceTime.Add(userId, 0);
                nameLabels.Add(userId, AddNameLabel(userId, x, y, c));
            }

            var time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            userPlaceTime[userId] = time;
            Canvas.SetLeft(nameLabels[userId], x + 1);
            Canvas.SetTop(nameLabels[userId], y + 1);
            (nameLabels[userId].Child as TextBlock).Foreground = new SolidColorBrush(c);

            if (time - lastUpdateTime > 100)
            {
                lastUpdateTime = time;
                List<int> toDelete = new List<int>();

                foreach (KeyValuePair<int, long> userTime in userPlaceTime)
                {
                    if (time - userTime.Value > 1500)
                        toDelete.Add(userTime.Key);
                }

                foreach (int id in toDelete)
                {
                    userPlaceTime.Remove(id);
                    MainCanvas.Children.Remove(nameLabels[id]);
                    nameLabels.Remove(id);
                }
            }
        }

        private Border AddNameLabel(int name, int x, int y, Color color)
        {
            Border border = new Border()
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.0),
                Background = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                Height = 16,
            };
            TextBlock nameLabel = new TextBlock()
            {
                Text = Helper.GetUsernameById(name),
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = nameLabel;
            Canvas.SetLeft(border, x + 1);
            Canvas.SetTop(border, y + 1);
            MainCanvas.Children.Add(border);
            return border;
        }

        private void CreatePalette()
        {
            palettePanel.Children.Clear();

            int key = canvasId;
            if (!Helper.Palette.ContainsKey(key))
                key = 7;

            foreach (System.Drawing.Color c in Helper.Palette[key])
            {
                Rectangle rectangle = new Rectangle() { Fill = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)), Width = 25, Height = 25, Stroke = Brushes.Black, StrokeThickness = 1 };
                rectangle.MouseDown += OnSelectColor;
                palettePanel.Children.Add(rectangle);
            }
        }

        private void OnSelectColor(object sender, EventArgs args)
        {
            Rectangle rect = sender as Rectangle;

            foreach (Rectangle c in palettePanel.Children)
            {
                c.StrokeThickness = 1;
            }

            rect.StrokeThickness = 3;
            selectedColor = (rect.Fill as SolidColorBrush).Color;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var mouseCoords = e.GetPosition(MainCanvas);
            coordsLabel.Text = String.Format("{0},{1}", (int)mouseCoords.X, (int)mouseCoords.Y);

            //Process shift key
            if (Keyboard.IsKeyDown(Key.LeftShift) && e.LeftButton == MouseButtonState.Pressed && tool != Tools.MOVE)
            {
                Point curPosition = e.GetPosition((UIElement)MainCanvas.Parent);
                curPosition.X -= translate.X;
                curPosition.Y -= translate.Y;

                int dx = (int)((mouseDownPoint.X - curPosition.X) / scale.ScaleX);
                int dy = (int)((mouseDownPoint.Y - curPosition.Y) / scale.ScaleY);

                if (!shiftDirectionDeterm)
                {
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        shiftDirHor = true;
                        shiftDirectionDeterm = true;
                    }
                    else if (Math.Abs(dx) < Math.Abs(dy))
                    {
                        shiftDirHor = false;
                        shiftDirectionDeterm = true;
                    }
                    //unable to determine of cursor has yet to move more than a pixel
                }

                if (shiftDirectionDeterm)
                {
                    if (shiftDirHor)
                    {
                        Canvas.SetLeft(brushHighlight, Math.Floor(mouseCoords.X - 2));
                        Canvas.SetLeft(pixelHighlight, Math.Floor(mouseCoords.X));
                    }
                    else
                    {
                        Canvas.SetTop(brushHighlight, Math.Floor(mouseCoords.Y - 2));
                        Canvas.SetTop(pixelHighlight, Math.Floor(mouseCoords.Y));
                    }
                }

            }
            else
            {
                shiftDirectionDeterm = false;

                Canvas.SetLeft(brushHighlight, Math.Floor(mouseCoords.X - 2));
                Canvas.SetTop(brushHighlight, Math.Floor(mouseCoords.Y - 2));

                Canvas.SetLeft(pixelHighlight, Math.Floor(mouseCoords.X));
                Canvas.SetTop(pixelHighlight, Math.Floor(mouseCoords.Y));
            }

            if (!MainCanvas.IsMouseCaptured)
                return;

            //Move or draw
            int drawx = (int)Canvas.GetLeft(pixelHighlight);
            int drawy = (int)Canvas.GetTop(pixelHighlight);

            if (tool == Tools.MOVE || e.LeftButton != MouseButtonState.Pressed)
            {
                Point curPosition = e.GetPosition((UIElement)MainCanvas.Parent);
                translate.X = curPosition.X - mouseDownPoint.X;
                translate.Y = curPosition.Y - mouseDownPoint.Y;
            }
            else if (tool == Tools.PENCIL)
            {
                if (bitmap.GetPixel(drawx, drawy) != selectedColor)
                {
                    DataExchange.CreateManualPixel(new GUIPixel(drawx, drawy, System.Drawing.Color.FromArgb(selectedColor.A, selectedColor.R, selectedColor.G, selectedColor.B)));
                }
            }
            else if (tool == Tools.BRUSH)
            {
                for (int i = drawx - 2; i <= drawx + 2; i++)
                {
                    for (int j = drawy - 2; j <= drawy + 2; j++)
                    {
                        if (bitmap.GetPixel(i, j) != selectedColor)
                        {
                            DataExchange.CreateManualPixel(new GUIPixel(i, j, System.Drawing.Color.FromArgb(selectedColor.A, selectedColor.R, selectedColor.G, selectedColor.B)));
                        }
                    }
                }
            }
            else if (tool == Tools.HISTORYBRUSH)
            {
                for (int i = drawx - 2; i <= drawx + 2; i++)
                {
                    for (int j = drawy - 2; j <= drawy + 2; j++)
                    {
                        System.Drawing.Color revertPixel = revertState.GetPixel(i, j);
                        Color curPixel = bitmap.GetPixel(i, j);
                        if (curPixel.R == 204 && curPixel.G == 204 && curPixel.B == 204)
                            continue;
                        if (curPixel.R != revertPixel.R || curPixel.G != revertPixel.G || curPixel.B != revertPixel.B)
                        {
                            DataExchange.CreateManualPixel(new GUIPixel(i, j, System.Drawing.Color.FromArgb(revertPixel.A, revertPixel.R, revertPixel.G, revertPixel.B)));
                        }
                    }
                }
            }

        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.Focus(MainCanvas);

            mouseDownPoint = e.GetPosition((UIElement)MainCanvas.Parent);
            mouseDownPoint.X -= translate.X;
            mouseDownPoint.Y -= translate.Y;
            MainCanvas.CaptureMouse();
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainCanvas.ReleaseMouseCapture();
        }

        private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point curPosition = e.GetPosition((UIElement)MainCanvas.Parent);
            curPosition.X -= translate.X;
            curPosition.Y -= translate.Y;

            scalingPower += e.Delta / 120;

            if (scalingPower < -6)
                scalingPower = -6;
            else if (scalingPower > 15)
                scalingPower = 15;

            double oldScale = scale.ScaleX;
            double newScale = Math.Pow(1.4, scalingPower);

            scale.ScaleX = newScale;
            scale.ScaleY = newScale;

            /*
            double factor = Math.Pow(1.5, e.Delta / 120);
            scale.ScaleX *= factor;
            scale.ScaleY *= factor;*/

            if (scalingPower < 0)
                RenderOptions.SetBitmapScalingMode(MainImage, BitmapScalingMode.HighQuality);
            else
                RenderOptions.SetBitmapScalingMode(MainImage, BitmapScalingMode.NearestNeighbor);

            double factor = newScale / oldScale;
            Point newPos = new Point(curPosition.X * factor, curPosition.Y * factor);

            translate.X -= newPos.X - curPosition.X;
            translate.Y -= newPos.Y - curPosition.Y;

            mouseDownPoint.X *= factor;
            mouseDownPoint.Y *= factor;
        }

        private void MainCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            if (tool == Tools.BRUSH || tool == Tools.HISTORYBRUSH)
                ShowBrush(true);
            else
                ShowBrush(false);
        }

        private void MainCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            HideBrush();
        }

        private void OnResetPosition(object sender, MouseButtonEventArgs e)
        {
            translate.X = 0;
            translate.Y = 0;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            scalingPower = 0;
        }

        private void MainCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.P)
            {
                var mousePos = Mouse.GetPosition(MainCanvas);
                DataExchange.PushTaskPosition((int)mousePos.X, (int)mousePos.Y);
            }
        }

        private void OnToolClick(object sender, MouseButtonEventArgs e)
        {
            moveTool.Background = Brushes.Black;
            brushTool.Background = Brushes.Black;
            drawTool.Background = Brushes.Black;
            historyBrushTool.Background = Brushes.Black;

            (sender as Border).Background = Brushes.Gray;

            if (sender == moveTool)
            {
                tool = Tools.MOVE;
                ShowBrush(false);
            }
            else if (sender == brushTool)
            {
                tool = Tools.BRUSH;
                ShowBrush(true);
            }
            else if (sender == drawTool)
            {
                tool = Tools.PENCIL;
                ShowBrush(false);
            }
            else if (sender == historyBrushTool)
            {
                tool = Tools.HISTORYBRUSH;
                ShowBrush(true);
            }
        }

        private void ShowBrush(bool big)
        {
            if (big)
            {
                pixelHighlight.Visibility = Visibility.Hidden;
                brushHighlight.Visibility = Visibility.Visible;
            }
            else
            {
                pixelHighlight.Visibility = Visibility.Visible;
                brushHighlight.Visibility = Visibility.Hidden;
            }
        }

        private void HideBrush()
        {
            pixelHighlight.Visibility = Visibility.Hidden;
            brushHighlight.Visibility = Visibility.Hidden;
        }

        private void OnSaveMouseUp(object sender, MouseButtonEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            var time = DateTime.Now;
            saveFileDialog.FileName = String.Format("pixeplace.io-{0}-{1}-{2:00}-{3:00}-{4:00}-{5:00}-{6:00}.png", canvasId, time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);
            saveFileDialog.Filter = "Image|*.png";
            if (saveFileDialog.ShowDialog() != true)
                return;

            using (var file = File.Create(saveFileDialog.FileName))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);
            }
        }

        private void OnSaveRevertStateClick(object sender, MouseButtonEventArgs e)
        {
            revertState?.Dispose();

            using (var stream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
                revertState = new System.Drawing.Bitmap(stream);
            }
        }

        private void OnLoadRevertStateClick(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog loadFileDialog = new OpenFileDialog();
            loadFileDialog.Filter = "Image|*.png";
            if (loadFileDialog.ShowDialog() != true)
                return;

            System.Drawing.Bitmap loaded = new System.Drawing.Bitmap(loadFileDialog.FileName);
            if (loaded.Width != bitmap.Width || loaded.Height != bitmap.Height)
            {
                MessageBox.Show("Selected image is not a valid canvas state");
                return;
            }

            revertState?.Dispose();
            revertState = loaded;
        }
    }
}
