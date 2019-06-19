using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class GUIUser
    {
        public GUIUser(Guid internalId, string name, string proxy, string authKey, string authToken, Status status, bool enabled)
        {
            InternalId = internalId;
            Name = name;
            Proxy = proxy;
            AuthKey = authKey;
            AuthToken = authToken;
            Status = status;
            Enabled = enabled;
        }
        public Guid InternalId { get; }
        public string Name { get; }
        public string Proxy { get; }
        public string AuthKey { get; }
        public string AuthToken { get; }
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

    public class GUIUserSpeed
    {
        public GUIUserSpeed(string username, string speed, string id)
        {
            Username = username;
            Speed = speed;
            Id = id;
        }
        public string Username { get; }
        public string Speed { get; }
        public string Id { get; }
    }

    public class PaletteColor
    {
        PaletteColor(Color color, bool enabled, bool selected)
        {
            Color = color;
            Enabled = enabled;
            Selected = selected;
        }
        public Color Color { get; }
        public bool Enabled { get; }
        public bool Selected { get; }
    }

    /// <summary>
    /// Contains and manipulates all data that is useful to have outside individual window/control classes. Updated in real time
    /// </summary>
    public class GUIDataExchange
    {
        //GUI element states
        public bool BotEnabled { get; private set; }

        public bool SuperimposeTasks { get; private set; }

        public int CanvasId { get; private set; }

        private Dictionary<int, List<GUITask>> guiTasks = new Dictionary<int, List<GUITask>>();
        //public IReadOnlyDictionary<int, IReadOnlyList<GUITask>> GUITasks => (IReadOnlyDictionary<int, IReadOnlyList<GUITask>>)guiTasks;
        public IReadOnlyDictionary<int, IReadOnlyList<GUITask>> GUITasks => guiTasks.ToDictionary((a) => a.Key, (a) => (IReadOnlyList<GUITask>)a.Value.AsReadOnly());

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

        public GUIUpdater Updater { get; set; }
        public UsefulDataRepresentation UsefulData { get; set; }

        public GUIDataExchange(GUI.UserPanel userPanel, GUI.TaskPanel taskPanel, GUI.PixelCanvas pixelCanvas, GUI.BotWindow botWindow)
        {
            this.userPanel = userPanel;
            this.taskPanel = taskPanel;
            this.pixelCanvas = pixelCanvas;
            this.botWindow = botWindow;
        }

        //"Update from GUI" methods. Grab data from controls and forward to other controls if necessary
        public void UpdateUsersFromGUI()
        {
            guiUsers = userPanel.GetUsers();

            UsefulData.UpdateUsers();
            Updater.Update();
        }

        public void UpdateTasksFromGUI()
        {
            SuperimposeTasks = botWindow.IsSuperimpositionEnabled();
            guiTasks = taskPanel.GetTasks();
            UsefulData.UpdateTasks();
            Updater.Update();
            if (SuperimposeTasks && guiTasks.ContainsKey(CanvasId))
                pixelCanvas.OverlayTasks(guiTasks[CanvasId].Where((a) => a.Enabled).ToList());
        }

        public void UpdateGeneralSettingsFromGUI()
        {
            BotEnabled = botWindow.IsBotEnabled();
            SuperimposeTasks = botWindow.IsSuperimpositionEnabled();

            int canvasId = botWindow.GetCanvasId();
            if (canvasId != CanvasId)
            {
                CanvasId = canvasId;

                taskPanel.SetCanvasId(canvasId);
                pixelCanvas.ReloadCanvas(canvasId);

                UsefulData.UpdateCanvasId();
            }

            if (SuperimposeTasks && guiTasks.ContainsKey(canvasId))
                pixelCanvas.OverlayTasks(guiTasks[CanvasId].Where((a) => a.Enabled).ToList());
            else
                pixelCanvas.OverlayTasks(new List<GUITask>());

            UsefulData.UpdateTasks();
            Updater.Update();
        }

        //"Create" methonds. They are like push methods, but push data back instead of forward
        /// <summary>
        /// return true on success, false on failure 
        /// </summary>
        public bool CreateChatMessage(string text, int color)
        {
            var user = userPanel.GetSelectedUserGuidIfAny();
            if (user == Guid.Empty)
                return false;
            ChatMessageGUIEvent message = new ChatMessageGUIEvent(text, user, color);
            Updater.AddEvent(message);
            Updater.Update();
            return true;
        }

        public void CreateUpdate()
        {
            Updater.Update();
        }

        //"Push" methods. Transfer individual events forward to controls. They don't impact data stored here.
        public void PushChatMessage(string message, System.Windows.Media.Color c)
        {
            botWindow.AddChatText(message, c);
        }

        public void PushPixel(int x, int y, Color color, int boardId, int userId, bool myOwnPixel)
        {
            if (boardId == CanvasId)
            {
                var cColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                pixelCanvas.SetPixel(x, y, cColor, userId);
                botWindow.UpdateSpeed(x, y, cColor, userId, myOwnPixel);
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
    }
}

