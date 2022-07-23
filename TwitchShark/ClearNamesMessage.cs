using System;

[Serializable]
class ClearNamesMessage : Message
{
    public TwitchChatMessage message;
    public uint originId;

    public ClearNamesMessage(Messages type) : base(type)
    {}
}
