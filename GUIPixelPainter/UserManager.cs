using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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
        private List<GUIEvent> latestGUIEvents = null;

        private Guid currentActiveUser = Guid.Empty;
        private Bitmap borders;
        private Bitmap canvas;
        private int curCanvas = -1;
        private DateTime lastUserConnectionTime = DateTime.MinValue;

        private Dictionary<int, Dictionary<int, System.Drawing.Color>> palette;
        private Dictionary<int, Dictionary<System.Drawing.Color, int>> invPalette;

        UsefulDataRepresentation guiData;

        public UserManager(UsefulDataRepresentation representation, Dictionary<int, Dictionary<int, System.Drawing.Color>> palette)
        {
            guiData = representation;

            this.palette = palette;
            this.invPalette = palette.Select((a) => new KeyValuePair<int, Dictionary<System.Drawing.Color, int>>(a.Key, a.Value.Select((b) => new KeyValuePair<Color, int>(b.Value, b.Key)).ToDictionary((b) => b.Key, (b) => b.Value))).ToDictionary((a) => a.Key, (a) => a.Value);

            loopThread = new Thread(Loop);
            loopThread.IsBackground = true;
            loopThread.Start();
        }

        public List<Tuple<string, EventArgs>> Update(List<GUIEvent> events)
        {
            latestGUIEvents = events.Select((a) => a).ToList();
            resetEvent.Set();
            Console.WriteLine("update");
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

                //TODO remake queues on update (or maybe not?)
                RefreshConnections();
                ManageQueues();
                ProcessGUIEvents();
                ProcessEvents();
                ChangeCanvas();
            }
        }

        private void ManageQueues()
        {
            //TODO write managequeues
            var total = users.Where((a) => a.Client.GetStatus() == Status.OPEN).ToList();
            foreach (Connection conn in total)
            {
                if (conn.Client.GetStatus() != Status.OPEN)
                    continue;
                if (conn.Session.QueueLength() > 50)
                    continue;

                var queue = BuildQueue(total.IndexOf(conn), total.Count);
                foreach (IdPixel pixel in queue)
                {
                    conn.Session.Enqueue(pixel);
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
                if ((DateTime.UtcNow - lastUserConnectionTime).TotalMilliseconds < 2000)
                    break;
                if (users.Find((a) => a.Id == user.Id) == null)
                {
                    Console.WriteLine("user connection created");
                    SocketIO server = CreateSocketIO(user);
                    server.Connect();
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
            foreach (GUIEvent @event in latestGUIEvents)
            {
                if (@event is ChatMessageGUIEvent)
                {
                    ChatMessageGUIEvent message = @event as ChatMessageGUIEvent;
                    var user = users.Where((a) => a.Id == message.UserId);
                    if (user.Count() == 0)
                        continue;
                    user.First().Client.SendChatMessage(message.Message, message.Color);
                }
            }
        }

        private void ProcessEvents()
        {
            //TODO write event processing
            lock (eventsToProcess)
            {
                foreach (Tuple<string, EventArgs> eventTuple in eventsToProcess)
                {
                    if (eventTuple.Item1 == "pixels")
                    {
                        PixelPacket pixel = eventTuple.Item2 as PixelPacket;
                        Color actualColor = palette[curCanvas][pixel.color]; //TODO this crashes
                        var border = borders.GetPixel(pixel.x, pixel.y);
                        if (!(border.R == 204 && border.G == 204 && border.B == 204))
                            canvas.SetPixel(pixel.x, pixel.y, actualColor);
                    }
                }
                eventsToProcess.Clear();
            }
        }

        private List<IdPixel> BuildQueue(int userNum, int totalUser)
        {

            List<IdPixel> queue = new List<IdPixel>();
            foreach (UsefulTask task in guiData.Tasks)
            {
                bool completed = true;
                for (int j = 0; j < task.Image.Height; j++)
                {
                    for (int i = userNum; i < task.Image.Width; i += totalUser)
                    {
                        var canvasPixel = canvas.GetPixel(task.X + i, task.Y + j);
                        if (canvasPixel.R == 204 && canvasPixel.G == 204 && canvasPixel.B == 204)
                            continue;
                        var reqPixel = task.Image.GetPixel(i, j);
                        if (canvasPixel == reqPixel)
                            continue;
                        if (!invPalette[curCanvas].ContainsKey(reqPixel))
                            continue;
                        completed = false;
                        IdPixel pixel = new IdPixel(invPalette[curCanvas][reqPixel], task.X + i, task.Y + j);
                        queue.Add(pixel);
                        if (queue.Count > 99)
                            goto end;
                    }
                }
                if (completed && !task.KeepRepairing)
                    CreateEventToDispatch("manager.taskenable", new TaskEnableStateData(task.Id, false));
            }

        end:
            //Bitmap test = new Bitmap(task.image.Width, task.image.Height);
            //foreach (Pixel pixel in queue)
            //{
            //    test.SetPixel(pixel.position.X - task.x, pixel.position.Y - task.y, pixel.color);
            //}
            //test.Save("test.png");

            return queue;
        }

        private void ChangeCanvas()
        {
            if (curCanvas == guiData.CanvasId)
                return;
            curCanvas = guiData.CanvasId;

            //Disconnect everyone
            for (int i = users.Count - 1; i >= 0; i--)
            {
                Console.WriteLine("user connection removed");
                users[i].Session.ClearQueue();
                users[i].Session.Close();
                if (users[i].Client.GetStatus() != Status.CLOSEDERROR && users[i].Client.GetStatus() != Status.CLOSEDDISCONNECT)
                    users[i].Client.Disconnect();
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
        }

        private SocketIO CreateSocketIO(UsefulUser user)
        {
            SocketIO socket = new SocketIO(user.AuthKey, user.AuthToken, guiData.CanvasId);
            socket.OnEvent += (a, b) => { OnSocketEvent(a, b, user.Id); };
            return socket;
        }

        private void CreateEventToDispatch(string type, EventArgs data)
        {
            eventsToDispatch.Add(new Tuple<string, EventArgs>(type, data));
        }

        private void OnSocketEvent(string type, EventArgs args, Guid user)
        {
            if (user != currentActiveUser)
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

            lock (eventsToProcess)
            {
                eventsToProcess.Add(new Tuple<string, EventArgs>(type, args));
            }
            lock (eventsToDispatch)
            {
                eventsToDispatch.Add(new Tuple<string, EventArgs>(type, args));
            }
        }

        private void FilterEventsToDispatch(string type, EventArgs args, Guid user)
        {
            //TODO filter events here instead of sending all of them in OnSocketEvent
        }
    }
}
