using System.Collections.Generic;

namespace GUIPixelPainter
{
    public abstract class PlacementBehaviour
    {
        public abstract List<IdPixel> BuildQueue(int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill);
        public abstract PlacementMode GetMode();
    }
}
