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
        class Pixel
        {
            public Color c;
            public int x;
            public int y;
        }

        private ScaleTransform scale = new ScaleTransform();
        private TranslateTransform translate = new TranslateTransform();
        private Point mouseDownPoint = new Point();
        private bool drawing = false;
        private Color selectedColor = Color.FromArgb(0, 1, 2, 3);

        private Dictionary<int, Border> nameLabels = new Dictionary<int, Border>();
        private Dictionary<int, long> userPlaceTime = new Dictionary<int, long>();
        private long lastUpdateTime = -1;

        private WriteableBitmap bitmap;
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

            OnMoveToolClick(null, null);
        }

        public void ReloadCanvas(int id)
        {
            canvasId = id;
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
        }

        public void OverlayTasks(List<GUITask> tasks)
        {
            MainCanvas.Children.RemoveRange(2, MainCanvas.Children.Count - 2);
            foreach (GUITask task in tasks)
            {
                Image image = new Image();
                image.Source = Helper.Convert(task.Dithering == true ? task.DitheredConvertedBitmap : task.ConvertedBitmap);
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                image.Opacity = 0.5;
                Canvas.SetLeft(image, task.X);
                Canvas.SetTop(image, task.Y);
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
            Canvas.SetLeft(pixelHighlight, Math.Floor(mouseCoords.X));
            Canvas.SetTop(pixelHighlight, Math.Floor(mouseCoords.Y));

            if (!MainCanvas.IsMouseCaptured)
                return;

            if (!drawing || e.LeftButton != MouseButtonState.Pressed)
            {
                Point curPosition = e.GetPosition((UIElement)MainCanvas.Parent);
                translate.X = curPosition.X - mouseDownPoint.X;
                translate.Y = curPosition.Y - mouseDownPoint.Y;
            }
            else
            {
                if (bitmap.GetPixel((int)mouseCoords.X, (int)mouseCoords.Y) != selectedColor)
                {
                    DataExchange.CreateManualPixel(new GUIPixel((int)mouseCoords.X, (int)mouseCoords.Y, System.Drawing.Color.FromArgb(selectedColor.A, selectedColor.R, selectedColor.G, selectedColor.B)));
                }
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
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


            double factor = Math.Pow(1.5, e.Delta / 120);
            scale.ScaleX *= factor;
            scale.ScaleY *= factor;
            Point newPos = new Point(curPosition.X * factor, curPosition.Y * factor);

            translate.X -= newPos.X - curPosition.X;
            translate.Y -= newPos.Y - curPosition.Y;

            mouseDownPoint.X *= factor;
            mouseDownPoint.Y *= factor;
        }

        private void MainCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            pixelHighlight.Visibility = Visibility.Visible;
        }

        private void MainCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            pixelHighlight.Visibility = Visibility.Hidden;
        }

        private void OnMoveToolClick(object sender, MouseButtonEventArgs e)
        {
            moveTool.Background = Brushes.DarkGray;
            drawTool.Background = Brushes.Black;
            drawing = false;
        }

        private void OnDrawToolClick(object sender, MouseButtonEventArgs e)
        {
            moveTool.Background = Brushes.Black;
            drawTool.Background = Brushes.DarkGray;
            drawing = true;
        }

        private void OnResetPosition(object sender, MouseButtonEventArgs e)
        {
            translate.X = 0;
            translate.Y = 0;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
    }
}
