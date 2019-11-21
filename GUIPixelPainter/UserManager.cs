using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace GUIPixelPainter
{
    public class UserStatusData : EventArgs
    {
        public UserStatusData(Guid userId, Status userStatus)
        {
            UserId = userId;
            UserStatus = userStatus;
        }
        public Guid UserId { get; }
        public Status UserStatus { get; }
    }

    public class TaskEnableStateData : EventArgs
    {
        public TaskEnableStateData(Guid taskId, bool enabled)
        {
            TaskId = taskId;
            Enabled = enabled;
        }
        public Guid TaskId { get; }
        public bool Enabled { get; }
    }

    /// <summary>
    /// Manages connections
    /// Converts and distributes tasks among users
    /// </summary>
    public class UserManager
    {
        class Connection
        {
            public Connection(SocketIO client, UserSession session, Guid id)
            {
                Client = client;
                Session = session;
                Id = id;
            }
            public SocketIO Client { get; }
            public UserSession Session { get; }
            public Guid Id { get; }
        }

        private List<Connection> users = new List<Connection>();
        private Thread loopThread;

        private List<Tuple<string, EventArgs>> eventsToProcess = new List<Tuple<string, EventArgs>>();
        private List<Tuple<string, EventArgs>> eventsToDispatch = new List<Tuple<string, EventArgs>>();
        private HashSet<Guid> activeUsers = new HashSet<Guid>();

        private AutoResetEvent resetEvent = new AutoResetEvent(false);
        private List<GUIEvent> latestGUIEvents = new List<GUIEvent>();

        private Guid currentActiveUser = Guid.Empty;
        private Bitmap borders;
        private Bitmap canvas;
        private int curCanvas = -1;
        private DateTime lastUserConnectionTime = DateTime.MinValue;

        private Dictionary<int, Dictionary<int, System.Drawing.Color>> palette;
        private Dictionary<int, Dictionary<System.Drawing.Color, int>> invPalette;

        UsefulDataRepresentation guiData;
        PlacementBehaviour placementBehaviour;

        public UserManager(UsefulDataRepresentation representation, Dictionary<int, Dictionary<int, System.Drawing.Color>> palette)
        {
            guiData = representation;

            this.palette = palette;
            this.invPalette = palette.Select((a) => new KeyValuePair<int, Dictionary<System.Drawing.Color, int>>(a.Key, a.Value.Select((b) => new KeyValuePair<Color, int>(b.Value, b.Key)).ToDictionary((b) => b.Key, (b) => b.Value))).ToDictionary((a) => a.Key, (a) => a.Value);

            loopThread = new Thread(Loop);
            loopThread.Name = "UserManager loop";
            loopThread.IsBackground = true;
            loopThread.Start();
        }

        public List<Tuple<string, EventArgs>> Update(List<GUIEvent> events)
        {
            lock (latestGUIEvents)
                latestGUIEvents.AddRange(events.Select((a) => a).ToList());
            resetEvent.Set();
            lock (eventsToDispatch)
            {
                var copy = eventsToDispatch.Select((a) => a).ToList();
                eventsToDispatch.Clear();
                return copy;
            }
        }

        private void Loop()
        {
            while (true)
            {
                resetEvent.WaitOne();

                if (curCanvas != guiData.CanvasId)
                    ChangeCanvas();

                if (curCanvas != -1) //HACK things should fire in a different order in a way that will allow curCanvas to be set before first resetEvent.
                {
                    if ((placementBehaviour == null || placementBehaviour.GetMode() != guiData.PlacementMode) && curCanvas != -1) //HACK curCanvas check is kinda hacky (it is there to avoid crah on start)
                        ChangePlacementBehaviour();

                    UpdatePlacementSpeed();
                    RefreshConnections();
                    ManageQueues();
                    ProcessGUIEvents();
                    ProcessEvents();
                    ManageUnknownUsernames();
                }
            }
        }

        private void UpdatePlacementSpeed()
        {
            foreach (Connection conn in users)
            {
                conn.Session.SetPlacementSpeed(guiData.PlacementSpeed);
            }
        }

        private void ManageUnknownUsernames()
        {
            foreach (Connection conn in users)
            {
                if (!conn.Client.Premium || conn.Client.GetStatus() != Status.OPEN)
                    continue;

                foreach (int unknownUsername in guiData.UnknownUsernames)
                    conn.Client.SendNicknameRequest(unknownUsername);

                break;
            }
        }

        private void ManageQueues()
        {
            var total = users.Where((a) => a.Client.GetStatus() == Status.OPEN).ToList();
            var completedTasksForEachUser = new List<List<UsefulTask>>();

            foreach (Connection conn in total)
            {
                var completedTasks = new List<UsefulTask>();
                completedTasksForEachUser.Add(completedTasks);

                if (conn.Client.GetStatus() != Status.OPEN)
                    continue;
                if (conn.Session.QueueLength() > 80)
                    continue;

                if (placementBehaviour == null)
                    break;

                var queue = placementBehaviour.BuildQueue(total.IndexOf(conn), total.Count, completedTasks);
                if (queue.Count == 0)
                    break;

                foreach (IdPixel pixel in queue)
                {
                    conn.Session.Enqueue(pixel);
                }
            }

            //Disable tasks which were completed by every user
            if (completedTasksForEachUser.Count > 0)
            {
                var commonCompletedTasks = completedTasksForEachUser.First();

                for (int i = 1; i < completedTasksForEachUser.Count; i++)
                    commonCompletedTasks = commonCompletedTasks.Intersect(completedTasksForEachUser[i]).ToList();

                foreach (UsefulTask task in commonCompletedTasks)
                {
                    CreateEventToDispatch("manager.taskenable", new TaskEnableStateData(task.Id, false));
                }
            }

            //Clear queues on bot disable
            if (guiData.Tasks.Count == 0 && guiData.ManualPixels.Count == 0)
            {
                foreach (Connection conn in total)
                {
                    conn.Session.ClearQueue();
                }
            }
        }

        private void RefreshConnections()
        {
            //Remove old users
            for (int i = users.Count - 1; i >= 0; i--)
            {
                if (guiData.Users.Where((a) => a.Id == users[i].Id).Count() == 0 || users[i].Client.GetStatus() == Status.CLOSEDDISCONNECT)
                {
                    Console.WriteLine("user connection removed");
                    users[i].Session.ClearQueue();
                    users[i].Session.Close();
                    if (users[i].Client.GetStatus() != Status.CLOSEDERROR && users[i].Client.GetStatus() != Status.CLOSEDDISCONNECT)
                        users[i].Client.Disconnect();
                    CreateEventToDispatch("manager.status", new UserStatusData(users[i].Id, Status.NOTOPEN));
                    users.RemoveAt(i);
                }
            }

            //Add new users
            foreach (UsefulUser user in guiData.Users)
            {
                if (users.Find((a) => a.Id == user.Id) == null)
                {
                    Console.WriteLine("user connection created, there was {0} users in total", users.Count);
                    SocketIO server = CreateSocketIO(user);
                    server.StartConnect();
                    UserSession newUser = new UserSession(server);
                    Connection connection = new Connection(server, newUser, user.Id);
                    users.Add(connection);
                    CreateEventToDispatch("manager.status", new UserStatusData(connection.Id, Status.OPEN));
                    lastUserConnectionTime = DateTime.UtcNow;
                }
            }

            //send status for disconnected users
            foreach (Connection user in users)
            {
                var status = user.Client.GetStatus();
                if (status == Status.CLOSEDDISCONNECT || status == Status.CLOSEDERROR)
                {
                    CreateEventToDispatch("manager.status", new UserStatusData(user.Id, status));
                }
            }
        }

        private void ProcessGUIEvents()
        {
            lock (latestGUIEvents)
            {
                for (int i = latestGUIEvents.Count - 1; i >= 0; i--)
                {
                    var @event = latestGUIEvents[i];
                    if (@event is ChatMessageGUIEvent)
                    {
                        ChatMessageGUIEvent message = @event as ChatMessageGUIEvent;
                        var user = users.Where((a) => a.Id == message.UserId);
                        if (user.Count() == 0)
                            continue;
                        user.First().Client.SendChatMessage(message.Message, message.Color, message.Chat);
                        latestGUIEvents.RemoveAt(i);
                    }
                }
            }
        }

        private void ProcessEvents()
        {
            lock (eventsToProcess)
            {
                foreach (Tuple<string, EventArgs> eventTuple in eventsToProcess)
                {
                    if (eventTuple.Item1 == "pixels")
                    {
                        PixelPacket pixel = eventTuple.Item2 as PixelPacket;
                        Color actualColor;
                        if (palette.ContainsKey(curCanvas))
                            actualColor = palette[curCanvas][pixel.color];
                        else
                            actualColor = palette[7][pixel.color];
                        if (pixel.x < 0 || pixel.x >= canvas.Width || pixel.y < 0 || pixel.y >= canvas.Height)
                            continue;
                        var border = borders.GetPixel(pixel.x, pixel.y);
                        if (!(border.R == 204 && border.G == 204 && border.B == 204))
                            canvas.SetPixel(pixel.x, pixel.y, actualColor);
                    }
                }
                eventsToProcess.Clear();
            }
        }

        private void ChangeCanvas()
        {
            curCanvas = guiData.CanvasId;
            Console.WriteLine("loading canvas {0} in UserManager", curCanvas);

            //Disconnect everyone
            for (int i = users.Count - 1; i >= 0; i--)
            {
                Console.WriteLine("user connection removed");
                users[i].Session.ClearQueue();
                users[i].Session.Close();
                if (users[i].Client.GetStatus() != Status.CLOSEDERROR && users[i].Client.GetStatus() != Status.CLOSEDDISCONNECT)
                {
                    users[i].Client.Disconnect();
                    CreateEventToDispatch("manager.status", new UserStatusData(users[i].Id, Status.NOTOPEN));
                }
                users.RemoveAt(i);
            }

            try
            {
                //Load canvas
                canvas?.Dispose();
                System.Net.WebRequest request = System.Net.WebRequest.Create("https://pixelplace.io/canvas/" + guiData.CanvasId.ToString() + ".png");
                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream responseStream = response.GetResponseStream();
                canvas = new Bitmap(responseStream);
                responseStream.Dispose();
            }
            catch (System.Net.WebException)
            {
                Console.WriteLine("invalid canvas in usermanager");
                return;
            }

            try
            {
                //Load locked pixels
                borders?.Dispose();
                System.Net.WebRequest request2 = System.Net.WebRequest.Create("https://pixelplace.io/canvas/" + guiData.CanvasId.ToString() + "p.png");
                System.Net.WebResponse response2 = request2.GetResponse();
                System.IO.Stream responseStream2 = response2.GetResponseStream();
                borders = new Bitmap(responseStream2);
                responseStream2.Dispose();
            }
            catch (System.Net.WebException)
            {
                borders = new Bitmap(canvas.Width, canvas.Height);
            }
            eventsToProcess.Clear();
            ChangePlacementBehaviour();

            Console.WriteLine("UserManager canvas loading finished");
        }

        private void ChangePlacementBehaviour()
        {
            switch (guiData.PlacementMode)
            {
                case PlacementMode.TOPDOWN:
                    placementBehaviour = new TopDownPlacementBehaviour(guiData, canvas, borders, invPalette.ContainsKey(curCanvas) ? invPalette[curCanvas] : invPalette[7]);
                    break;
                case PlacementMode.DENOISE:
                    placementBehaviour = new DenoisePlacementBehaviour(guiData, canvas, borders, invPalette.ContainsKey(curCanvas) ? invPalette[curCanvas] : invPalette[7]);
                    break;
            }
            foreach (Connection conn in users)
            {
                conn.Session.ClearQueue();
            }

        }

        private SocketIO CreateSocketIO(UsefulUser user)
        {
            SocketIO socket = new SocketIO(user.AuthKey, user.AuthToken, user.PhpSessId, guiData.CanvasId, user.Proxy);
            socket.OnEvent += (a, b) => { OnSocketEvent(a, b, user.Id); };
            return socket;
        }

        private void CreateEventToDispatch(string type, EventArgs data)
        {
            lock (eventsToDispatch)
            {
                eventsToDispatch.Add(new Tuple<string, EventArgs>(type, data));
            }
        }

        private void OnSocketEvent(string type, EventArgs args, Guid user)
        {

            if (type == "throw.error" && (args as ErrorPacket).id == 11)
            {
                users.Where((a) => a.Id == user).FirstOrDefault()?.Session.Stall(1000);
                Console.WriteLine("stall");
            }

            if (type == "tokens")
            {
                (args as TokenPacket).id = user;
            }

            if (user != currentActiveUser && type != "tokens" && type != "nickname")
            {
                var curactive = users.Where((a) => a.Id == currentActiveUser).ToList();
                if (curactive.Count == 0 || curactive[0].Client.GetStatus() != Status.OPEN)
                {
                    currentActiveUser = user;
                    Console.WriteLine("Listening to {0}", user);
                }
                else
                {
                    return;
                }
            }

            if (type == "pixels" && placementBehaviour.GetMode() == PlacementMode.TOPDOWN)
            {
                PixelPacket px = args as PixelPacket;
                (placementBehaviour as TopDownPlacementBehaviour).ResetResendDelay(px.x, px.y);
            }

            lock (eventsToProcess)
            {
                eventsToProcess.Add(new Tuple<string, EventArgs>(type, args));
            }
            lock (eventsToDispatch)
            {
                eventsToDispatch.Add(new Tuple<string, EventArgs>(type, args));
            }
        }
    }
}
