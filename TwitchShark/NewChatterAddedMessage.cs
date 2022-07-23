using System;

[Serializable]
class NewChatterAddedMessage : Message
{
    public TwitchChatMessage message;
    public uint originId;

    public NewChatterAddedMessage(Messages type, uint origin, TwitchChatMessage message) : base(type)
    {
        this.originId = origin;
        this.message = message;
    }
}
