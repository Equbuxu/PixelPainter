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

    class TokenPacket : EventArgs
    {
        public string phpSessId;
        public string authToken;
        public Guid id;
    }

    class NicknamePacket : EventArgs
    {
        public int id;
        public string nickname;
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
        public int chat = 0;

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
        [JsonIgnore]
        public int socketUserId = 0;

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ color.GetHashCode() ^ userId.GetHashCode() ^ boardId.GetHashCode() ^ socketUserId.GetHashCode();
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
        private string phpSessId;
        private string proxy;

        private bool premium;
        private string username;
        private int pixelId;

        public bool Premium { get { return premium; } }
        public string Username { get { return username; } }
        public int PixelId { get { return pixelId; } }

        private string id;
        private Status status;
        private Thread thread;
        private bool abortRequested = false;

        public event OnEventHandler OnEvent;
        public delegate void OnEventHandler(string type, EventArgs args);

        private List<Tuple<Task<HttpResponseMessage>, bool>> tasks = new List<Tuple<Task<HttpResponseMessage>, bool>>();
        private HttpClient client;

        public SocketIO(string authKey, string authToken, string phpSessId, int boardId, string proxy)
        {
            this.authKey = authKey;
            this.authToken = authToken;
            this.phpSessId = phpSessId;
            this.boardId = boardId;
            this.proxy = proxy;
        }

        public void StartConnect()
        {
            thread = new Thread(Connect);
            thread.Name = "SocketIO polling";
            thread.IsBackground = true;
            thread.Start();
        }

        private void Connect()
        {
            if (status != Status.NOTOPEN)
                throw new Exception("already connected");
            status = Status.CONNECTING;

            CookieContainer cookies = new CookieContainer();
            cookies.Add(new Cookie("PHPSESSID", phpSessId, "/", "pixelplace.io"));
            cookies.Add(new Cookie("authKey", authKey, "/", "pixelplace.io"));
            cookies.Add(new Cookie("authToken", authToken, "/", "pixelplace.io"));

            HttpClientHandler handler;
            if (string.IsNullOrWhiteSpace(proxy))
                handler = new HttpClientHandler() { CookieContainer = cookies };
            else
            {
                handler = new HttpClientHandler()
                {
                    UseProxy = true,
                    Proxy = new WebProxy(proxy)
                };
            }
            client = new HttpClient(handler);


            ConnectSequence();
            if (status == Status.CLOSEDDISCONNECT || status == Status.CLOSEDERROR)
                return;

            ConnectLoop();
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
                //Get premium status, username, new AuthToken and PHPSESSID
                {
                    var page = client.GetAsync("https://pixelplace.io/").Result;

                    IEnumerable<string> values;
                    if (page.Headers.TryGetValues("Set-Cookie", out values))
                    {
                        bool changed = false;
                        foreach (string s in values)
                        {
                            if (s.StartsWith("authToken"))
                            {
                                string[] data = s.Split('=', ';');
                                if (data[1] != authToken)
                                {
                                    changed = true;
                                    authToken = data[1];
                                }
                            }
                            else if (s.StartsWith("PHPSESSID"))
                            {
                                string[] data = s.Split('=', ';');
                                if (data[1] != phpSessId)
                                {
                                    changed = true;
                                    phpSessId = data[1];
                                }
                            }
                        }
                        if (changed)
                        {
                            TokenPacket packet = new TokenPacket() { authToken = authToken, phpSessId = phpSessId };
                            OnEvent("tokens", packet);
                        }
                    }

                    if (authToken == "deleted")
                    {
                        status = Status.CLOSEDERROR;
                        return;
                    }

                    string content = page.Content.ReadAsStringAsync().Result;
                    int configStart = content.IndexOf(@"var CONFIG = {");
                    int userStart = content.IndexOf("user: {", configStart);

                    int idStart = content.IndexOf(@"id:", userStart);
                    string idToken = content.Substring(idStart, content.IndexOf(',', idStart) - idStart);
                    string idString = idToken.Substring(3, idToken.Length - 3);
                    if (idString == "null")
                    {
                        status = Status.CLOSEDERROR;
                        return;
                    }
                    pixelId = int.Parse(idString);

                    int usernameStart = content.IndexOf(@"username:", userStart);
                    string usernameToken = content.Substring(usernameStart, content.IndexOf(',', usernameStart) - usernameStart);
                    username = usernameToken.Substring(11, usernameToken.Length - 12);

                    int premiumStart = content.IndexOf(@"premium:", userStart);
                    string premiumToken = content.Substring(premiumStart, content.IndexOf(',', premiumStart) + 1 - premiumStart);
                    premium = premiumToken.Contains("true");

                    Console.WriteLine("premium={0} ({1}) for {2}", premium, premiumToken, username);
                    if (!premium)
                    {
                        int modStart = content.IndexOf(@"mod:", userStart);
                        string modToken = content.Substring(modStart, content.IndexOf(',', modStart) + 1 - modStart);
                        premium = modToken.Contains("true");

                        Console.WriteLine("mod={1} ({0}) for {2}", modToken, premium, username);
                    }

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
                            Console.WriteLine("Faulted task: {0}", tasks[i].Item1.Exception);
                            status = Status.CLOSEDDISCONNECT;
                            return;
                        }
                        string response = tasks[i].Item1.Result.Content.ReadAsStringAsync().Result;
                        if (response.Contains("Session ID unknown")) //HACK shouldnt be here
                        {
                            Console.WriteLine("Session ID unknown err");
                            status = Status.CLOSEDDISCONNECT;
                            return;
                        }
                        if (!ProcessMessage(response, tasks[i].Item1.Result.RequestMessage.RequestUri.ToString()))
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

        public bool SendNicknameRequest(int userId)
        {
            if (status != Status.OPEN)
            {
                Console.WriteLine("Failed to username request");
                return false;
            }

            //if (premium == false)
            //    return false;

            try
            {
                tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.GetAsync("https://pixelplace.io/back/modalsV2.php?getUsernameById=true&id=" + userId), false));
                Console.WriteLine("Requested {0}'s nickname", userId);
            }
            catch (HttpRequestException)
            {
                status = Status.CLOSEDDISCONNECT;
                return false;
            }
            return true;
        }

        public bool SendPixels(IReadOnlyCollection<IdPixel> pixels, Action<Task> callback)
        {
            if (status != Status.OPEN)
            {
                Console.WriteLine("Failed to send pixels");
                return false;
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
                Task<HttpResponseMessage> sent = client.PostAsync(urlBase + CalcTime() + "&sid=" + id, content);
                sent.ContinueWith(callback);
                tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(sent, false));
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

        public bool SendChatMessage(string message, int color, int chat)
        {
            if (status != Status.OPEN)
            {
                Console.WriteLine("Failed to send chat message: {0}", message);
                return false;
            }

            string data = String.Format("42[\"chat.message\",{{\"message\":\"{0}\",\"color\":{1},\"chat\":{2}}}]", message, color, chat);
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

        private bool ProcessMessage(string data, string requestUri)
        {
            if (data.Contains("\"success\"") && data.Contains("\"data\""))
            {
                int id = int.Parse(requestUri.Substring(requestUri.LastIndexOf("=") + 1));

                //data = "{\"success\":true,\"data\":{\"username\":\"Person" + id.ToString() + "\"}}";

                //username request response
                string suc = data.Substring(11, 5);
                if (suc == "false")
                {
                    premium = false;
                    Console.WriteLine("failed to get username, no premium");
                    return true;
                }
                if (data.Contains("User not found") || data.Contains("You need to be connected"))
                {
                    Console.WriteLine("failed to get username");
                    return true;
                }
                int startPos = data.IndexOf("\"username\":\"") + 1 + 11;
                int endPos = data.IndexOf("\"}}");
                if (endPos == -1)
                {
                    Console.WriteLine("failed to get username");
                    return true;
                }
                string name = data.Substring(startPos, endPos - startPos);
                NicknamePacket packet = new NicknamePacket() { nickname = name, id = id };
                OnEvent("nickname", packet);
                return true;
            }

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
                                pixel.socketUserId = pixelId;
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
