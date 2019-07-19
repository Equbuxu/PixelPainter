using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class UsefulTask
    {
        public UsefulTask(Guid id, Bitmap image, int x, int y, bool keepRepairing)
        {
            Id = id;
            Image = image;
            X = x;
            Y = y;
            KeepRepairing = keepRepairing;
        }
        public Guid Id { get; }
        public Bitmap Image { get; }
        public int X { get; }
        public int Y { get; }
        public bool KeepRepairing { get; }
    }

    public class UsefulUser
    {
        public UsefulUser(Guid id, string authKey, string authToken, string proxy)
        {
            Id = id;
            AuthKey = authKey;
            AuthToken = authToken;
            Proxy = proxy;
        }
        public Guid Id { get; }
        public string AuthToken { get; }
        public string AuthKey { get; }
        public string Proxy { get; }
    }

    public class UsefulPixel
    {
        public UsefulPixel(int x, int y, Color color)
        {
            X = x;
            Y = y;
            Color = color;
        }
        public int X { get; }
        public int Y { get; }
        public Color Color { get; }
    }

    public class UsefulDataRepresentation
    {
        private List<UsefulTask> tasks = new List<UsefulTask>();
        public List<UsefulTask> Tasks
        {
            get
            {
                lock (tasks) { return tasks.Select((a) => a).ToList(); }
            }
        }

        private List<UsefulUser> users = new List<UsefulUser>();
        public List<UsefulUser> Users
        {
            get
            {
                lock (users) { return users.Select((a) => a).ToList(); }
            }
        }

        private List<UsefulPixel> manualPixels = new List<UsefulPixel>();
        public List<UsefulPixel> ManualPixels
        {
            get
            {
                lock (manualPixels) { return manualPixels.Select((a) => a).ToList(); }
            }
        }

        public int CanvasId { get; private set; } = -1;

        public PlacementMode PlacementMode { get; private set; }

        private GUIDataExchange dataExchange;

        public UsefulDataRepresentation(GUIDataExchange dataExchange)
        {
            this.dataExchange = dataExchange;
        }

        public void UpdateCanvasId()
        {
            CanvasId = dataExchange.CanvasId;
        }

        public void UpdateManualTask()
        {
            lock (manualPixels)
            {
                manualPixels.Clear();
                if (!dataExchange.BotEnabled)
                    return;
                foreach (GUIPixel pixel in dataExchange.ManualTask)
                {
                    UsefulPixel newPixel = new UsefulPixel(pixel.X, pixel.Y, pixel.Color);
                    manualPixels.Add(newPixel);
                }
            }
        }

        public void UpdatePlacementMode()
        {
            PlacementMode = dataExchange.PlacementMode;
        }

        public void UpdateTasks()
        {
            lock (tasks)
            {
                tasks.Clear();
                if (!dataExchange.BotEnabled)
                    return;
                foreach (GUITask task in dataExchange.GUITasks[CanvasId])
                {
                    if (!task.Enabled)
                        continue;
                    UsefulTask newTask = new UsefulTask(task.InternalId, task.Dithering ? task.DitheredConvertedBitmap.Clone() as Bitmap : task.ConvertedBitmap.Clone() as Bitmap, task.X, task.Y, task.KeepRepairing);
                    tasks.Add(newTask);
                }
            }
        }

        public void UpdateUsers()
        {
            lock (users)
            {
                users.Clear();
                foreach (GUIUser user in dataExchange.GUIUsers)
                {
                    if (!user.Enabled)
                        continue;
                    UsefulUser newUser = new UsefulUser(user.InternalId, user.AuthKey, user.AuthToken, user.Proxy);
                    users.Add(newUser);
                }
            }
        }
    }
}