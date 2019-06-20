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

        public GUIHelper(Dictionary<int, List<System.Drawing.Color>> palette)
        {
            Palette = palette;
            usernames = JsonConvert.DeserializeObject<Dictionary<int, string>>(Resources.Usernames);
        }

        public string GetUsernameById(int id)
        {
            if (usernames.ContainsKey(id))
                return usernames[id];// + " (" + id.ToString() + ")";
            return id.ToString();
        }

        public BitmapSource Convert(System.Drawing.Bitmap bitmap)
        {
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
