using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public enum PlacementMode
    {
        TOPDOWN,
        DENOISE
    }

    public class GUIUser
    {
        public GUIUser(Guid internalId, string name, string proxy, string authKey, string authToken, string phpSessId, Status status, bool enabled)
        {
            InternalId = internalId;
            Name = name;
            Proxy = proxy;
            AuthKey = authKey;
            AuthToken = authToken;
            Status = status;
            PhpSessId = phpSessId;
            Enabled = enabled;
        }
        public Guid InternalId { get; }
        public string Name { get; }
        public string Proxy { get; }
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
        public double windowWidth { get; private set; }
        public double windowHeight { get; private set; }

        public Guid SelectedTaskId { get; private set; }

        public int CanvasId { get; private set; }

        public PlacementMode PlacementMode { get; private set; }

        public double PlacementSpeed { get; private set; }

        public IReadOnlyCollection<int> UnknownUsernames { get; private set; }

        private Dictionary<int, List<GUITask>> guiTasks = new Dictionary<int, List<GUITask>>();
        //public IReadOnlyDictionary<int, IReadOnlyList<GUITask>> GUITasks => (IReadOnlyDictionary<int, IReadOnlyList<GUITask>>)guiTasks;
        public IReadOnlyDictionary<int, IReadOnlyList<GUITask>> GUITasks => guiTasks.ToDictionary((a) => a.Key, (a) => (IReadOnlyList<GUITask>)a.Value.AsReadOnly());

        private List<GUIPixel> manualTask = new List<GUIPixel>();
        public IReadOnlyCollection<GUIPixel> ManualTask
        {
            get { return manualTask.AsReadOnly(); }
        }

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
        private GUIHelper guiHelper;

        public GUIUpdater Updater { get; set; }
        public UsefulDataRepresentation UsefulData { get; set; }

        public GUIDataExchange(GUI.UserPanel userPanel, GUI.TaskPanel taskPanel, GUI.PixelCanvas pixelCanvas, GUI.BotWindow botWindow, GUIHelper helper)
        {
            this.userPanel = userPanel;
            this.taskPanel = taskPanel;
            this.pixelCanvas = pixelCanvas;
            this.botWindow = botWindow;
            this.guiHelper = helper;
        }

        public void UpdateOverlay()
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
            windowWidth = botWindow.GetWindowWidth();
            windowHeight = botWindow.GetWindowHeight();
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
            BotEnabled = botWindow.IsBotEnabled();
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
                manualTask.Clear();
                UsefulData.UpdateCanvasId();
            }

            UpdateOverlay();

            UsefulData.UpdatePlacementMode();
            UsefulData.UpdateTasks();
            Updater.Update();
        }

        //"Create" methonds. They are like push methods, but push data back instead of forward

        /// <summary>
        /// return true on success, false on failure 
        /// </summary>
        public bool CreateChatMessage(string text, int color, int chat)
        {
            var user = userPanel.GetSelectedUserGuidIfAny();
            if (user == Guid.Empty)
                return false;
            ChatMessageGUIEvent message = new ChatMessageGUIEvent(text, user, color, chat);
            Updater.AddEvent(message);
            Updater.Update();
            return true;
        }

        public void CreateManualPixel(GUIPixel pixel)
        {
            for (int i = 0; i < manualTask.Count; i++)
            {
                GUIPixel cur = manualTask[i];
                if (cur.X == pixel.X && cur.Y == pixel.Y)
                {
                    if (cur.Color.R == pixel.Color.R && cur.Color.G == pixel.Color.G && cur.Color.B == pixel.Color.B)
                        return;
                    else
                    {
                        manualTask[i] = pixel;
                        UsefulData.UpdateManualTask();
                        return;
                    }
                }
            }
            manualTask.Add(pixel);
            UsefulData.UpdateManualTask();
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

                manualTask.RemoveAll((a) => a.X == x && a.Y == y && a.Color == color);
                UsefulData.UpdateManualTask(); //TODO might not be necessary, could be removed as an optimisation
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
        /*
        public void PushSetting(string name, string value)
        {
            var property = typeof(GUIDataExchange).GetProperty(name);
            if (property == null)
                return;

            property.SetValue(this, Convert.ChangeType(value, property.PropertyType));
        }*/

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