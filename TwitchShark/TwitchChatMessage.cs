using System;

[Serializable]
public class TwitchChatMessage : EventArgs
{
    public DateTime DateTime { get; set; }
    public TwitchUser Sender { get; set; }
    public string Message { get; set; }
    public string Channel { get; set; }
    public bool IsSub { get; set; }
    public bool IsMod { get; set; }
}

