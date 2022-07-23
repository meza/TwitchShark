using System;

[Serializable]
class ChatterRemovedMessage : Message
{
    public String username;

    public ChatterRemovedMessage(Messages type, String username) : base(type)
    {
        this.username = username;
    }
}
