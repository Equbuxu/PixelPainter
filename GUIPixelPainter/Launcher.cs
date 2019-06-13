using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    class Launcher
    {
        GUI.BotWindow window;
        GUIDataExchange dataExchange;
        GUIHelper helper;
        UsefulDataRepresentation representation;
        GUIUpdater updater;
        UserManager manager;

        public void Launch(GUI.BotWindow window)
        {
            this.window = window;

            dataExchange = new GUIDataExchange(window.usersPanel, window.taskList, window.pixelCanvas, window);

            window.DataExchange = dataExchange;
            window.taskList.DataExchange = dataExchange;
            window.usersPanel.DataExchange = dataExchange;
            window.pixelCanvas.DataExchange = dataExchange;

            var palette = new Dictionary<int, Dictionary<int, System.Drawing.Color>>()
            {
                {
                    7, new Dictionary<int, System.Drawing.Color>() {
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
                        { 26, System.Drawing.Color.FromArgb(0x04, 0x4b, 0xff)},
                        { 27, System.Drawing.Color.FromArgb(0xbb, 0x4f, 0x00)},//premium 1
                        { 28, System.Drawing.Color.FromArgb(0x45, 0xff, 0xc8)}//premium 2
                    }
                },
                {
                    6, new Dictionary<int, System.Drawing.Color>() {
                         {0, System.Drawing.Color.FromArgb(0xCA, 0xDC, 0x9F) },
                         {1, System.Drawing.Color.FromArgb(0x9B, 0xBC, 0x0F) },
                         {2, System.Drawing.Color.FromArgb(0x8B, 0xAC, 0x0F) },
                         {3, System.Drawing.Color.FromArgb(0x30, 0x92, 0x30) },
                         {4, System.Drawing.Color.FromArgb(0x0F, 0x38, 0x0F) }
                    }
                },
                {
                    666, new Dictionary<int, System.Drawing.Color>() {
                         {0, System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF) },
                         {1, System.Drawing.Color.FromArgb(0xC4, 0xC4, 0xC4) },
                         {2, System.Drawing.Color.FromArgb(0x88, 0x88, 0x88) },
                         {3, System.Drawing.Color.FromArgb(0x22, 0x22, 0x22) }
                    }
                }
            };

            Dictionary<int, List<System.Drawing.Color>> GUIPalette = palette.Select((a) =>
            {
                return new KeyValuePair<int, List<System.Drawing.Color>>(a.Key, a.Value.Select((b) => b.Value).ToList());
            }).ToDictionary((a) => a.Key, (a) => a.Value);
            GUIPalette[7].RemoveRange(27, 2); //dont show premium color in UI


            helper = new GUIHelper(GUIPalette);
            window.taskList.Helper = helper;
            window.pixelCanvas.Helper = helper;

            ServicePointManager.DefaultConnectionLimit = 10;

            representation = new UsefulDataRepresentation(dataExchange);
            manager = new UserManager(representation, palette);

            updater = new GUIUpdater(palette);
            updater.DataExchange = dataExchange;
            updater.Manager = manager;

            dataExchange.UsefulData = representation;
            dataExchange.Updater = updater;

            //Test();
        }

        public void Test()
        {
            SocketIO socketIO = new SocketIO(
                "ljmltf44s38t906gfe0ohqquzuu9je34xppq4uvxx3q74xojco8tfyofsjocxshuh7lm1gjzja6n7u7jjfdze2e7l3jjv0dd7zz9fj8zufn1auluayto18vnbf67gkknjkxz36yxllzwgkqqjkfktb75rec7ywvhgsgkzfhl0hhg187ltn6mzdsqr4xq1s7ilo2l3k641nk3wsopfuce854092qbvythnw2qh8uiwfms8ain5v2d07da94l53fm",
                "z6z8z92taue1oyqt0htajrozfmcgi3biaaraku4uoivchm5i3zsnqhm64zmm3x5d8xosrsngbiss5ya9x3wokjuojhbmfh0neog5h3msmelrdw1bzxzkhu80bknq1nefculuy7mkm7cz4da3bans4wtfhg6i4kyhfkbdryye5beapod0z1t3ynjf4qx9awqqg138z2m5e0f3pt4ovxstlc8p26yd2o3jq7rqaewoebr45wt0ultfy24183fbsiu",
                7);

            /*SocketIO socketIO = new SocketIO(
                "jmltf44s38t906gfe0ohqquzuu9je34xppq4uvxx3q74xojco8tfyofsjocxshuh7lm1gjzja6n7u7jjfdze2e7l3jjv0dd7zz9fj8zufn1auluayto18vnbf67gkknjkxz36yxllzwgkqqjkfktb75rec7ywvhgsgkzfhl0hhg187ltn6mzdsqr4xq1s7ilo2l3k641nk3wsopfuce854092qbvythnw2qh8uiwfms8ain5v2d07da94l53fm",
                "gzvv6cwds86j2hlu0iupx4fi4srwyd5ed1ajd7x5f4phlbcmu6brbq9fj1bhfhwsi6cwd92tdsbz3nlxuwp5nyl70xofel8xrku5txy7p97sxtqrqfxdejleh9uwv2tmmosglrna1v2yosqe7osl8d0pnuliwe5i3yzopmyqi1p6ugl25dodro2fjnxf23y51xtqksh3t79nnvps9e5028flvd1yg03hxx8iqplkwv8kqyczci0eqg0mt1l91pr",
                7);*/

            socketIO.Connect();

            UserSession session = new UserSession(socketIO);

            int x = 1084;
            int y = 704;
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    if (i % 2 == j % 2)
                        session.Enqueue(new IdPixel(5, x + i, y + j));
                    else
                        session.Enqueue(new IdPixel(21, x + i, y + j));
                }
            }

        }
    }
}
