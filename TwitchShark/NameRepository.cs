using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Twitch;

public class NameRepository
{
    Twitch client;

    private readonly CancellationTokenSource cts = new CancellationTokenSource();
    private readonly System.Collections.Generic.HashSet<string> activeChatters = new System.Collections.Generic.HashSet<string>();
    public void Stop()
    {
        cts.Cancel();
    }
    public async Task Start(String username, String token, String channel)
    {
        client = new Twitch(username, token);

        client.Start(cts);
        
        await client.JoinChannel(channel);
        client.OnMessage += OnMessage;

    }

    private bool ShouldAddMessage(TwitchChatMessage message)
    {
        var subOnly = TwitchSharkName.ExtraSettingsAPI_GetCheckboxState("twitchSubOnly");
        if (activeChatters.Contains(message.Sender)) return false;

        if (subOnly)
        {
            if (!message.IsSub && !message.IsMod)
            {
                return false;
            }
        }

        return true;
    }
    private void OnMessage(object sender, TwitchChatMessage message)
    {
        if (!ShouldAddMessage(message)) return;

        activeChatters.Add(message.Sender);
        
        var msg = $"{message.Sender} just entered the Shark Name Pool";
        Debug.Log(msg);

        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState("twitchAnnounceToTwitch"))
        {
            client.SendMessage(message.Channel, $"@{msg}");
        }
        
        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState("twitchAnnounceToGame"))
        {
            RAPI.BroadcastChatMessage(msg);
        }

    }

    public string Next()
    {
        if (activeChatters.Count == 0)
        {
            return TwitchSharkName.ExtraSettingsAPI_GetInputValue("twitchDefaultSharkName");
        }

        var random = new System.Random();
        var array = activeChatters.ToArray();
        var result = array[random.Next(array.Length)];
        Debug.Log($"Randomly chosen the name: {result}");
        activeChatters.Remove(result);
        return result;
    }
}
