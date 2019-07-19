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
        private int[,] lastUpdateIterCount;
        private const int maxQueueSize = 200;
        private const int pixelResendDelay = 15;
        int iterCount = 0;
        private Dictionary<System.Drawing.Color, int> curCanvasInvPalette;

        public TopDownPlacementBehaviour(UsefulDataRepresentation guiData, Bitmap canvas, Dictionary<System.Drawing.Color, int> curCanvasInvPalette)
        {
            this.guiData = guiData;
            this.canvas = canvas;
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
            AddTasks(queue, userNumber, totalUsers, completedTasksToFill);

            if (userNumber == 0)
            {
                //Console.WriteLine("Itercount is now {0}", iterCount);
                iterCount++;
            }
            Console.WriteLine("Made a queue of {0} pixels", queue.Count);
            return queue;
        }

        private void AddTasks(List<IdPixel> queue, int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill)
        {
            foreach (UsefulTask task in guiData.Tasks)
            {
                bool completed = true;
                Color queueColor = Color.Transparent;
                bool queueColorChosen = false;
                for (int j = 0; j < task.Image.Height; j++)
                {
                    for (int i = userNumber; i < task.Image.Width; i += totalUsers)
                    {
                        if (queue.Count >= maxQueueSize)
                            return;

                        var canvasPixel = canvas.GetPixel(task.X + i, task.Y + j);
                        if (canvasPixel.R == 204 && canvasPixel.G == 204 && canvasPixel.B == 204)
                            continue;
                        var reqPixel = task.Image.GetPixel(i, j);
                        if (canvasPixel == reqPixel)
                            continue;
                        if (!curCanvasInvPalette.ContainsKey(reqPixel))
                            continue;
                        if (!queueColorChosen)
                        {
                            queueColor = reqPixel;
                            queueColorChosen = true;
                        }
                        if (reqPixel != queueColor)
                            continue;
                        completed = false;

                        if (iterCount - lastUpdateIterCount[task.X + i, task.Y + j] < 6) //avoid spamming the same place
                            continue;

                        IdPixel pixel = new IdPixel(curCanvasInvPalette[reqPixel], task.X + i, task.Y + j);
                        queue.Add(pixel);
                        lastUpdateIterCount[task.X + i, task.Y + j] = iterCount;
                    }
                }
                if (completed && !task.KeepRepairing)
                    completedTasksToFill.Add(task);
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
                if (!curCanvasInvPalette.ContainsKey(reqPixel.Color))
                    continue;
                IdPixel pixel = new IdPixel(curCanvasInvPalette[reqPixel.Color], reqPixel.X, reqPixel.Y);
                queue.Add(pixel);
                lastUpdateIterCount[reqPixel.X, reqPixel.Y] = iterCount;
            }
        }
    }
}
