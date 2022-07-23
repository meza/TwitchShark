using System;

[Serializable]
class ChatterRefreshedMessage : Message
{
    public TwitchChatMessage message;

    public ChatterRefreshedMessage(Messages type, TwitchChatMessage message) : base(type)
    {
        this.message = message;
    }
}
