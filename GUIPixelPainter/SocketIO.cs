using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public string username = null;
        public int color = 0;
        public string guild = null;
        public string message = null;
        public bool admin = false;
        public bool mod = false;
        public bool premium = false;
        public int boardId = 0;

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
        public int x = 0;
        public int y = 0;
        [JsonProperty(PropertyName = "c")]
        public int color = 0;
        [JsonProperty(PropertyName = "u")]
        public int userId = 0;
        [JsonProperty(PropertyName = "b")]
        public int boardId = 0;

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

    //TODO make disposable to get rid of the client
    public class SocketIO
    {
        class IdResponce
        {
            public string sid = null;
            public string[] upgrades = null;
            public int pingInterval = 0;
            public int pingTimeout = 0;
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
        private string proxy;

        private string id;
        private Status status;
        private Thread thread;
        private bool abortRequested = false;

        public event OnEventHandler OnEvent;
        public delegate void OnEventHandler(string type, EventArgs args);

        private List<Tuple<Task<HttpResponseMessage>, bool>> tasks = new List<Tuple<Task<HttpResponseMessage>, bool>>();
        private HttpClient client;

        public SocketIO(string authKey, string authToken, int boardId, string proxy)
        {
            this.authKey = authKey;
            this.authToken = authToken;
            this.boardId = boardId;
            this.proxy = proxy;
        }

        public void Connect()
        {
            if (status != Status.NOTOPEN)
                throw new Exception("already connected");
            status = Status.CONNECTING;

            if (string.IsNullOrWhiteSpace(proxy))
                client = new HttpClient();
            else
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    UseProxy = true,
                    Proxy = new WebProxy(proxy)
                };
                client = new HttpClient(handler);
            }


            ConnectSequence();
            if (status == Status.CLOSEDDISCONNECT)
                return;

            thread = new Thread(ConnectLoop);
            thread.Name = "SocketIO polling";
            thread.IsBackground = true;
            thread.Start();
        }

        private void ConnectSequence()
        {
            try
            {
                //Get ID
                {
                    HttpResponseMessage resp = client.GetAsync(urlBase + CalcTime()).Result;
                    string response = resp.Content.ReadAsStringAsync().Result;
                    response = response.Remove(0, response.IndexOf('{'));
                    int index = response.IndexOf('}');
                    response = response.Remove(index + 1, response.Length - index - 1);
                    IdResponce result = JsonConvert.DeserializeObject<IdResponce>(response);
                    this.id = result.sid;
                }
                //Send authKey and authToken
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
                    string resultJson = sb.ToString();
                    StringContent request = new StringContent((resultJson.Length + 2).ToString() + ":42" + resultJson);
                    HttpResponseMessage response = client.PostAsync(urlBase + CalcTime() + "&sid=" + id, request).Result;
                }
            }
            catch (HttpRequestException)
            {
                status = Status.CLOSEDDISCONNECT;
            }
            catch (AggregateException)
            {
                status = Status.CLOSEDDISCONNECT;
            }
        }

        private void ConnectLoop()
        {
            status = Status.OPEN;

            DateTime lastUpdate;

            int updateResponsesRecieved = 1;
            int updateCount = 0;
            while (true)
            {
                lastUpdate = DateTime.Now;

                if (abortRequested || status != Status.OPEN)
                    return;

                try
                {
                    //Process responses
                    for (int i = tasks.Count - 1; i >= 0; i--)
                    {
                        if (!tasks[i].Item1.IsCompleted)
                            continue;
                        if (tasks[i].Item1.IsFaulted)
                        {
                            status = Status.CLOSEDDISCONNECT;
                            return;
                        }
                        string response = tasks[i].Item1.Result.Content.ReadAsStringAsync().Result;
                        if (response.Contains("Session ID unknown")) //HACK shouldnt be here
                        {
                            status = Status.CLOSEDDISCONNECT;
                            return;
                        }
                        if (!ProcessMessage(response))
                        {
                            status = Status.CLOSEDERROR;
                            return;
                        }
                        //Console.WriteLine(response.Length > 40 ? response.Substring(0, 40) : response);
                        if (tasks[i].Item2)
                            updateResponsesRecieved++;
                        tasks.RemoveAt(i);
                    }


                    //Request update
                    if (updateResponsesRecieved > 0)
                    {
                        tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.GetAsync(urlBase + CalcTime() + "&sid=" + id), true));
                        updateResponsesRecieved--;
                    }

                    //Ping
                    if (updateCount % 25 == 24)
                    {
                        StringContent pingContent = new StringContent("1:2");
                        tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.PostAsync(urlBase + CalcTime() + "&sid=" + id, pingContent), false));
                    }
                }
                catch (HttpRequestException)
                {
                    status = Status.CLOSEDDISCONNECT;
                    return;
                }
                catch (AggregateException)
                {
                    status = Status.CLOSEDDISCONNECT;
                    return;
                }

                updateCount++;

                int delay = 1000 - (DateTime.Now - lastUpdate).Milliseconds;
                if (delay > 0)
                    Thread.Sleep(delay);
            }
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

            StringBuilder builder = new StringBuilder();
            foreach (IdPixel pixel in pixels)
            {
                string pixelMessage = String.Format("42[\"place\",{{\"x\":{0},\"y\":{1},\"c\":{2}}}]", pixel.X, pixel.Y, pixel.Color);
                builder.Append(String.Format("{0}:{1}", pixelMessage.Length, pixelMessage));
            }
            StringContent content = new StringContent(builder.ToString());
            try
            {
                tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.PostAsync(urlBase + CalcTime() + "&sid=" + id, content), false));
                Console.Write("Sent {0} pixels", pixels.Count);
                foreach (IdPixel px in pixels)
                {
                    Console.Write("[{0}]", px.Color);
                }
                Console.Write("\n");
            }
            catch (HttpRequestException)
            {
                status = Status.CLOSEDDISCONNECT;
                return false;
            }
            return true;
        }

        public bool SendChatMessage(string message, int color)
        {
            if (status != Status.OPEN)
            {
                throw new Exception("not connected");
            }

            string data = String.Format("42[\"chat.message\",{{\"message\":\"{0}\",\"color\":{1}}}]", message, color);
            string packet = String.Format("{0}:{1}", data.Length, data);
            StringContent content = new StringContent(packet);
            try
            {
                tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.PostAsync(urlBase + CalcTime() + "&sid=" + id, content), false));
            }
            catch (HttpRequestException)
            {
                status = Status.CLOSEDDISCONNECT;
                return false;
            }
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
                        //Console.WriteLine("3");
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
                            Console.WriteLine("err {0}", errorId);
                        }
                        if (errorId == 0 || errorId == 1 || errorId == 2 || errorId == 6 || errorId == 7 || errorId == 9 || errorId == 10 || errorId == 18 || errorId == 19 || errorId == 20)
                        {
                            Console.WriteLine("\a");
                            Console.WriteLine("site err {0}", errorId);
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
