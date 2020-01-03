using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace GUIPixelPainter
{
    class DenoisePlacementBehaviour : PlacementBehaviour
    {
        private UsefulDataRepresentation guiData;
        private Bitmap canvas;
        private Bitmap borders;
        private const int maxQueueSize = 200;
        private Dictionary<System.Drawing.Color, int> curCanvasInvPalette;
        Random random = new Random();
        private Dictionary<Guid, List<IdPixel>> taskQueues = new Dictionary<Guid, List<IdPixel>>();

        public DenoisePlacementBehaviour(UsefulDataRepresentation guiData, Bitmap canvas, Bitmap borders, Dictionary<System.Drawing.Color, int> curCanvasInvPalette)
        {
            this.guiData = guiData;
            this.canvas = canvas;
            this.borders = borders;
            this.curCanvasInvPalette = curCanvasInvPalette;
        }

        public override PlacementMode GetMode()
        {
            return PlacementMode.DENOISE;
        }

        public override List<IdPixel> BuildQueue(int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill)
        {
            List<IdPixel> queue = new List<IdPixel>();
            UpdateTaskQueues();

            AddTasks(queue, userNumber, totalUsers, completedTasksToFill);

            if (queue.Count != 0)
                Logger.Info("Made a queue of {0} pixels", queue.Count);
            return queue;
        }

        private void UpdateTaskQueues()
        {
            HashSet<Guid> relevantTasks = new HashSet<Guid>();

            //Add new tasks
            foreach (UsefulTask task in guiData.Tasks)
            {
                relevantTasks.Add(task.Id);

                if (!taskQueues.ContainsKey(task.Id))
                {
                    taskQueues.Add(task.Id, GenerateTaskQueue(task));
                }

                //Redo finished tasks
                if (taskQueues[task.Id].Count == 0)
                    taskQueues[task.Id] = GenerateTaskQueue(task);
            }

            //Remove old tasks
            for (int i = taskQueues.Count - 1; i >= 0; i--)
            {
                Guid key = taskQueues.ElementAt(i).Key;
                if (!relevantTasks.Contains(key))
                    taskQueues.Remove(key);
            }
        }

        private List<IdPixel> GenerateTaskQueue(UsefulTask task)
        {
            List<IdPixel> queue = new List<IdPixel>();
            for (int j = 0; j < task.Image.Height; j++)
            {
                for (int i = 0; i < task.Image.Width; i++)
                {
                    Color canvasPixel;
                    Color bordersPixel;

                    int canvasX = task.X + i;
                    int canvasY = task.Y + j;
                    if (canvasX < 0 || canvasY < 0 || canvasX >= canvas.Width || canvasY >= canvas.Height)
                        continue;
                    canvasPixel = canvas.GetPixel(canvasX, canvasY);
                    bordersPixel = borders.GetPixel(canvasX, canvasY);

                    if (bordersPixel.R == 204 && bordersPixel.G == 204 && bordersPixel.B == 204)
                        continue;
                    var taskPixel = task.Image.GetPixel(i, j);
                    if (taskPixel.A == 0)
                        continue;
                    var reqPixel = GetAntidotColor(canvasX, canvasY);
                    if (canvasPixel == reqPixel)
                        continue;
                    if (!curCanvasInvPalette.ContainsKey(reqPixel))
                        continue;
                    IdPixel pixel = new IdPixel(curCanvasInvPalette[reqPixel], task.X + i, task.Y + j);
                    queue.Add(pixel);
                }
            }
            return queue;
        }

        private void ShuffleQueues()
        {
            taskQueues = taskQueues.OrderBy(x => random.Next()).ToDictionary(item => item.Key, item => item.Value);
        }

        private void AddTasks(List<IdPixel> queue, int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill)
        {
            ShuffleQueues();

            int queueColor = -1;
            bool queueColorChosen = false;
            foreach (KeyValuePair<Guid, List<IdPixel>> pair in taskQueues)
            {
                for (int i = pair.Value.Count - 1; i >= 0; i--)
                {
                    if (queue.Count >= maxQueueSize)
                        return;
                    var reqPixel = pair.Value[i];
                    if (!queueColorChosen)
                    {
                        queueColor = reqPixel.Color;
                        queueColorChosen = true;
                    }
                    if (reqPixel.Color != queueColor)
                        continue;
                    queue.Add(reqPixel);
                    pair.Value.RemoveAt(i);
                }

                if (pair.Value.Count == 0)
                {
                    UsefulTask done = guiData.Tasks.Where((a) => a.Id == pair.Key).FirstOrDefault();
                    if (done != null && !done.KeepRepairing)
                        completedTasksToFill.Add(done);
                }
            }
        }

        private Color GetAntidotColor(int x, int y)
        {
            Color center = canvas.GetPixel(x, y);

            Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
            for (int i = x - 1; i <= x + 1; i++)
            {
                for (int j = y - 1; j <= y + 1; j++)
                {
                    Color color;
                    try
                    {
                        color = canvas.GetPixel(i, j);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        continue;
                    }
                    if (!colorCounts.ContainsKey(color))
                        colorCounts.Add(color, 1);
                    else
                        colorCounts[color]++;
                }
            }

            if (colorCounts[center] > 2)
                return center;
            else
            {
                int max = -1;
                Color mC = center;
                foreach (KeyValuePair<Color, int> pair in colorCounts)
                {
                    if (pair.Value > max)
                    {
                        max = pair.Value;
                        mC = pair.Key;
                    }
                }
                return mC;
            }
        }
    }
}
