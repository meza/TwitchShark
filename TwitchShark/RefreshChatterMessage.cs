using System;
using static Twitch;

[Serializable]
class RefreshChatterMessage : Message
{
    public TwitchChatMessage message;
    public uint originId;

    public RefreshChatterMessage(Messages type, uint origin, TwitchChatMessage message) : base(type)
    {
        this.originId = origin;
        this.message = message;
    }
}
