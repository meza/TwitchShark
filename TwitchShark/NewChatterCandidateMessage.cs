using System;

[Serializable]
class NewChatterCandidateMessage : Message
{
    public TwitchChatMessage message;
    public uint originId;

    public NewChatterCandidateMessage(Messages type, uint origin, TwitchChatMessage message) : base(type)
    {
        this.originId = origin;
        this.message = message;
    }
}
