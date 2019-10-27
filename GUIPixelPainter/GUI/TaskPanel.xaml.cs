using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    /// Interaction logic for TaskPanel.xaml
    /// </summary>
    public partial class TaskPanel : UserControl
    {
        private class Task
        {
            public Guid internalId;
            public bool imagesConverted;

            public string name;
            public int x;
            public int y;
            public Bitmap originalImage;
            public Bitmap convertedImage;
            public Bitmap ditheredImage;
            public bool dithering;
            public bool loop;
            public bool isEnabled;
        }

        private Dictionary<int, List<Task>> tasks = new Dictionary<int, List<Task>>();
        private bool ignoreEvents = false;
        private Thread converterThread = null;
        private Guid convertingTask = new Guid();
        private int canvasId = 7;

        public GUIHelper Helper { get; set; }
        public GUIDataExchange DataExchange { get; set; }

        public TaskPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// returns an immutable copy of tasks
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, List<GUITask>> GetTasks()
        {
            Dictionary<int, List<GUITask>> converted = new Dictionary<int, List<GUITask>>();

            foreach (KeyValuePair<int, List<Task>> canvas in tasks)
            {
                converted.Add(canvas.Key, new List<GUITask>());
                foreach (Task task in canvas.Value)
                {
                    if (task.imagesConverted)
                    {
                        GUITask newTask = new GUITask(task.internalId, task.name, task.isEnabled, task.x, task.y, task.dithering, task.loop, task.originalImage, task.convertedImage, task.ditheredImage);
                        converted[canvas.Key].Add(newTask);
                    }
                }
            }
            return converted;
        }

        public int GetSelectedTaskIndex()
        {
            return taskList.SelectedIndex;
        }

        public void SetCanvasId(int id)
        {
            canvasId = id;
            if (!tasks.ContainsKey(canvasId))
                tasks.Add(canvasId, new List<Task>());

            UpdateTaskList();
        }

        public void SetTaskEnabledState(Guid id, bool state)
        {
            if (!TaskExists(id))
                return;
            Task task = GetTask(id);
            if (task.isEnabled == state)
                return;
            task.isEnabled = state;
            UpdateTaskList();
            DataExchange.UpdateTasksFromGUI();
        }

        public void AddTask(GUITask task, int taskCanvasId)
        {
            Task newTask = new Task()
            {
                internalId = task.InternalId,
                imagesConverted = true,
                name = task.Name,
                x = task.X,
                y = task.Y,
                originalImage = task.OriginalBitmap,
                convertedImage = task.ConvertedBitmap,
                ditheredImage = task.DitheredConvertedBitmap,
                dithering = task.Dithering,
                loop = task.KeepRepairing,
                isEnabled = task.Enabled
            };
            if (!tasks.ContainsKey(taskCanvasId))
                tasks.Add(taskCanvasId, new List<Task>());

            tasks[taskCanvasId].Add(newTask);
            DataExchange.UpdateTasksFromGUI();
            UpdateTaskList();
        }

        public void MoveCurrentTask(int xpos, int ypos)
        {
            if (taskList.SelectedIndex == -1)
                return;
            var selectedTask = GetSelectedTask();
            x.Text = (xpos).ToString();
            y.Text = (ypos).ToString();
            selectedTask.x = xpos;
            selectedTask.y = ypos;
            DataExchange.UpdateTasksFromGUI();
        }

        private Task GetSelectedTask()
        {
            return GetTask(Guid.Parse(((taskList.SelectedItem as StackPanel).Children[1] as TextBlock).Text));
        }

        private Task GetTask(Guid id)
        {
            return tasks[canvasId].Find((a) => a.internalId == id);
        }

        private bool TaskExists(Guid id)
        {
            return tasks[canvasId].Find((a) => a.internalId == id) != null;
        }

        private void OnNewTaskClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            string name = "New task";
            Task task = new Task() { name = name, internalId = Guid.NewGuid() };
            tasks[canvasId].Add(task);
            DataExchange.UpdateTasksFromGUI();
            UpdateTaskList();
        }

        private void UpdateTaskList()
        {
            //remove old tasks
            for (int i = taskList.Items.Count - 1; i >= 0; i--)
            {
                StackPanel item = taskList.Items[i] as StackPanel;
                Guid id = Guid.Parse((item.Children[1] as TextBlock).Text);
                if (!TaskExists(id))
                    taskList.Items.Remove(item);
            }
            //add new tasks
            foreach (Task task in tasks[canvasId])
            {
                bool exists = false;
                foreach (StackPanel item in taskList.Items)
                {
                    if (Guid.Parse((item.Children[1] as TextBlock).Text) == task.internalId)
                    {
                        exists = true;
                        var nameLabel = (item.Children[0] as Label);
                        nameLabel.Content = task.name; //update name of existing task in case it changed
                        if (task.isEnabled)
                            nameLabel.Foreground = System.Windows.Media.Brushes.Green;
                        else
                            nameLabel.Foreground = System.Windows.Media.Brushes.Black;
                        break;
                    }
                }
                if (exists)
                    continue;

                Label label = new Label() { Content = task.name };
                if (task.isEnabled)
                    label.Foreground = System.Windows.Media.Brushes.Green;
                else
                    label.Foreground = System.Windows.Media.Brushes.Black;
                TextBlock id = new TextBlock() { Text = task.internalId.ToString(), Visibility = Visibility.Collapsed };
                StackPanel panel = new StackPanel();
                panel.Children.Add(label);
                panel.Children.Add(id);
                taskList.Items.Add(panel);
            }

            UpdateTaskSettingsPanel();
        }

        private void UpdateTaskSettingsPanel()
        {
            ignoreEvents = true;
            disableAll.IsEnabled = tasks[canvasId].Where((a) => a.isEnabled == true).Count() != 0;
            if (taskList.SelectedItem == null)
            {
                taskName.Text = string.Empty;
                taskEnabled.IsChecked = false;
                x.Text = string.Empty;
                y.Text = string.Empty;
                dithering.IsChecked = false;
                loop.IsChecked = false;

                taskName.IsEnabled = false;
                taskEnabled.IsEnabled = false;
                selectImage.IsEnabled = false;
                x.IsEnabled = false;
                y.IsEnabled = false;
                dithering.IsEnabled = false;
                loop.IsEnabled = false;
                deleteThisTask.IsEnabled = false;
                preview.Visibility = Visibility.Collapsed;

                ignoreEvents = false;
                return;
            }
            Task selectedTask = GetSelectedTask();

            taskName.Text = selectedTask.name;
            taskEnabled.IsChecked = selectedTask.isEnabled;
            x.Text = selectedTask.x.ToString();
            y.Text = selectedTask.y.ToString();
            dithering.IsChecked = selectedTask.dithering;
            loop.IsChecked = selectedTask.loop;

            taskName.IsEnabled = true;
            taskEnabled.IsEnabled = true;
            selectImage.IsEnabled = true;
            x.IsEnabled = true;
            y.IsEnabled = true;
            dithering.IsEnabled = true;
            loop.IsEnabled = true;
            deleteThisTask.IsEnabled = true;
            preview.Visibility = Visibility.Visible;

            ignoreEvents = false;
            ShowPreview();

        }

        private void OnDeleteTaskClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            Task task = GetSelectedTask();
            tasks[canvasId].Remove(task);
            DataExchange.UpdateTasksFromGUI();
            UpdateTaskList();
        }

        private void OnTaskSelection(object sender, SelectionChangedEventArgs e)
        {
            if (ignoreEvents)
                return;
            DataExchange.UpdateSelectedTaskFromGUI();
            if (taskList.Items.Count > 0)
                UpdateTaskSettingsPanel();
        }

        private void OnBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            if (taskList.SelectedIndex == -1)
                return;

            Task selectedTask = GetSelectedTask();

            int xPos, yPos;
            if (int.TryParse(x.Text, out xPos))
                selectedTask.x = xPos;
            if (int.TryParse(y.Text, out yPos))
                selectedTask.y = yPos;
            selectedTask.dithering = dithering.IsChecked == true;
            selectedTask.loop = loop.IsChecked == true;
            selectedTask.name = taskName.Text;

            DataExchange.UpdateTasksFromGUI();

            if (sender.Equals(taskName))
                UpdateTaskList();
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            taskName.SelectAll();
            x.SelectAll();
            y.SelectAll();
        }

        private void OnXDownClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(x.Text, out int xPos))
            {
                ChangePosX(xPos - 1, sender);
            }
        }

        private void OnXUpClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(x.Text, out int xPos))
            {
                ChangePosX(xPos + 1, sender);
            }
        }

        private void OnYDownClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(y.Text, out int yPos))
            {
                ChangePosY(yPos - 1, sender);
            }
        }

        private void OnYUpClick(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(y.Text, out int yPos))
            {
                ChangePosY(yPos + 1, sender);
            }
        }

        private void ChangePosX(int xPos, object sender)
        {
            Task selectedTask = GetSelectedTask();
            x.Text = (xPos).ToString();
            selectedTask.x = xPos;
            DataExchange.UpdateTasksFromGUI();
            if (sender.Equals(taskName))
                UpdateTaskList();
        }

        private void ChangePosY(int yPos, object sender)
        {
            Task selectedTask = GetSelectedTask();
            y.Text = (yPos).ToString();
            selectedTask.y = yPos;
            DataExchange.UpdateTasksFromGUI();
            if (sender.Equals(taskName))
                UpdateTaskList();
        }

        private void ConvertImages()
        {
            if (converterThread != null)
            {
                if (TaskExists(convertingTask))
                {
                    converterThread.Join(); //will freeze UI but oh well
                }
                else
                {
                    converterThread.Abort();
                }
            }

            Task task = GetSelectedTask();
            Bitmap image = task.originalImage;

            ImageConverter noDither = new ImageConverter(image, Helper.Palette[7], false);
            ImageConverter dither = new ImageConverter(image, Helper.Palette[7], true);

            convertingTask = task.internalId;
            converterThread = new Thread(() =>
            {
                noDither.Convert();
                dither.Convert();

                Dispatcher.InvokeAsync(() =>
                {
                    if (!TaskExists(task.internalId))
                        return;

                    task.convertedImage = noDither.Convert();
                    task.ditheredImage = dither.Convert();
                    task.imagesConverted = true;
                    DataExchange.UpdateTasksFromGUI();
                    ShowPreview();
                });
            });
            converterThread.Name = "TaskPanel conversion";
            converterThread.IsBackground = true;
            converterThread.Start();
        }

        private void ShowPreview()
        {
            Debug.Assert(Helper != null);

            Task task = GetSelectedTask();
            Bitmap image = task.originalImage;
            if (image == null)
            {
                preview.Source = null;
                return;
            }

            if (task.imagesConverted)
            {
                if (dithering.IsChecked == true)
                    preview.Source = Helper.Convert(task.ditheredImage);
                else
                    preview.Source = Helper.Convert(task.convertedImage);
            }

            preview.UpdateLayout();

            if (preview.Source.Width > preview.ActualWidth)
                RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
            else
                RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);
        }

        private void OnSelectImageClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Images |*.png;*.jpg;*.jpeg";
            if (dialog.ShowDialog() != true)
                return;

            using (Bitmap image = new Bitmap(dialog.FileName))
            {
                Bitmap copy = new Bitmap(image); //Converts to correct format and unlock the file
                GetSelectedTask().originalImage = copy;
            }

            ConvertImages();
        }

        private void OnEnableTask(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            if (taskList.SelectedItem == null)
                return;
            GetSelectedTask().isEnabled = true;

            UpdateTaskList();

            DataExchange.UpdateTasksFromGUI();
        }

        private void OnDisableTask(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            if (taskList.SelectedItem == null)
                return;
            GetSelectedTask().isEnabled = false;
            UpdateTaskList();

            DataExchange.UpdateTasksFromGUI();


        }

        private void OnDitheringChange(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            GetSelectedTask().dithering = dithering.IsChecked == true;
            ShowPreview();
            DataExchange.UpdateTasksFromGUI();
        }

        private void OnKeepRepairingChange(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            GetSelectedTask().loop = loop.IsChecked == true;
            DataExchange.UpdateTasksFromGUI();
        }

        private void OnDisableAllClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            tasks[canvasId].ForEach((a) => a.isEnabled = false);
            UpdateTaskList();

            DataExchange.UpdateTasksFromGUI();
        }
    }
}
