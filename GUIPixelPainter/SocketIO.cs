using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace GUIPixelPainter
{
    public enum Status
    {
        NOTOPEN,
        CONNECTING,
        OPEN,
        CLOSEDERROR,
        CLOSEDDISCONNECT
    }

    class ErrorPacket : EventArgs
    {
        public int id;
    }

    class ChatMessagePacket : EventArgs
    {
        public string username;
        public int color;
        public string guild;
        public string message;
        public bool admin;
        public bool mod;
        public bool premium;
        public int boardId;

        public override int GetHashCode()
        {
            var properties = typeof(ChatMessagePacket).GetFields();
            int hash = 0;
            foreach (var prop in properties)
            {
                hash ^= prop.GetValue(this).GetHashCode();
            }
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ChatMessagePacket))
                return false;
            ChatMessagePacket packet = obj as ChatMessagePacket;

            var properties = typeof(ChatMessagePacket).GetFields();
            bool suc = true;
            foreach (var prop in properties)
            {
                if (!prop.GetValue(this).Equals(prop.GetValue(obj)))
                {
                    suc = false;
                    break;
                }
            }
            return suc;
        }
    }

    class PixelPacket : EventArgs
    {
        public int x;
        public int y;
        [JsonProperty(PropertyName = "c")]
        public int color;
        [JsonProperty(PropertyName = "u")]
        public int userId;
        [JsonProperty(PropertyName = "b")]
        public int boardId;

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ color.GetHashCode() ^ userId.GetHashCode() ^ boardId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PixelPacket))
                return false;
            PixelPacket packet = obj as PixelPacket;
            return packet.x == x && packet.y == y && packet.userId == userId && packet.color == color && packet.boardId == boardId;
        }
    }

    public class IdPixel //TODO move all these container classes into seperate file(s)
    {
        public IdPixel(int color, int x, int y)
        {
            Color = color;
            X = x;
            Y = y;
        }
        public int Color { get; }
        public int X { get; }
        public int Y { get; }
    }

    //TODO rewrite using HttpClient
    public class SocketIO
    {
        class IdResponce
        {
            public string sid;
            public string[] upgrades;
            public int pingInterval;
            public int pingTimeout;
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly char[] characters = new char[] {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E',
            'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i',
            'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '-', '_'
        };
        private static readonly string urlBase = "https://canvas.pixelplace.io:7777/socket.io/?EIO=3&transport=polling&t=";

        private int boardId;
        private string authKey;
        private string authToken;

        private string id;
        private Status status;
        private Thread thread;
        private bool abortRequested = false;

        public event OnEventHandler OnEvent;
        public delegate void OnEventHandler(string type, EventArgs args);

        public SocketIO(string authKey, string authToken, int boardId)
        {
            this.authKey = authKey;
            this.authToken = authToken;
            this.boardId = boardId;
        }

        public void Connect()
        {
            if (status != Status.NOTOPEN)
                throw new Exception("already connected");
            status = Status.CONNECTING;

            thread = new Thread(ConnectLoop);
            thread.IsBackground = true;
            thread.Start();
        }

        public Status GetStatus()
        {
            return status;
        }

        public void Disconnect()
        {
            if (status == Status.OPEN || status == Status.CONNECTING)
                abortRequested = true;
            else if (status == Status.NOTOPEN)
                throw new Exception("can't disconnect if there is no connection");
            else if (status == Status.CLOSEDDISCONNECT || status == Status.CLOSEDERROR)
                throw new Exception("already closed");
        }

        public bool SendPixels(IReadOnlyCollection<IdPixel> pixels)
        {
            if (status != Status.OPEN)
            {
                throw new Exception("not connected");
            }


            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + CalcTime() + "&sid=" + id);
            rq.Method = "POST";
            rq.Timeout = 4000;

            using (StreamWriter s = new StreamWriter(rq.GetRequestStream()))//TODO web exceprion crash
            {
                foreach (IdPixel pixel in pixels)
                {
                    string pixelMessage = String.Format("42[\"place\",{{\"x\":{0},\"y\":{1},\"c\":{2}}}]", pixel.X, pixel.Y, pixel.Color);
                    s.Write(String.Format("{0}:{1}", pixelMessage.Length, pixelMessage));
                }
            }

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                rq.Abort();
                return false;
            }
            resp.Close();
            return true;
        }

        public bool SendChatMessage(string message, int color)
        {
            if (status != Status.OPEN)
            {
                throw new Exception("not connected");
            }


            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + CalcTime() + "&sid=" + id);
            rq.Method = "POST";
            rq.Timeout = 4000;

            using (StreamWriter s = new StreamWriter(rq.GetRequestStream()))
            {
                string toSend = String.Format("42[\"chat.message\",{{\"message\":\"{0}\",\"color\":{1}}}]", message, color);
                s.Write(String.Format("{0}:{1}", toSend.Length, toSend));
            }

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                rq.Abort();
                return false;
            }
            resp.Close();
            return true;
        }

        private string CalcTime()
        {
            long time = (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
            int a = 64;
            string result = "";
            do
            {
                result = characters[time % a] + result;
                time = (long)Math.Floor((double)(time / a));
            }
            while (time > 0);
            return result;
        }

        private bool FetchID()
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + CalcTime());
            rq.Timeout = 4000;
            rq.Method = "GET";
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                rq.Abort();
                return false;
            }
            string response;
            using (StreamReader s = new StreamReader(resp.GetResponseStream()))
            {
                response = s.ReadToEnd();
            }

            response = response.Remove(0, response.IndexOf('{'));
            int index = response.IndexOf('}');
            response = response.Remove(index + 1, response.Length - index - 1);

            IdResponce result = JsonConvert.DeserializeObject<IdResponce>(response);

            this.id = result.sid;
            return true;
        }

        private bool SendAuthInfo()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.None;

                writer.WriteStartArray();
                writer.WriteValue("init");
                writer.WriteStartObject();
                writer.WritePropertyName("authKey");
                writer.WriteValue(authKey);
                writer.WritePropertyName("authToken");
                writer.WriteValue(authToken);
                writer.WritePropertyName("boardId");
                writer.WriteValue(boardId);
                writer.WriteEndObject();
                writer.WriteEnd();
            }

            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + CalcTime() + "&sid=" + id);
            rq.Method = "POST";
            rq.Timeout = 4000;

            string resultJson = sb.ToString();
            using (StreamWriter s = new StreamWriter(rq.GetRequestStream()))
            {
                s.Write(resultJson.Length + 2);
                s.Write(":42");
                s.Write(resultJson);
            }

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                rq.Abort();
                return false;
            }
            return true;
        }

        private string FetchUpdate()
        {
            string time = CalcTime();
            //Console.WriteLine(time);
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + time + "&sid=" + id);
            rq.Timeout = 4000;

            rq.Method = "GET";

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                return null;
            }

            string response;
            using (StreamReader s = new StreamReader(resp.GetResponseStream()))
            {
                response = s.ReadToEnd();
            }
            //File.WriteAllText("text.txt", response);
            return response;
        }

        private bool SendPing()
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(urlBase + CalcTime() + "&sid=" + id);
            rq.Timeout = 4000;

            rq.Method = "POST";

            using (StreamWriter s = new StreamWriter(rq.GetRequestStream()))
            {
                s.Write("1:2");
            }

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)rq.GetResponse();
            }
            catch (WebException)
            {
                rq.Abort();
                return false;
            }

            resp.Close();

            return true;
        }

        private void ConnectLoop()
        {
            Console.WriteLine("fetch id");
            if (!FetchID() || !SendAuthInfo())
            {
                status = Status.CLOSEDDISCONNECT;
                return;
            }

            Console.WriteLine("recent actions");
            string recentActions = FetchUpdate();
            if (recentActions == null)
            {
                status = Status.CLOSEDDISCONNECT;
                return;
            }

            if (!ProcessMessage(recentActions))
            {
                status = Status.CLOSEDERROR;
                return;
            }

            status = Status.OPEN;
            Console.WriteLine("successful connection");

            DateTime lastUpdate;
            int updateCount = 0;
            while (true)
            {
                lastUpdate = DateTime.Now;

                if (abortRequested)
                    return;

                Console.WriteLine("fetch update");
                string update = FetchUpdate();
                if (update == null)
                {
                    status = Status.CLOSEDDISCONNECT;
                    return;
                }

                if (!ProcessMessage(update))
                {
                    status = Status.CLOSEDERROR;
                    return;
                }

                if (updateCount % 25 == 24)
                {
                    Console.WriteLine("ping");
                    if (!SendPing())
                    {
                        status = Status.CLOSEDDISCONNECT;
                        return;
                    }
                }

                updateCount++;

                int delay = 900 - (DateTime.Now - lastUpdate).Milliseconds;
                if (delay > 0)
                    System.Threading.Thread.Sleep(delay);
            }
        }

        private bool ProcessMessage(string data)
        {
            if (!data.Contains('['))
            {
                return true;
            }

            List<string> packets = new List<string>();
            if (data.IndexOf('[') > data.IndexOf(':') && data.Contains(':'))
            {
                while (data.Length > 0)
                {
                    int colonIndex = data.IndexOf(':');
                    int packetSize = int.Parse(data.Substring(0, colonIndex));
                    if (packetSize > 1)
                    {
                        string packet = data.Substring(colonIndex + 3, packetSize - 2);
                        packets.Add(packet);
                    }
                    else
                    {
                        Console.WriteLine("3");
                    }
                    data = data.Substring(packetSize + colonIndex + 1);
                }
            }
            else
            {
                int bracketIndex = data.IndexOf('[');
                int curlyIndex = data.IndexOf('{');

                if (bracketIndex == -1) bracketIndex = int.MaxValue;
                if (curlyIndex == -1) curlyIndex = int.MaxValue;

                int leftmost = Math.Min(bracketIndex, curlyIndex);
                if (leftmost == int.MaxValue) leftmost = 0;

                packets.Add(data.Substring(leftmost));
            }

            foreach (string packet in packets)
            {
                JArray objects = JArray.Parse(packet);

                switch (objects[0].ToString())
                {
                    case "canvas":
                    case "pixels":
                        string type = objects[0].ToString();
                        List<PixelPacket> pixels = objects[1].ToObject<List<PixelPacket>>();
                        if (OnEvent != null)
                        {
                            foreach (PixelPacket pixel in pixels)
                            {
                                OnEvent("pixels", pixel);
                            }
                        }
                        break;
                    case "cooldown":
                        OnEvent?.Invoke("cooldown", null);
                        break;
                    case "protection":
                        OnEvent?.Invoke("protection", null);
                        break;
                    case "canvas.access.requested":
                        break;
                    case "canvas.alert":
                        break;
                    case "throw.error":
                        int errorId = objects[1].ToObject<int>();
                        if (OnEvent != null)
                        {
                            ErrorPacket eventArgs = new ErrorPacket();
                            eventArgs.id = errorId;
                            OnEvent("throw.error", eventArgs);
                        }
                        if (errorId == 0 || errorId == 1 || errorId == 2 || errorId == 6 || errorId == 7 || errorId == 9 || errorId == 10 || errorId == 18 || errorId == 19 || errorId == 20)
                        {
                            Console.WriteLine("\a");
                            Console.WriteLine("site err " + errorId);
                            return false;
                        }
                        break;
                    case "chat.user.message":
                        ChatMessagePacket message = objects[1].ToObject<ChatMessagePacket>();
                        if (OnEvent != null)
                        {
                            OnEvent("chat.user.message", message);
                        }
                        break;
                    case "chat.system.message":
                        break;
                    case "chat.system.delete":
                        break;
                    case "chat.system.announce":
                        break;
                }
            }
            return true;
        }
    }
}
