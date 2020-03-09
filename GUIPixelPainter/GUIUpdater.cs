using System;
using System.Collections.Generic;
using System.Drawing;

namespace GUIPixelPainter
{
    public enum ChatType
    {
        Local, Global, Guild, Whispers
    }

    public class GUIEvent
    {

    }

    public class ClearQueuesGUIEvent : GUIEvent
    {

    }

    public class ChatMessageGUIEvent : GUIEvent
    {
        public ChatMessageGUIEvent(string message, Guid userId, Color color, int chat)
        {
            Message = message;
            UserId = userId;
            Color = color;
            Chat = chat;
        }
        public string Message { get; }
        public Guid UserId { get; }
        public Color Color { get; }
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

        public void PushEvent(string type, EventArgs args)
        {
            try
            {
                App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProcessEvent(type, args);
                }
                );
            }
            catch (NullReferenceException) { }
        }

        public void Update()
        {
            Manager.Update(events);
            events.Clear();
        }

        public void ProcessEvent(string type, EventArgs args)
        {
            if (type == "chat.user.message")
            {
                var message = args as ChatMessagePacket;

                if (message.chat != "0" && message.chat != DataExchange.CanvasId.ToString())
                    return;

                string formatted = String.Format("{0}: ", message.username);
                if (!String.IsNullOrWhiteSpace(message.guild))
                    formatted = formatted.Insert(0, String.Format("<{0}>", message.guild));
                if (message.admin)
                    formatted = formatted.Insert(0, "[🔧]");
                if (message.mod)
                    formatted = formatted.Insert(0, "[🔨]");
                if (message.icon == "mvp-moderator")
                    formatted = formatted.Insert(0, "[mvp]");
                if (message.premium)
                    formatted = formatted.Insert(0, "[💎]");
                if (message.boardId != DataExchange.CanvasId)
                    formatted = formatted.Insert(0, "[" + message.boardId.ToString() + "]");

                int boardId = message.boardId;
                if (!palette.ContainsKey(boardId))
                    boardId = 7;
                Color color = palette[boardId][message.color];

                ChatType chatType = ChatType.Global;
                switch (message.chatType)
                {
                    case "channel":
                        if (message.chat == "0")
                            chatType = ChatType.Global;
                        else
                            chatType = ChatType.Local;
                        break;
                    case "guild":
                        chatType = ChatType.Guild;
                        break;
                    case "whisper":
                        chatType = ChatType.Whispers;
                        break;
                }

                DataExchange.PushChatMessage(formatted, message.message, chatType, System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            }
            else if (type == "pixels")
            {
                PixelPacket pixel = args as PixelPacket;
                int boardId = pixel.boardId;
                if (!palette.ContainsKey(boardId))
                    boardId = 7;
                Color actualColor = palette[boardId][pixel.color];
                DataExchange.PushPixel(pixel.x, pixel.y, actualColor, pixel.boardId, pixel.userId, pixel.instantPixel);
            }
            else if (type == "manager.status")
            {
                UserStatusData data = args as UserStatusData;
                DataExchange.PushUserStatus(data);
            }
            else if (type == "manager.taskenable")
            {
                TaskEnableStateData data = args as TaskEnableStateData;
                DataExchange.PushTaskEnabledState(data);
            }
            else if (type == "nickname")
            {
                NicknamePacket data = args as NicknamePacket;
                DataExchange.PushNewUsername(data.id, data.nickname);
            }
            else if (type == "tokens")
            {
                TokenPacket data = args as TokenPacket;
                DataExchange.PushTokens(data.phpSessId, data.authToken, data.id);
            }
        }
    }
}
