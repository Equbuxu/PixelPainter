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
        public bool instantPixel = false;

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ color.GetHashCode() ^ userId.GetHashCode() ^ boardId.GetHashCode() ^ instantPixel.GetHashCode();
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

    class LockBool
    {
        private bool val;
        public bool Value
        {
            set
            {
                lock (this)
                    val = value;
            }
            get
            {
                lock (this)
                    return val;
            }
        }
        public LockBool(bool value)
        {
            Value = value;
        }
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

        private readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly char[] characters = new char[] {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E',
            'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i',
            'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '-', '_'
        };
        private const string urlBase = "https://canvas.pixelplace.io:7777/socket.io/?EIO=3&transport=polling&t=";

        private readonly int boardId;
        private readonly string authKey;
        private readonly bool authenticate;

        private string authToken;
        private string phpSessId;

        public bool Premium { get; private set; }
        public string Username { get; private set; }
        public int PixelId { get; private set; }
        public Status Status { get; private set; }

        private string id;
        private Thread thread;
        private LockBool saveNextPoll = new LockBool(false);
        private LockBool abortRequested = new LockBool(false);
        private LockBool canSendRequest = new LockBool(true);
        private Task<HttpResponseMessage> pollingTask;
        private StringBuilder messageQueue = new StringBuilder();
        private List<Action<Task<HttpResponseMessage>>> callbackQueue = new List<Action<Task<HttpResponseMessage>>>();

        public event OnEventHandler OnEvent;
        public delegate void OnEventHandler(string type, EventArgs args);

        private HttpClient client;

        public SocketIO(string authKey, string authToken, string phpSessId, int boardId)
        {
            this.authKey = authKey;
            this.authToken = authToken;
            this.phpSessId = phpSessId;
            this.boardId = boardId;

            this.authenticate = true;
        }

        public SocketIO(int boardId)
        {
            this.boardId = boardId;
            this.authenticate = false;
        }

        public void Connect()
        {
            thread = new Thread(StartConnecting);
            thread.Name = "SocketIO polling";
            thread.IsBackground = true;
            thread.Start();
        }

        public void Disconnect()
        {
            if (Status == Status.OPEN || Status == Status.CONNECTING)
                abortRequested.Value = true;
            else if (Status == Status.NOTOPEN)
                throw new Exception("can't disconnect if there is no connection");
            else if (Status == Status.CLOSEDDISCONNECT || Status == Status.CLOSEDERROR)
                throw new Exception("already closed");
        }


        private Task<HttpResponseMessage> SendGetRequest(string address)
        {
            Task<HttpResponseMessage> task = client.GetAsync(address);
            task.ContinueWith(OnTaskCompletion);
            return task;
        }

        private void SendPostRequest(string address, string data, List<Action<Task<HttpResponseMessage>>> additionalCallbacks = null)
        {
            StringContent content = new StringContent(data);
            Task<HttpResponseMessage> task = client.PostAsync(address, content);
            task.ContinueWith(OnTaskCompletion);
            if (additionalCallbacks != null)
            {
                foreach (var callback in additionalCallbacks)
                    task.ContinueWith(callback);
            }
        }

        private void OnTaskCompletion(Task<HttpResponseMessage> task)
        {
            if (task.IsFaulted)
            {
                string message = task.Exception.ToString();
                if (message.Length > 120)
                    message = message.Substring(0, 120);
                Logger.Error("Faulted task for {1}: {0}", message, Username);
                Status = Status.CLOSEDDISCONNECT;
                abortRequested.Value = true;
                return;
            }

            string response = task.Result.Content.ReadAsStringAsync().Result;
            if (response.Contains("Session ID unknown")) //HACK shouldnt be here
            {
                Logger.Warning("Session ID unknown err for {0}", Username);
                Status = Status.CLOSEDDISCONNECT;
                abortRequested.Value = true;
                return;
            }

            if (!ProcessMessage(response, task.Result.RequestMessage.RequestUri.ToString()))
            {
                Status = Status.CLOSEDERROR;
                abortRequested.Value = true;
                return;
            }

            if (!abortRequested.Value && task == pollingTask)
            {
                task.Dispose();
                pollingTask = SendGetRequest(urlBase + CalcTime() + "&sid=" + id);
                canSendRequest.Value = true;
                if (messageQueue.Length > 0)
                    TrySendRequest("");
            }
            else
            {
                task.Dispose();
            }
        }

        private void StartConnecting()
        {
            if (Status != Status.NOTOPEN)
                throw new Exception("already connected");
            Status = Status.CONNECTING;

            CookieContainer cookies = new CookieContainer();
            if (authenticate)
            {
                cookies.Add(new Cookie("PHPSESSID", phpSessId, "/", "pixelplace.io"));
                cookies.Add(new Cookie("authKey", authKey, "/", "pixelplace.io"));
                cookies.Add(new Cookie("authToken", authToken, "/", "pixelplace.io"));
            }

            HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookies };
            client = new HttpClient(handler);

            ConnectSequence();
            if (Status == Status.CLOSEDDISCONNECT || Status == Status.CLOSEDERROR)
                return;

            ConnectLoop();
        }

        private void ConnectSequence()
        {
            try
            {
                HttpResponseMessage page = client.GetAsync("https://pixelplace.io/").Result;
                MakeSidRequest();

                if (authenticate)
                {
                    UpdateCookiesFromPage(page);
                    if (authToken == "deleted")
                    {
                        Status = Status.CLOSEDERROR;
                        return;
                    }
                    ParsePremiumAndUsername(page);
                    SendAuthKeyAuthToken();
                }
                else
                {
                    SendEmptyInitRequest();
                }

            }
            catch (HttpRequestException)
            {
                Status = Status.CLOSEDDISCONNECT;
            }
            catch (AggregateException)
            {
                Status = Status.CLOSEDDISCONNECT;
            }
        }

        private void UpdateCookiesFromPage(HttpResponseMessage page)
        {
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
        }

        private void ParsePremiumAndUsername(HttpResponseMessage page)
        {
            string content = page.Content.ReadAsStringAsync().Result;
            int configStart = content.IndexOf(@"var CONFIG = {");
            int userStart = content.IndexOf("user: {", configStart);

            int idStart = content.IndexOf(@"id:", userStart);
            string idToken = content.Substring(idStart, content.IndexOf(',', idStart) - idStart);
            string idString = idToken.Substring(3, idToken.Length - 3);
            if (idString == "null")
            {
                Status = Status.CLOSEDERROR;
                return;
            }
            PixelId = int.Parse(idString);

            int usernameStart = content.IndexOf(@"username:", userStart);
            string usernameToken = content.Substring(usernameStart, content.IndexOf(',', usernameStart) - usernameStart);
            Username = usernameToken.Substring(11, usernameToken.Length - 12);

            int premiumStart = content.IndexOf(@"premium:", userStart);
            string premiumToken = content.Substring(premiumStart, content.IndexOf(',', premiumStart) + 1 - premiumStart);
            Premium = premiumToken.Contains("true");

            Logger.Info("premium={0} ({1}) for {2}", Premium, premiumToken, Username);
            if (!Premium)
            {
                int modStart = content.IndexOf(@"mod:", userStart);
                string modToken = content.Substring(modStart, content.IndexOf(',', modStart) + 1 - modStart);
                Premium = modToken.Contains("true");

                Logger.Info("mod={1} ({0}) for {2}", modToken, Premium, Username);
            }
        }

        private void MakeSidRequest()
        {
            HttpResponseMessage resp = client.GetAsync(urlBase + CalcTime()).Result;
            string response = resp.Content.ReadAsStringAsync().Result;

            response = response.Remove(0, response.IndexOf('{'));
            int index = response.IndexOf('}');
            response = response.Remove(index + 1, response.Length - index - 1);
            IdResponce result = JsonConvert.DeserializeObject<IdResponce>(response);
            id = result.sid;
        }

        private void SendAuthKeyAuthToken()
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

        private void SendEmptyInitRequest()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.None;
                writer.WriteStartArray();
                writer.WriteValue("init");
                writer.WriteStartObject();
                writer.WritePropertyName("boardId");
                writer.WriteValue(boardId);
                writer.WriteEndObject();
                writer.WriteEnd();
            }
            string resultJson = sb.ToString();
            StringContent request = new StringContent((resultJson.Length + 2).ToString() + ":42" + resultJson);
            HttpResponseMessage response = client.PostAsync(urlBase + CalcTime() + "&sid=" + id, request).Result;
        }

        private void ConnectLoop()
        {
            Status = Status.OPEN;
            pollingTask = SendGetRequest(urlBase + CalcTime() + "&sid=" + id);

            while (true)
            {
                if (abortRequested.Value || Status != Status.OPEN)
                    return;

                try
                {
                    //Ping
                    SendPostRequest(urlBase + CalcTime() + "&sid=" + id, "1:2");
                }
                catch (HttpRequestException)
                {
                    Status = Status.CLOSEDDISCONNECT;
                    return;
                }
                catch (AggregateException)
                {
                    Status = Status.CLOSEDDISCONNECT;
                    return;
                }

                Thread.Sleep(25000);
            }
        }

        private void TrySendRequest(string packet, Action<Task<HttpResponseMessage>> callback = null)
        {
            lock (messageQueue)
            {
                messageQueue.Append(packet);
                if (callback != null)
                    callbackQueue.Add(callback);
                if (canSendRequest.Value)
                {
                    SendPostRequest(urlBase + CalcTime() + "&sid=" + id, messageQueue.ToString(), callbackQueue);
                    messageQueue.Clear();
                    callbackQueue.Clear();
                    canSendRequest.Value = false;
                }
            }
        }

        public bool SendNicknameRequest(int userId)
        {
            if (Status != Status.OPEN || !authenticate)
            {
                Logger.Warning("Failed to request username ({0})", Username);
                return false;
            }

            try
            {
                SendGetRequest("https://pixelplace.io/back/modalsV2.php?getUsernameById=true&id=" + userId);
                Logger.Info("Requested {0}'s nickname (using {1})", userId, Username);
            }
            catch (HttpRequestException)
            {
                Status = Status.CLOSEDDISCONNECT;
                return false;
            }
            return true;
        }

        public bool SendPixels(IReadOnlyCollection<IdPixel> pixels, Action<Task<HttpResponseMessage>> callback)
        {
            if (Status != Status.OPEN || !authenticate)
            {
                Logger.Warning("Failed to send pixels for {0}", Username);
                return false;
            }

            StringBuilder builder = new StringBuilder();
            foreach (IdPixel pixel in pixels)
            {
                string pixelMessage = String.Format("42[\"place\",{{\"x\":{0},\"y\":{1},\"c\":{2}}}]", pixel.X, pixel.Y, pixel.Color);
                builder.Append(String.Format("{0}:{1}", pixelMessage.Length, pixelMessage));
            }
            try
            {
                TrySendRequest(builder.ToString(), callback);
                saveNextPoll.Value = true;
                //SendPostRequest(urlBase + CalcTime() + "&sid=" + id, builder.ToString(), callback);
                Logger.Info("Sent {0} pixels for {1}", pixels.Count, Username);
            }
            catch (HttpRequestException)
            {
                Status = Status.CLOSEDDISCONNECT;
                return false;
            }
            return true;
        }

        public bool SendChatMessage(string message, int color, int chat)
        {
            if (Status != Status.OPEN || !authenticate)
            {
                Logger.Warning("Failed to send chat message for {1}: {0}", message, Username);
                return false;
            }

            string data = String.Format("42[\"chat.message\",{{\"message\":\"{0}\",\"color\":{1},\"chat\":{2}}}]", message, color, chat);
            string packet = String.Format("{0}:{1}", data.Length, data);
            //StringContent content = new StringContent(packet);
            try
            {
                TrySendRequest(packet);
                //SendPostRequest(urlBase + CalcTime() + "&sid=" + id, packet);
                //tasks.Add(new Tuple<Task<HttpResponseMessage>, bool>(client.PostAsync(urlBase + CalcTime() + "&sid=" + id, content), false));
            }
            catch (HttpRequestException)
            {
                Status = Status.CLOSEDDISCONNECT;
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
            bool saveNext = saveNextPoll.Value;
            if (data.Contains("\"success\"") && data.Contains("\"data\""))
                return ProcessUsernameResponce(data, requestUri);

            if (!data.Contains('['))
            {
                //"ok" responce
                return true;
            }

            List<string> packets = SplitIntoPackets(data);

            bool success = true;
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
                                pixel.instantPixel = saveNext;
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
                            Logger.Error("Error {0} for {1}", errorId, Username);
                        }
                        if (errorId == 0 || errorId == 1 || errorId == 2 || errorId == 6 || errorId == 7 || errorId == 9 || errorId == 10 || errorId == 18 || errorId == 19 || errorId == 20)
                        {
                            Logger.Error("Site error {0} for {1}", errorId, Username);
                            success = false;
                        }
                        break;
                    case "chat.user.message":
                        ChatMessagePacket message = objects[1].ToObject<ChatMessagePacket>();
                        OnEvent?.Invoke("chat.user.message", message);
                        break;
                    case "chat.system.message":
                        break;
                    case "chat.system.delete":
                        break;
                    case "chat.system.announce":
                        break;
                }
            }
            if (saveNext)
                saveNextPoll.Value = false;

            return success;
        }

        private List<string> SplitIntoPackets(string data)
        {
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

            return packets;
        }

        public bool ProcessUsernameResponce(string data, string requestUri)
        {
            int id = int.Parse(requestUri.Substring(requestUri.LastIndexOf("=") + 1));

            //username request response
            string suc = data.Substring(11, 5);
            if (suc == "false")
            {
                Premium = false;
                Logger.Error("Failed to get username, no premium for {0}", Username);
                return true;
            }
            if (data.Contains("User not found"))
            {
                Logger.Error("Failed to get username, user not found (req:{0} using:{1})", id, Username);
                return true;
            }
            else if (data.Contains("You need to be connected"))
            {
                Logger.Error("Failed to get username, you need to be connected for {0}", Username);
                return true;
            }

            int startPos = data.IndexOf("\"username\":\"") + 1 + 11;
            int endPos = data.IndexOf("\"}}");
            if (endPos == -1)
            {
                Logger.Error("Failed to get username, parsing error for {0}, response: {1}", Username, data);
                return true;
            }
            string name = data.Substring(startPos, endPos - startPos);
            NicknamePacket packet = new NicknamePacket() { nickname = name, id = id };
            OnEvent?.Invoke("nickname", packet);
            return true;
        }
    }
}
