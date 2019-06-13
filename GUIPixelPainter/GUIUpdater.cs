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
        public ChatMessageGUIEvent(string message, Guid userId, int color)
        {
            Message = message;
            UserId = userId;
            Color = color;
        }
        public string Message { get; }
        public Guid UserId { get; }
        public int Color { get; }
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
                    string formatted;
                    if (String.IsNullOrWhiteSpace(message.guild))
                        formatted = String.Format("{0}: {1}", message.username, message.message);
                    else
                        formatted = String.Format("<{0}>{1}: {2}", message.guild, message.username, message.message);
                    DataExchange.PushChatMessage(formatted);
                }
                else if (eventTuple.Item1 == "pixels")
                {
                    PixelPacket pixel = eventTuple.Item2 as PixelPacket;
                    int boardId = pixel.boardId;
                    if (!palette.ContainsKey(boardId))
                        boardId = 7;
                    Color actualColor = palette[boardId][pixel.color];
                    DataExchange.PushPixel(pixel.x, pixel.y, actualColor, pixel.boardId);
                }
            }
        }
    }
}
