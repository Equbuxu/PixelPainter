using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class Config
    {
        private Config()
        {

        }

        private static Config instance = null;

        private static Bitmap LoadBitmap(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = (Bitmap)Image.FromStream(ms);
            return img;
        }

        private static Config Load()
        {
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/config.json");
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter");

            if (!File.Exists(configPath))
                return new Config();

            using (StreamReader file = File.OpenText(configPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                Config config = (Config)serializer.Deserialize(file, typeof(Config));

                //load images
                foreach (Task task in config.tasks)
                {
                    try
                    {
                        task.image = LoadBitmap(Path.Combine(folderPath, task.name + ".png"));
                    }
                    catch (FileNotFoundException)
                    {
                        task.image = null;
                    }
                }

                return config;
            }
        }

        public void Save()
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter");
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/config.json");

            Directory.CreateDirectory(folderPath);

            using (StreamWriter file = new StreamWriter(File.Create(configPath)))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, this);

                //save images
                foreach (Task task in tasks)
                {
                    task.image.Save(Path.Combine(folderPath, task.name + ".png"));
                }
            }
        }

        public static Config GetInstance()
        {
            if (instance == null)
                instance = Load();
            return instance;
        }

        public int canvasId = 7;
        public List<User> users = new List<User>();
        public List<Task> tasks = new List<Task>();
    }
}
