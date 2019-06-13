using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GUIPixelPainter
{
    public class Model
    {
        public List<User> users = new List<User>();
        public WriteableBitmap canvasState;
        public List<Task> tasks = new List<Task>();
        public Dictionary<int, System.Drawing.Color> palette;
        public Dictionary<System.Drawing.Color, int> invPalette;
        public int canvasId;

        private UserManager manager;
        private bool launched = false;

        public Model()
        {

        }

        public void Launch()
        {
            if (launched)
                return;
            launched = true;
            GetBoardState();
            PopulatePalette();
            manager = new UserManager(this);
        }

        private void PopulatePalette()//TODO add all palettes
        {
            if (canvasId == 666)
            {

            }
            else if (canvasId == 6)
            {

            }
            else
            {
                palette = new Dictionary<int, System.Drawing.Color>()
            {
                { 0, System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF)},
                { 1, System.Drawing.Color.FromArgb(0xC4, 0xC4, 0xC4)},
                { 2, System.Drawing.Color.FromArgb(0x88, 0x88, 0x88)},
                { 3, System.Drawing.Color.FromArgb(0x22, 0x22, 0x22)},
                { 4, System.Drawing.Color.FromArgb(0xFF, 0xA7, 0xD1)},
                { 5, System.Drawing.Color.FromArgb(0xE5, 0x00, 0x00)},
                { 6, System.Drawing.Color.FromArgb(0xE5, 0x95, 0x00)},
                { 7, System.Drawing.Color.FromArgb(0xA0, 0x6A, 0x42)},
                { 8, System.Drawing.Color.FromArgb(0xE5, 0xD9, 0x00)},
                { 9, System.Drawing.Color.FromArgb(0x94, 0xE0, 0x44)},
                { 10, System.Drawing.Color.FromArgb(0x02, 0xBE, 0x01)},
                { 11, System.Drawing.Color.FromArgb(0x00, 0xD3, 0xDD)},
                { 12, System.Drawing.Color.FromArgb(0x00, 0x83, 0xC7)},
                { 13, System.Drawing.Color.FromArgb(0x00, 0x00, 0xEA)},
                { 14, System.Drawing.Color.FromArgb(0xCF, 0x6E, 0xE4)},
                { 15, System.Drawing.Color.FromArgb(0x82, 0x00, 0x80)},
                { 16, System.Drawing.Color.FromArgb(0xff, 0xdf, 0xcc)},
                { 17, System.Drawing.Color.FromArgb(0x55, 0x55, 0x55)},
                { 18, System.Drawing.Color.FromArgb(0x00, 0x00, 0x00)},
                { 19, System.Drawing.Color.FromArgb(0xec, 0x08, 0xec)},
                { 20, System.Drawing.Color.FromArgb(0x6b, 0x00, 0x00)},
                { 21, System.Drawing.Color.FromArgb(0xff, 0x39, 0x04)},
                { 22, System.Drawing.Color.FromArgb(0x63, 0x3c, 0x1f)},
                { 23, System.Drawing.Color.FromArgb(0x51, 0xe1, 0x19)},
                { 24, System.Drawing.Color.FromArgb(0x00, 0x66, 0x00)},
                { 25, System.Drawing.Color.FromArgb(0x36, 0xba, 0xff)},
                { 26, System.Drawing.Color.FromArgb(0x04, 0x4b, 0xff)}
            };
            }

            invPalette = palette.ToDictionary((a) => a.Value, (a) => a.Key);
        }

        private void GetBoardState()
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead("https://pixelplace.io/canvas/" + canvasId + ".png");
            Bitmap bitmap = new Bitmap(stream);
            canvasState = new WriteableBitmap(Convert(bitmap));
            bitmap.Dispose();
            stream.Flush();
            stream.Close();
            client.Dispose();
        }

    }
}
