using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class TopDownPlacementBehaviour : PlacementBehaviour
    {
        private UsefulDataRepresentation guiData;
        private Bitmap canvas;
        private Bitmap borders;
        private int[,] lastUpdateIterCount;
        private const int maxQueueSize = 50;
        private const int pixelResendDelay = 40;
        int iterCount = pixelResendDelay;
        private Dictionary<System.Drawing.Color, int> curCanvasInvPalette;

        public TopDownPlacementBehaviour(UsefulDataRepresentation guiData, Bitmap canvas, Bitmap borders, Dictionary<System.Drawing.Color, int> curCanvasInvPalette)
        {
            this.guiData = guiData;
            this.canvas = canvas;
            this.borders = borders;
            this.curCanvasInvPalette = curCanvasInvPalette;
            lastUpdateIterCount = new int[canvas.Width, canvas.Height];
        }

        public override PlacementMode GetMode()
        {
            return PlacementMode.TOPDOWN;
        }

        public override List<IdPixel> BuildQueue(int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill)
        {
            List<IdPixel> queue = new List<IdPixel>();
            AddManual(queue, userNumber, totalUsers);
            AddTasks(queue, completedTasksToFill);

            if (userNumber == 0)
            {
                //Console.WriteLine("Itercount is now {0}", iterCount);
                iterCount++;
            }
            if (queue.Count != 0)
                Console.WriteLine("Made a queue of {0} pixels", queue.Count);
            return queue;
        }

        private void AddTasks(List<IdPixel> queue, List<UsefulTask> completedTasksToFill)
        {
            Dictionary<int, List<IdPixel>> pixels = new Dictionary<int, List<IdPixel>>();

            foreach (UsefulTask task in guiData.Tasks)
            {
                bool completed = true;
                for (int j = 0; j < task.Image.Height; j++)
                {
                    for (int i = 0; i < task.Image.Width; i++)
                    {
                        foreach (KeyValuePair<int, List<IdPixel>> pair in pixels)
                        {
                            if (pair.Value.Count >= maxQueueSize)
                                goto loopend;
                        }

                        int canvasX = task.X + i;
                        int canvasY = task.Y + j;
                        if (canvasX < 0 || canvasY < 0 || canvasX >= canvas.Width || canvasY >= canvas.Height)
                            continue;
                        if (iterCount - lastUpdateIterCount[canvasX, canvasY] < pixelResendDelay) //avoid spamming the same place
                            continue;

                        var canvasPixel = canvas.GetPixel(canvasX, canvasY);
                        var bordersPixel = borders.GetPixel(canvasX, canvasY);
                        if (bordersPixel.R == 204 && bordersPixel.G == 204 && bordersPixel.B == 204)
                            continue;
                        var reqPixel = task.Image.GetPixel(i, j);
                        if (canvasPixel == reqPixel)
                            continue;
                        if (!curCanvasInvPalette.ContainsKey(reqPixel))
                            continue;
                        if (reqPixel.R == 255 && reqPixel.G == 255 && reqPixel.B == 255 && canvasPixel.A == 0)
                            continue;
                        completed = false;

                        int pxId = curCanvasInvPalette[reqPixel];
                        IdPixel pixel = new IdPixel(pxId, canvasX, canvasY);
                        if (pixels.ContainsKey(pxId))
                            pixels[pxId].Add(pixel);
                        else
                            pixels.Add(pxId, new List<IdPixel>() { pixel });
                    }
                }
                if (completed && !task.KeepRepairing)
                    completedTasksToFill.Add(task);
            }

            loopend:
            while (queue.Count < maxQueueSize && pixels.Count > 0)
            {
                KeyValuePair<int, List<IdPixel>> maxPair = new KeyValuePair<int, List<IdPixel>>();
                int maxPx = -1;
                foreach (KeyValuePair<int, List<IdPixel>> pair in pixels)
                {
                    if (pair.Value.Count > maxPx)
                    {
                        maxPx = pair.Value.Count;
                        maxPair = pair;
                    }
                }
                queue.AddRange(maxPair.Value);
                pixels.Remove(maxPair.Key);
            }
            if (queue.Count > maxQueueSize)
                queue.RemoveRange(maxQueueSize, queue.Count - maxQueueSize);
            foreach (IdPixel px in queue)
            {
                lastUpdateIterCount[px.X, px.Y] = iterCount;
            }
        }

        private void AddManual(List<IdPixel> queue, int userNumber, int totalUsers)
        {
            var pixels = guiData.ManualPixels;

            for (int i = userNumber; i < pixels.Count; i += totalUsers)
            {
                if (queue.Count >= maxQueueSize)
                    break;

                UsefulPixel reqPixel = pixels[i];
                if (iterCount - lastUpdateIterCount[reqPixel.X, reqPixel.Y] < pixelResendDelay) //avoid spamming the same place
                    continue;
                var canvasPixel = canvas.GetPixel(reqPixel.X, reqPixel.Y);
                if (canvasPixel.R == 204 && canvasPixel.G == 204 && canvasPixel.B == 204) //protected pixel
                    continue;
                if (canvasPixel == reqPixel.Color)
                    continue;
                if (reqPixel.Color.R == 255 && reqPixel.Color.G == 255 && reqPixel.Color.B == 255 && canvasPixel.A == 0)
                    continue;

                if (!curCanvasInvPalette.ContainsKey(reqPixel.Color))
                    continue;
                IdPixel pixel = new IdPixel(curCanvasInvPalette[reqPixel.Color], reqPixel.X, reqPixel.Y);
                queue.Add(pixel);
                lastUpdateIterCount[reqPixel.X, reqPixel.Y] = iterCount;

            }

        }
    }
}
