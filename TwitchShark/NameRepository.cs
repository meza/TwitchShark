using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Twitch;

public class NameRepository
{
    Twitch client;
    private string username;
    private HNotification connectionNotification;
    private readonly CancellationTokenSource cts = new CancellationTokenSource();
    private readonly System.Collections.Generic.HashSet<string> activeChatters = new System.Collections.Generic.HashSet<string>();
    public void Stop()
    {
        cts.Cancel();
    }
    public async Task Start(String username, String token, String channel)
    {
        this.username = username;
        connectionNotification = TwitchShark.LoadingNotification("Connecting to Twitch");
        client = new Twitch(username, token);
        client.OnMessage += OnMessage;
        client.OnConnection += OnConnection;

        client.Start(cts);

        await client.JoinChannel(channel);


    }

    private bool ShouldAddMessage(TwitchChatMessage message)
    {
        if (message.Sender.ToLower() == username.ToLower()) return false;

        var subOnly = TwitchShark.ExtraSettingsAPI_GetCheckboxState(TwitchShark.SETTINGS_SUB_ONLY);
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

    private async void OnConnection(object sender, TwitchConnection connection)
    {
        connectionNotification.Close();
        if (connection.Success == true)
        {
            TwitchShark.SuccessNotification("Connected to Twitch");
            return;
        }

        TwitchShark.ErrorNotification("Could not connect to Twitch. Please check your settings.");
    }
    private async void OnMessage(object sender, TwitchChatMessage message)
    {
        if (!ShouldAddMessage(message)) return;

        activeChatters.Add(message.Sender);

        var msg = $"{message.Sender} just entered the Shark Name Pool";

        if (TwitchShark.ExtraSettingsAPI_GetCheckboxState(TwitchShark.SETTINGS_ANNOUNCE_TWITCH))
        {
            await client.SendMessage(message.Channel, $"@{msg}");
        }

        if (TwitchShark.ExtraSettingsAPI_GetCheckboxState(TwitchShark.SETTINGS_ANNOUNCE_GAME) && TwitchShark.InWorld())
        {
            RAPI.BroadcastChatMessage(msg);
        }

    }

    public string Next()
    {
        if (activeChatters.Count == 0)
        {
            return TwitchShark.ExtraSettingsAPI_GetInputValue(TwitchShark.SETTINGS_DEFAULT_SHARK_NAME);
        }

        var random = new System.Random();
        var array = activeChatters.ToArray();
        var result = array[random.Next(array.Length)];
        Debug.Log($"Randomly chosen the name: {result}");
        activeChatters.Remove(result);
        return result;
    }
}
