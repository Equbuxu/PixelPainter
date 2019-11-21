using GUIPixelPainter.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GUIPixelPainter
{
    public class GUIHelper
    {
        public Dictionary<int, List<System.Drawing.Color>> Palette { get; }
        public Dictionary<int, string> usernames;
        private HashSet<int> unknownUsernames = new HashSet<int>();
        public GUIDataExchange DataExchange { get; set; }

        public GUIHelper(Dictionary<int, List<System.Drawing.Color>> palette)
        {
            Palette = palette;
        }

        public string GetUsernameById(int id)
        {
            if (usernames.ContainsKey(id))
                return usernames[id];
            if (unknownUsernames.Add(id))
                DataExchange.UpdateUnknownUsernamesFromGUI();
            return id.ToString();
        }

        public void AddUsername(int id, string name)
        {
            unknownUsernames.Remove(id);
            if (!usernames.ContainsKey(id))
            {
                usernames.Add(id, name);
                DataExchange.UpdateUnknownUsernamesFromGUI();
            }
        }

        public IReadOnlyCollection<int> GetUnknownUsernames()
        {
            return unknownUsernames;
        }

        public BitmapSource Convert(System.Drawing.Bitmap bitmap)
        {
            bitmap.SetResolution(96, 96);

            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }
    }
}
