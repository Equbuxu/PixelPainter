using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public abstract class PlacementBehaviour
    {
        public abstract List<IdPixel> BuildQueue(int userNumber, int totalUsers, List<UsefulTask> completedTasksToFill);
        public abstract PlacementMode GetMode();
    }
}
