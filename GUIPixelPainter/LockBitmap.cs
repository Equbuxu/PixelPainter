using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class LockBitmap : IDisposable
    {
        private readonly object lockObj = new object();
        private Bitmap bitmap;

        public LockBitmap(System.IO.Stream stream)
        {
            bitmap = new Bitmap(stream);
        }

        public LockBitmap(int width, int height)
        {
            bitmap = new Bitmap(width, height);
        }

        public void Dispose()
        {
            lock (lockObj)
            {
                bitmap.Dispose();
            }
        }

        public Color GetPixel(int x, int y)
        {
            lock (lockObj)
            {
                return bitmap.GetPixel(x, y);
            }
        }

        public void SetPixel(int x, int y, Color color)
        {
            lock (lockObj)
            {
                bitmap.SetPixel(x, y, color);
            }
        }
    }
}
