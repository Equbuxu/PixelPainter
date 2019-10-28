using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class GUIEvent
    {

    }

    public class ChatMessageGUIEvent : GUIEvent
    {
        public ChatMessageGUIEvent(string message, Guid userId, int color, int chat)
        {
            Message = message;
            UserId = userId;
            Color = color;
            Chat = chat;
        }
        public string Message { get; }
        public Guid UserId { get; }
        public int Color { get; }
        public int Chat { get; }
    }

    public class GUIUpdater
    {
        public GUIDataExchange DataExchange { get; set; }
        public UserManager Manager { get; set; }

        private List<GUIEvent> events = new List<GUIEvent>();
        private Dictionary<int, Dictionary<int, System.Drawing.Color>> palette;

        public GUIUpdater(Dictionary<int, Dictionary<int, System.Drawing.Color>> palette)
        {
            this.palette = palette;
        }

        public void AddEvent(GUIEvent @event)
        {
            events.Add(@event);
        }

        public void Update()
        {
            var newEvents = Manager.Update(events);
            events.Clear();

            foreach (Tuple<string, EventArgs> eventTuple in newEvents)
            {
                if (eventTuple.Item1 == "chat.user.message")
                {
                    var message = eventTuple.Item2 as ChatMessagePacket;

                    if (message.chat != 0 && message.chat != DataExchange.CanvasId)
                        continue;

                    string formatted = String.Format("{0}: {1}", message.username, message.message);
                    if (!String.IsNullOrWhiteSpace(message.guild))
                        formatted = formatted.Insert(0, String.Format("<{0}>", message.guild));
                    if (message.admin)
                        formatted = formatted.Insert(0, "[🔧]");
                    if (message.mod)
                        formatted = formatted.Insert(0, "[🔨]");
                    if (message.premium)
                        formatted = formatted.Insert(0, "[💎]");
                    if (message.boardId != DataExchange.CanvasId)
                        formatted = formatted.Insert(0, "[" + message.boardId.ToString() + "]");

                    int boardId = message.boardId;
                    if (!palette.ContainsKey(boardId))
                        boardId = 7;
                    Color color = palette[boardId][message.color];
                    DataExchange.PushChatMessage(formatted, message.chat != 0, System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
                }
                else if (eventTuple.Item1 == "pixels")
                {
                    PixelPacket pixel = eventTuple.Item2 as PixelPacket;
                    int boardId = pixel.boardId;
                    if (!palette.ContainsKey(boardId))
                        boardId = 7;
                    Color actualColor = palette[boardId][pixel.color];
                    DataExchange.PushPixel(pixel.x, pixel.y, actualColor, pixel.boardId, pixel.userId, false); //TODO replace false with proper user comparison
                }
                else if (eventTuple.Item1 == "manager.status")
                {
                    UserStatusData data = eventTuple.Item2 as UserStatusData;
                    DataExchange.PushUserStatus(data);
                }
                else if (eventTuple.Item1 == "manager.taskenable")
                {
                    TaskEnableStateData data = eventTuple.Item2 as TaskEnableStateData;
                    DataExchange.PushTaskEnabledState(data);
                }
                else if (eventTuple.Item1 == "nickname")
                {
                    NicknamePacket data = eventTuple.Item2 as NicknamePacket;
                    DataExchange.PushNewUsername(data.id, data.nickname);
                }
                else if (eventTuple.Item1 == "tokens")
                {
                    TokenPacket data = eventTuple.Item2 as TokenPacket;
                    DataExchange.PushTokens(data.phpSessId, data.authToken, data.id);
                }
            }
        }
    }
}
