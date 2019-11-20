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
        public UsefulUser(Guid id, string authKey, string authToken, string phpSessId, string proxy)
        {
            Id = id;
            AuthKey = authKey;
            AuthToken = authToken;
            PhpSessId = phpSessId;
            Proxy = proxy;
        }
        public Guid Id { get; }
        public string AuthToken { get; }
        public string PhpSessId { get; }
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

        public override bool Equals(object obj)
        {
            if (obj is UsefulPixel)
            {
                UsefulPixel other = obj as UsefulPixel;
                return X == other.X && Y == other.Y;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return (X << 16) | Y;
        }
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

        private Dictionary<UsefulPixel, UsefulPixel> manualPixels = new Dictionary<UsefulPixel, UsefulPixel>();
        public List<UsefulPixel> ManualPixels
        {
            get
            {
                lock (manualPixels) { return manualPixels.Select((a) => a.Key).ToList(); }
            }
        }

        private List<int> unknownUsernames = new List<int>();
        public List<int> UnknownUsernames
        {
            get
            {
                lock (manualPixels) { return unknownUsernames.Select((a) => a).ToList(); }
            }
        }

        public int CanvasId { get; private set; } = -1;

        public double PlacementSpeed { get; private set; } = 11.2;

        public PlacementMode PlacementMode { get; private set; }

        private GUIDataExchange dataExchange;

        public UsefulDataRepresentation(GUIDataExchange dataExchange)
        {
            this.dataExchange = dataExchange;
        }

        public void UpdateUnknownUsernames()
        {
            lock (unknownUsernames)
            {
                unknownUsernames.Clear();
                foreach (int userId in dataExchange.UnknownUsernames)
                {
                    unknownUsernames.Add(userId);
                }
            }
        }

        public void UpdateCanvasId()
        {
            CanvasId = dataExchange.CanvasId;
            tasks.Clear();
            manualPixels.Clear();
        }

        public void UpdateManualPixel(GUIPixel pixel)
        {
            if (!dataExchange.BotEnabled)
                return;
            lock (manualPixels)
            {
                UsefulPixel newPixel = new UsefulPixel(pixel.X, pixel.Y, pixel.Color);
                manualPixels.Remove(newPixel);
                manualPixels.Add(newPixel, newPixel);
                /*manualPixels.Clear();
                foreach (GUIPixel pixel in dataExchange.ManualTask)
                {
                    UsefulPixel newPixel = new UsefulPixel(pixel.X, pixel.Y, pixel.Color);
                    manualPixels.Add(newPixel);
                }*/
            }
        }

        public void RemoveManualPixel(GUIPixel pixel)
        {
            lock (manualPixels)
            {
                UsefulPixel newPixel = new UsefulPixel(pixel.X, pixel.Y, pixel.Color);
                manualPixels.Remove(newPixel);
            }
        }

        public void UpdatePlacementMode()
        {
            PlacementMode = dataExchange.PlacementMode;
        }

        public void UpdatePlacementSpeed()
        {
            PlacementSpeed = dataExchange.PlacementSpeed;
        }

        public void UpdateTasks()
        {
            lock (tasks)
            {
                tasks.Clear();
                if (!dataExchange.BotEnabled)
                    return;
                if (!dataExchange.GUITasks.ContainsKey(CanvasId))
                {
                    tasks.Clear();
                    return;
                }
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
                    if (!user.Enabled || string.IsNullOrEmpty(user.PhpSessId) || string.IsNullOrEmpty(user.AuthKey) || string.IsNullOrEmpty(user.AuthToken))
                        continue;
                    UsefulUser newUser = new UsefulUser(user.InternalId, user.AuthKey, user.AuthToken, user.PhpSessId, user.Proxy);
                    users.Add(newUser);
                }
            }
        }
    }
}