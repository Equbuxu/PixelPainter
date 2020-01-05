using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class UserSession
    {
        private Thread drawThread;

        int packetSize = 28;
        int packetDelay = 2500;
        long lastPacketTime = -1;
        long lastRecieveTime = -1;

        AutoResetEvent packetSent = new AutoResetEvent(false);

        bool stalled = false;
        int stallDelay = 0;
        int lastPacketSize = 0;

        private LinkedList<IdPixel> queue = new LinkedList<IdPixel>();
        private SocketIO server;

        public UserSession(SocketIO server)
        {
            this.server = server;

            drawThread = new Thread(DrawLoop);
            drawThread.Name = "User draw loop";
            drawThread.IsBackground = true;
            drawThread.Start();
        }

        public void Stall(int ms)
        {
            stallDelay = ms;
            stalled = true;
        }

        public List<IdPixel> Close()
        {
            drawThread.Abort();
            lock (queue)
            {
                return queue.Select((a) => a).ToList(); //copy for thread safety
            }
        }

        public void SetPlacementSpeed(double speed)
        {
            if (speed < 1 || speed > 15)
                return;
            double delay = 28 / speed * 1000;
            packetDelay = (int)delay;
        }

        public void Enqueue(IdPixel pixel)
        {
            lock (queue)
            {
                queue.AddLast(pixel);
            }
        }

        public void EnqueueFront(IdPixel pixel)
        {
            lock (queue)
            {
                queue.AddFirst(pixel);
            }
        }

        public void ClearQueue()
        {
            lock (queue)
            {
                queue.Clear();
            }
        }

        public int QueueLength()
        {
            lock (queue)
            {
                return queue.Count;
            }
        }

        private void DrawLoop()
        {
            while (true)
            {
                Draw();
            }
        }

        private void SendCallback(Task t)
        {
            packetSent.Set();
        }

        private bool Draw()
        {
            int queueCount;
            lock (queue) queueCount = queue.Count;
            if (queueCount == 0 || server.Status != Status.OPEN)
            {
                //Thread.Sleep(packetDelay);
                Thread.Sleep(100);
                return false;
            }

            List<IdPixel> toPlace = new List<IdPixel>(packetSize);
            toPlace.AddRange(SelectOneColor());

            long time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long estimatedRecieveTime = (lastPacketTime + lastRecieveTime) / 2;

            int delay = (int)(packetDelay * (lastPacketSize / (double)packetSize));
            if (time - estimatedRecieveTime < delay)
            {
                int sleeptime = (int)(delay - (time - estimatedRecieveTime));
                if (sleeptime < 80)
                    sleeptime = 80;
                Logger.Info("{2} is sleeping for {0} ms after sending {1} pixels", sleeptime, lastPacketSize, server.Username);
                Thread.Sleep(sleeptime);
            }

            if (stalled)
            {
                Thread.Sleep(stallDelay);
                stalled = false;
            }
            lastPacketTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (server.Status == Status.OPEN)
            {
                lastPacketSize = toPlace.Count;
                server.SendPixels(toPlace, SendCallback);
                packetSent.WaitOne();
                lastRecieveTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }
            else
                Logger.Warning("Failed to send pixels");
            return true;
        }

        private List<IdPixel> SelectOneColor()
        {
            List<IdPixel> toPlace = new List<IdPixel>(packetSize);

            lock (queue)
            {
                List<LinkedListNode<IdPixel>> toRemove = new List<LinkedListNode<IdPixel>>(packetSize);

                int firstColor = -1;
                for (LinkedListNode<IdPixel> node = queue.First; node != null; node = node.Next)
                {
                    IdPixel pixel = node.Value;
                    if (firstColor == -1)
                        firstColor = pixel.Color;
                    else if (firstColor != pixel.Color)
                        continue;
                    if (toPlace.Count >= packetSize)
                        break;
                    toPlace.Add(pixel);
                    toRemove.Add(node);
                }

                foreach (var node in toRemove)
                {
                    queue.Remove(node);
                }
            }
            return toPlace;
        }
    }

}