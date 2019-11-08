using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class DataLoader
    {
        private GUIDataExchange dataExchange;
        private GUIHelper helper;
        public DataLoader(GUIDataExchange dataExchange, GUIHelper helper)
        {
            this.dataExchange = dataExchange;
            this.helper = helper;
        }

        private string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter");
        private string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/config.json");
        private string usernamesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/usernames.json");

        enum ConfigVersion
        {
            UNKNOWN,
            LEGACY,
            v0,
            v1,
        }

        private ConfigVersion GetConfigVersion()
        {
            if (!File.Exists(configPath))
                return ConfigVersion.UNKNOWN;
            JsonTextReader reader = new JsonTextReader(File.OpenText(configPath));
            reader.Read(); //skip {
            reader.Read(); //read version

            if (reader.Value == null)
                return ConfigVersion.UNKNOWN;
            else if ((string)reader.Value == "Item1")
                return ConfigVersion.LEGACY;
            else if ((string)reader.Value == "version0")
                return ConfigVersion.v0;
            else if ((string)reader.Value == "version1")
                return ConfigVersion.v1;

            return ConfigVersion.UNKNOWN;
        }

        public void Load()
        {
            ConfigVersion version = GetConfigVersion();
            if (version == ConfigVersion.UNKNOWN)
                LoadNew();
            else if (version == ConfigVersion.LEGACY)
                LoadLegacy();
            else if (version == ConfigVersion.v0 || version == ConfigVersion.v1)
                LoadV0or1(version == ConfigVersion.v1);

            LoadUsernames();
        }

        public void LoadUsernames()
        {
            Dictionary<int, string> embeddedNames = JsonConvert.DeserializeObject<Dictionary<int, string>>(Properties.Resources.Usernames);

            if (File.Exists(usernamesPath))
            {
                Dictionary<int, string> savedNames = JsonConvert.DeserializeObject<Dictionary<int, string>>(File.ReadAllText(usernamesPath));
                foreach (KeyValuePair<int, string> pair in savedNames)
                {
                    if (!embeddedNames.ContainsKey(pair.Key))
                        embeddedNames.Add(pair.Key, pair.Value);
                }
            }

            helper.usernames = embeddedNames;
        }

        public void SaveUsernames(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(helper.usernames, Formatting.Indented));
        }

        private void LoadNew()
        {
            dataExchange.PushSettings(true, false, false, 0.5, 7, PlacementMode.TOPDOWN, 11.2);
            dataExchange.PushWindowState(1350, 950, System.Windows.WindowState.Normal);
        }

        private void LoadV0or1(bool v1)
        {
            using (StreamReader file = File.OpenText(configPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                List<object> data = serializer.Deserialize(file, typeof(List<object>)) as List<object>;

                string version = data[0] as string;
                Dictionary<int, List<GUITask>> tasks = (data[1] as JObject).ToObject(typeof(Dictionary<int, List<GUITask>>)) as Dictionary<int, List<GUITask>>;
                List<GUIUser> users = (data[2] as JArray).ToObject(typeof(List<GUIUser>)) as List<GUIUser>; ;
                Dictionary<string, string> dataExchangeData = (data[3] as JObject).ToObject(typeof(Dictionary<string, string>)) as Dictionary<string, string>;

                //load images and push tasks
                foreach (KeyValuePair<int, List<GUITask>> canvas in tasks)
                {
                    foreach (GUITask task in canvas.Value)
                    {
                        Bitmap original, converted, dithered;
                        try
                        {
                            original = LoadBitmap(Path.Combine(folderPath, "original", task.InternalId + ".png"));
                            converted = LoadBitmap(Path.Combine(folderPath, "converted", task.InternalId + ".png"));
                            dithered = LoadBitmap(Path.Combine(folderPath, "dithered", task.InternalId + ".png"));
                        }
                        catch (FileNotFoundException)
                        {
                            return;
                        }
                        GUITask newTask = new GUITask(task.InternalId, task.Name, task.Enabled, task.X, task.Y, task.Dithering, task.KeepRepairing, original, converted, dithered);
                        dataExchange.PushNewTask(newTask, canvas.Key);
                    }
                }

                //push users
                foreach (GUIUser user in users)
                {
                    dataExchange.PushNewUser(user);
                }

                //parse parameters
                bool overlayTasks = bool.Parse(dataExchangeData["overlayTasks"]);
                bool overlayAllTasks = bool.Parse(dataExchangeData["overlayAllTasks"]);
                bool overlaySelectedTask = bool.Parse(dataExchangeData["overlaySelectedTasks"]);
                double overlayTranslucency = double.Parse(dataExchangeData["overlayTranslucency"], CultureInfo.InvariantCulture);
                int canvasId = int.Parse(dataExchangeData["canvasId"]);
                PlacementMode placementMode = (PlacementMode)Enum.Parse(typeof(PlacementMode), dataExchangeData["placementMode"]);

                double placementSpeed = 11.2;
                if (v1)
                    placementSpeed = double.Parse(dataExchangeData["placementSpeed"], CultureInfo.InvariantCulture);

                double windowWidth = double.Parse(dataExchangeData["windowWidth"], CultureInfo.InvariantCulture);
                double windowHeight = double.Parse(dataExchangeData["windowHeight"], CultureInfo.InvariantCulture);
                System.Windows.WindowState windowState = (System.Windows.WindowState)Enum.Parse(typeof(System.Windows.WindowState), dataExchangeData["windowState"]);

                dataExchange.PushSettings(overlayTasks, overlayAllTasks, overlaySelectedTask, overlayTranslucency, canvasId, placementMode, placementSpeed);
                dataExchange.PushWindowState(windowWidth, windowHeight, windowState);
            }
        }

        private void LoadLegacy()
        {
            using (StreamReader file = File.OpenText(configPath))
            {
                JsonSerializer serializer = new JsonSerializer();
                var data = (Tuple<Dictionary<int, List<GUITask>>, List<GUIUser>, bool, int>)serializer.Deserialize(file, typeof(Tuple<Dictionary<int, List<GUITask>>, List<GUIUser>, bool, int>));

                var tasks = data.Item1;
                var users = data.Item2;
                bool overlayTasks = data.Item3;
                int canvasId = data.Item4;

                //load images and push tasks
                foreach (KeyValuePair<int, List<GUITask>> canvas in tasks)
                {
                    foreach (GUITask task in canvas.Value)
                    {
                        Bitmap original, converted, dithered;
                        try
                        {
                            original = LoadBitmap(Path.Combine(folderPath, "original", task.InternalId + ".png"));
                            converted = LoadBitmap(Path.Combine(folderPath, "converted", task.InternalId + ".png"));
                            dithered = LoadBitmap(Path.Combine(folderPath, "dithered", task.InternalId + ".png"));
                        }
                        catch (FileNotFoundException)
                        {
                            return;
                        }
                        GUITask newTask = new GUITask(task.InternalId, task.Name, task.Enabled, task.X, task.Y, task.Dithering, task.KeepRepairing, original, converted, dithered);
                        dataExchange.PushNewTask(newTask, canvas.Key);
                    }
                }

                //push users
                foreach (GUIUser user in users)
                {
                    dataExchange.PushNewUser(user);
                }

                dataExchange.PushSettings(overlayTasks, false, false, 0.5, canvasId, PlacementMode.TOPDOWN, 11.2);
                dataExchange.PushWindowState(1350, 950, System.Windows.WindowState.Normal);
            }
        }

        private static Bitmap LoadBitmap(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = (Bitmap)Image.FromStream(ms);
            return img;
        }

        private void ClearDirectory(string path)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(Path.Combine(folderPath, "original"));
            Directory.CreateDirectory(Path.Combine(folderPath, "converted"));
            Directory.CreateDirectory(Path.Combine(folderPath, "dithered"));

            //Delete old images
            ClearDirectory(Path.Combine(folderPath, "original"));
            ClearDirectory(Path.Combine(folderPath, "converted"));
            ClearDirectory(Path.Combine(folderPath, "dithered"));

            using (StreamWriter file = new StreamWriter(File.Create(configPath)))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                Dictionary<int, List<GUITask>> tasks = dataExchange.GUITasks.Select((a) => a).ToDictionary((a) => a.Key, (a) => a.Value.Select((b) => b).ToList());
                Dictionary<string, string> dataExchangeData = new Dictionary<string, string>()
                {
                    {"overlayTasks", dataExchange.OverlayTasks.ToString(CultureInfo.InvariantCulture) },
                    {"overlayAllTasks", dataExchange.OverlayAllTasks.ToString(CultureInfo.InvariantCulture) },
                    {"overlaySelectedTasks", dataExchange.OverlaySelectedTask.ToString(CultureInfo.InvariantCulture) },
                    {"overlayTranslucency", dataExchange.OverlayTranslucency.ToString(CultureInfo.InvariantCulture) },
                    {"canvasId", dataExchange.CanvasId.ToString(CultureInfo.InvariantCulture) },
                    {"placementMode", dataExchange.PlacementMode.ToString() },
                    {"placementSpeed", dataExchange.PlacementSpeed.ToString(CultureInfo.InvariantCulture) },
                    {"windowWidth", dataExchange.windowWidth.ToString(CultureInfo.InvariantCulture) },
                    {"windowHeight", dataExchange.windowHeight.ToString(CultureInfo.InvariantCulture) },
                    {"windowState", dataExchange.windowState.ToString() },
                };
                List<GUIUser> users = dataExchange.GUIUsers.Select((a) => a).ToList();
                bool overlayTasks = dataExchange.OverlayTasks;
                int canvasId = dataExchange.CanvasId;
                serializer.Serialize(file, new List<object>() {
                    "version1",
                    tasks,
                    users,
                    dataExchangeData,
                    });

                //save images
                foreach (KeyValuePair<int, List<GUITask>> pair in tasks)
                {
                    foreach (GUITask task in pair.Value)
                    {
                        task.OriginalBitmap.Save(Path.Combine(folderPath, "original", task.InternalId + ".png"));
                        task.ConvertedBitmap.Save(Path.Combine(folderPath, "converted", task.InternalId + ".png"));
                        task.DitheredConvertedBitmap.Save(Path.Combine(folderPath, "dithered", task.InternalId + ".png"));
                    }
                }
            }

            SaveUsernames(usernamesPath);
        }
    }
}