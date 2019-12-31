using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace GUIPixelPainter
{
    public enum PlacementMode
    {
        TOPDOWN,
        DENOISE
    }

    public class GUIUser
    {
        public GUIUser(Guid internalId, string name, string authKey, string authToken, string phpSessId, Status status, bool enabled)
        {
            InternalId = internalId;
            Name = name;
            AuthKey = authKey;
            AuthToken = authToken;
            Status = status;
            PhpSessId = phpSessId;
            Enabled = enabled;
        }
        public Guid InternalId { get; }
        public string Name { get; }
        public string AuthKey { get; }
        public string AuthToken { get; }
        public string PhpSessId { get; }
        [JsonIgnore]
        public Status Status { get; }
        public bool Enabled { get; }
    }

    public class GUITask
    {
        public GUITask(Guid internalId, string name, bool enabled, int x, int y, bool dithering, bool keepRepairing, Bitmap originalBitmap, Bitmap convertedBitmap, Bitmap ditheredConvertedBitmap)
        {
            InternalId = internalId;
            Name = name;
            Enabled = enabled;
            X = x;
            Y = y;
            Dithering = dithering;
            KeepRepairing = keepRepairing;
            OriginalBitmap = originalBitmap;
            ConvertedBitmap = convertedBitmap;
            DitheredConvertedBitmap = ditheredConvertedBitmap;
        }
        public Guid InternalId { get; }
        public string Name { get; }
        public bool Enabled { get; }
        public int X { get; }
        public int Y { get; }
        public bool Dithering { get; }
        public bool KeepRepairing { get; }
        [JsonIgnore]
        public Bitmap OriginalBitmap { get; }
        [JsonIgnore]
        public Bitmap ConvertedBitmap { get; }
        [JsonIgnore]
        public Bitmap DitheredConvertedBitmap { get; }
    }

    public class GUIPixel
    {
        public GUIPixel(int x, int y, Color color)
        {
            X = x;
            Y = y;
            Color = color;
        }
        public int X { get; }
        public int Y { get; }
        public Color Color { get; }

        public override bool Equals(object obj)
        {
            if (obj is GUIPixel)
            {
                GUIPixel other = obj as GUIPixel;
                return X == other.X && Y == other.Y;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            int result = (X << 16) | Y;
            return result;
        }
    }

    /// <summary>
    /// Contains and manipulates all data that is useful to have outside individual window/control classes. Updated in real time
    /// </summary>
    public class GUIDataExchange
    {
        //GUI element states
        public bool BotEnabled { get; private set; }

        public bool TrackingEnabled { get; private set; }

        public bool OverlayTasks { get; private set; }
        public bool OverlayAllTasks { get; private set; }
        public bool OverlaySelectedTask { get; private set; }
        public double OverlayTranslucency { get; private set; }

        public System.Windows.WindowState windowState { get; private set; }
        public double WindowWidth { get; private set; }
        public double WindowHeight { get; private set; }

        public Guid SelectedTaskId { get; private set; }

        public int CanvasId { get; private set; }

        public PlacementMode PlacementMode { get; private set; }

        public double PlacementSpeed { get; private set; }

        public IReadOnlyCollection<int> UnknownUsernames { get; private set; }

        private Dictionary<int, List<GUITask>> guiTasks = new Dictionary<int, List<GUITask>>();
        public IReadOnlyDictionary<int, IReadOnlyList<GUITask>> GUITasks => guiTasks.ToDictionary((a) => a.Key, (a) => (IReadOnlyList<GUITask>)a.Value.AsReadOnly());

        public Color SelectedColor { get; private set; }

        private OrderedSet<GUIPixel> manualTask = new OrderedSet<GUIPixel>();

        public bool MoveToolSelected { get; private set; }

        public bool DrawToolSelected { get; private set; }

        public bool BucketToolSelected { get; private set; }

        private List<GUIUser> guiUsers = new List<GUIUser>();
        public IReadOnlyCollection<GUIUser> GUIUsers => guiUsers;

        //Other
        private GUI.UserPanel userPanel;
        private GUI.TaskPanel taskPanel;
        private GUI.PixelCanvas pixelCanvas;
        private GUI.BotWindow botWindow;
        private GUI.TimelapsePanel timelapsePanel;
        private GUIHelper guiHelper;

        private GUI.TimelapsePanelViewModel timelapsePanelViewModel;
        private GUI.TimelapsePanelModel timelapsePanelModel;

        public GUIUpdater Updater { get; set; }
        public UsefulDataRepresentation UsefulData { get; set; }

        public GUIDataExchange(GUI.UserPanel userPanel, GUI.TaskPanel taskPanel, GUI.PixelCanvas pixelCanvas, GUI.BotWindow botWindow, GUI.TimelapsePanel timelapsePanel, GUIHelper helper)
        {
            this.userPanel = userPanel;
            this.taskPanel = taskPanel;
            this.pixelCanvas = pixelCanvas;
            this.botWindow = botWindow;
            this.timelapsePanel = timelapsePanel;
            this.guiHelper = helper;

            InitModels();
        }

        private void InitModels()
        {
            timelapsePanelModel = new GUI.TimelapsePanelModel(this);
            timelapsePanelViewModel = new GUI.TimelapsePanelViewModel(timelapsePanelModel);
            timelapsePanel.SetViewModel(timelapsePanelViewModel);
        }

        private void ClearManualTask()
        {
            manualTask.Clear();
            UsefulData.ClearManualPixels();
        }

        public void SaveBitmapToStream(System.IO.Stream stream)
        {
            pixelCanvas.SaveBitmapToStream(stream);
        }

        public Size GetCanvasSize()
        {
            return pixelCanvas.GetCanvasSize();
        }

        private void UpdateOverlay()
        {
            if (!guiTasks.ContainsKey(CanvasId))
            {
                pixelCanvas.OverlayTasks(new List<GUITask>());
                return;
            }

            if (OverlayAllTasks)
                pixelCanvas.OverlayTasks(guiTasks[CanvasId].ToList());
            else if (OverlayTasks)
                pixelCanvas.OverlayTasks(guiTasks[CanvasId].Where((a) => a.Enabled).ToList());
            else if (OverlaySelectedTask)
            {
                var task = guiTasks[CanvasId].Where((a) => a.InternalId == SelectedTaskId);
                if (task.Count() == 0)
                    pixelCanvas.OverlayTasks(new List<GUITask>());
                else
                    pixelCanvas.OverlayTasks(new List<GUITask>() { task.First() });
            }
            else
                pixelCanvas.OverlayTasks(new List<GUITask>());
        }

        //"Update from GUI" methods. Grab data from controls and forward to other controls if necessary
        public void UpdateSelectedColorFromGUI()
        {
            SelectedColor = pixelCanvas.GetSelectedColor();
        }

        public void UpdateUsersFromGUI()
        {
            guiUsers = userPanel.GetUsers();

            UsefulData.UpdateUsers();
            Updater.Update();
        }

        public void UpdateUnknownUsernamesFromGUI()
        {
            UnknownUsernames = guiHelper.GetUnknownUsernames();
            UsefulData.UpdateUnknownUsernames();
        }

        public void UpdateWindowStateFromUI()
        {
            WindowWidth = botWindow.GetWindowWidth();
            WindowHeight = botWindow.GetWindowHeight();
            windowState = botWindow.GetWindowState();
        }

        public void UpdateSelectedTaskFromGUI()
        {
            SelectedTaskId = taskPanel.GetSelectedTaskId();
            UpdateOverlay();
        }

        public void UpdateTranslucencyFromGUI()
        {
            OverlayTranslucency = botWindow.GetOverlayTranslucency();
            pixelCanvas.SetTaskOverlayTranslucency(OverlayTranslucency);
        }

        public void UpdateTasksFromGUI()
        {
            OverlayTasks = botWindow.IsOverlayEnabled();
            guiTasks = taskPanel.GetTasks();
            UsefulData.UpdateTasks();
            Updater.Update();
            UpdateOverlay();
        }

        public void UpdateGeneralSettingsFromGUI()
        {
            bool botEnabled = botWindow.IsBotEnabled();
            if (botEnabled == false && BotEnabled == true)
            {
                CreateClearManualTask();
            }
            BotEnabled = botEnabled;

            OverlayTasks = botWindow.IsOverlayEnabled();
            OverlayAllTasks = botWindow.IsOverlayAllEnabled();
            OverlaySelectedTask = botWindow.IsOverlaySelectedEnabled();
            PlacementMode = botWindow.GetPlacementMode();
            TrackingEnabled = botWindow.IsTrackingEnabled();

            pixelCanvas.SetNameLabelDisplay(TrackingEnabled);

            double placementSpeed = botWindow.GetPlacementSpeed();
            if (placementSpeed != PlacementSpeed)
            {
                PlacementSpeed = placementSpeed;
                UsefulData.UpdatePlacementSpeed();
            }

            int canvasId = botWindow.GetCanvasId();
            if (canvasId != CanvasId)
            {
                CanvasId = canvasId;

                botWindow.ClearChat();
                taskPanel.SetCanvasId(canvasId);
                pixelCanvas.ReloadCanvas(canvasId);
                ClearManualTask();
                UsefulData.UpdateCanvasId();

                timelapsePanelViewModel.StopRecording();
            }

            UpdateOverlay();

            UsefulData.UpdatePlacementMode();
            UsefulData.UpdateTasks();
            Updater.Update();
        }

        //"Create" methonds. They are like push methods, but push data back instead of forward

        public void CreateClearManualTask()
        {
            ClearManualTask();
            Updater.AddEvent(new ClearQueuesGUIEvent());
        }

        /// <summary>
        /// return true on success, false on failure 
        /// </summary>
        public bool CreateChatMessage(string text, int chat)
        {
            var user = userPanel.GetSelectedUserGuidIfAny();
            if (user == Guid.Empty)
                return false;
            ChatMessageGUIEvent message = new ChatMessageGUIEvent(text, user, SelectedColor, chat);
            Updater.AddEvent(message);
            Updater.Update();
            return true;
        }

        public void CreateManualPixel(GUIPixel pixel)
        {
            if (!BotEnabled)
                return;
            pixel = new GUIPixel(pixel.X, pixel.Y, Color.FromArgb(pixel.Color.A, pixel.Color.R, pixel.Color.G, pixel.Color.B));
            if (manualTask.Contains(pixel))
            {
                GUIPixel cur = manualTask.GetStoredCopy(pixel);
                if (cur.Color.R == pixel.Color.R && cur.Color.G == pixel.Color.G && cur.Color.B == pixel.Color.B)
                    return;
                else
                {
                    manualTask.Remove(pixel);
                    manualTask.Add(pixel);
                    UsefulData.UpdateManualPixel(pixel);
                    return;
                }
            }

            manualTask.Add(pixel);
            UsefulData.UpdateManualPixel(pixel);
            Updater.Update();//TODO temporary!!
        }

        public void CreateUpdate()
        {
            Updater.Update();
        }

        //"Push" methods. Transfer individual events forward to controls. They don't impact data stored here.
        public void PushTokens(string PHPSESSID, string authToken, Guid id)
        {
            userPanel.SetUserTokens(id, PHPSESSID, authToken);
        }

        public void PushNewUsername(int id, string name)
        {
            guiHelper.AddUsername(id, name);
        }

        public void PushChatMessage(string message, bool isLocal, System.Windows.Media.Color c)
        {
            botWindow.AddChatText(message, isLocal, c);
        }

        public void PushPixel(int x, int y, Color color, int boardId, int userId, bool myOwnPixel)
        {
            if (boardId == CanvasId)
            {
                var cColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                pixelCanvas.SetPixel(x, y, cColor, userId);
                botWindow.UpdateSpeed(x, y, cColor, userId, myOwnPixel);

                GUIPixel removed = new GUIPixel(x, y, color);
                if (manualTask.Remove(removed))
                    UsefulData.RemoveManualPixel(removed);
            }
        }

        public void PushUserStatus(UserStatusData data)
        {
            userPanel.SetUserStatus(data.UserId, data.UserStatus);
        }

        public void PushTaskEnabledState(TaskEnableStateData data)
        {
            taskPanel.SetTaskEnabledState(data.TaskId, data.Enabled);
        }

        public void PushNewTask(GUITask task, int taskCanvasId)
        {
            taskPanel.AddTask(task, taskCanvasId);
        }

        public void PushNewUser(GUIUser user)
        {
            userPanel.AddNewUser(user);
        }

        public void PushWindowState(double width, double height, System.Windows.WindowState state)
        {
            botWindow.SetWindowState(width, height, state);
        }

        public void PushSettings(bool overlayTasks, bool overlayAllTasks, bool overlaySelectedTask, double overlayTranslucency, int canvasId, PlacementMode placementMode, double placementSpeed)
        {
            botWindow.SetSettings(overlayTasks, overlayAllTasks, overlaySelectedTask, overlayTranslucency, canvasId, placementMode, placementSpeed);
        }

        public void PushTaskPosition(int x, int y)
        {
            taskPanel.MoveCurrentTask(x, y);
        }

        public void PushLoadingState(bool loading)
        {
            botWindow.SetLoadingState(loading);
        }
    }
}