using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class UserSession
    {
        private Thread drawThread;

        int packetSize = 28;
        int packetDelay = 2600;
        long lastPacketTime = -1;

        bool stalled = false;
        int stallDelay = 0;

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
                if (Draw())
                {
                    //AdjustDelay();
                }
            }
        }

        private bool Draw()
        {
            int queueCount;
            lock (queue) queueCount = queue.Count;
            if (queueCount == 0 || server.GetStatus() != Status.OPEN)
            {
                Thread.Sleep(packetDelay);
                return false;
            }

            List<IdPixel> toPlace = new List<IdPixel>(packetSize);
            toPlace.AddRange(SelectOneColor());
            //toPlace.AddRange(SelectOneColor());
            //if (toPlace.Count > packetSize)
            //    toPlace.RemoveRange(packetSize, toPlace.Count - packetSize);

            long time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (time - lastPacketTime < packetDelay)
            {
                Thread.Sleep((int)(packetDelay - (time - lastPacketTime)));
            }

            if (stalled)
            {
                Thread.Sleep(stallDelay);
                stalled = false;
            }
            lastPacketTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (server.GetStatus() == Status.OPEN)
                server.SendPixels(toPlace);
            else
                Console.WriteLine("Failed to send pixels");
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