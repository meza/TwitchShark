using System;
using System.Collections.Generic;
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
    private readonly HashSet<string> activeChatters = new HashSet<string>();
    private HashSet<string> blacklist;
    public void Stop()
    {
        cts.Cancel();
    }

    public async Task Start(String username, String token, String channel)
    {
        blacklist = new HashSet<string>(TwitchSharkName.ExtraSettingsAPI_GetDataNames(TwitchSharkName.SETTINGS_BLACKLIST));
        this.username = username;
        connectionNotification = TwitchSharkName.LoadingNotification("Connecting to Twitch");
        client = new Twitch(username, token);
        client.OnMessage += OnMessage;
        client.OnConnection += OnConnection;
        client.Start(cts);

        await client.JoinChannel(channel);
    }

    private bool ShouldAddName(TwitchChatMessage message)
    {
        if (message.Sender.ToLower() == username.ToLower()) return false;
        if (blacklist.Contains(message.Sender.ToLower())) return false;

        var subOnly = TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_SUB_ONLY);
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
            TwitchSharkName.SuccessNotification("Connected to Twitch");
            return;
        }

        TwitchSharkName.ErrorNotification("Could not connect to Twitch. Please check your settings.");
    }

    private async void OnMessage(object sender, TwitchChatMessage message)
    {
        var processedMessage = ProcessMessage(message);

        if (processedMessage.Type == CommandType.COMMAND)
        {
            if (!message.IsMod) return;

            if (processedMessage.Command == "noshark")
            {
                var firstArgument = processedMessage.Message.Split(' ')[0].ToLower();
                if (firstArgument.StartsWith("@"))
                {
                    firstArgument = firstArgument.Substring(1);
                }
                if (!blacklist.Contains(firstArgument))
                {
                    blacklist.Add(firstArgument);
                    var msg = $"{firstArgument} is now blacklisted";

                    Debug.Log(msg);
                    Notify(message.Channel, msg);

                    TwitchSharkName.ExtraSettingsAPI_SetDataValue(TwitchSharkName.SETTINGS_BLACKLIST, firstArgument, "");
                }
                else
                {
                    var msg = $"{firstArgument} is already blacklisted";
                    Debug.Log(msg);
                    Notify(message.Channel, msg);
                }
            }

            if (processedMessage.Command == "allowshark")
            {
                var firstArgument = processedMessage.Message.Split(' ')[0].ToLower();
                if (firstArgument.StartsWith("@"))
                {
                    firstArgument = firstArgument.Substring(1);
                }

                if (blacklist.Contains(firstArgument))
                {
                    blacklist.Remove(firstArgument);

                    Dictionary<string, string> persistedBlacklist = new Dictionary<string, string>();
                    foreach (var name in blacklist)
                    {
                        persistedBlacklist.Add(name, "");
                    }

                    TwitchSharkName.ExtraSettingsAPI_SetDataValues(TwitchSharkName.SETTINGS_BLACKLIST, persistedBlacklist);
                    var msg = $"{firstArgument} is now allowed to be a shark";
                    Debug.Log(msg);
                    Notify(message.Channel, msg);

                }
                else
                {
                    var msg = $"{firstArgument} is not blacklisted";
                    Debug.Log(msg);
                    Notify(message.Channel, msg);
                }
            }
        }

        if (processedMessage.Type == CommandType.REGULAR)
        {
            if (!ShouldAddName(message)) return;

            activeChatters.Add(message.Sender);

            var msg = $"{message.Sender} just entered the Shark Name Pool";

            if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_TWITCH))
            {
                await client.SendMessage(message.Channel, $"@{msg}");
            }

            if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_GAME) && TwitchSharkName.InWorld())
            {
                RAPI.BroadcastChatMessage(msg);
            }
        }

    }

    public string Next()
    {
        if (activeChatters.Count == 0)
        {
            return TwitchSharkName.ExtraSettingsAPI_GetInputValue(TwitchSharkName.SETTINGS_DEFAULT_SHARK_NAME);
        }

        var random = new System.Random();
        var array = activeChatters.ToArray();
        var result = array[random.Next(array.Length)];
        Debug.Log($"Randomly chosen the name: {result}");
        activeChatters.Remove(result);
        return result;
    }

    private ControlCommand ProcessMessage(TwitchChatMessage message)
    {
        var result = new ControlCommand
        {
            Original = message,
            Type = message.Message.StartsWith("!") ? CommandType.COMMAND : CommandType.REGULAR,
            Message = message.Message
        };

        if (result.Type == CommandType.COMMAND)
        {
            var delimPos = result.Message.IndexOf(" ");
            result.Command = result.Message.Substring(1, delimPos).TrimEnd().ToLower();
            result.Message = result.Message.Substring(delimPos).Trim();
        }

        return result;

    }

    private enum CommandType
    {
        REGULAR,
        COMMAND
    }

    private class ControlCommand
    {
        public TwitchChatMessage Original { get; set; }
        public CommandType Type { get; set; }
        public String Message { get; set; }
        public String Command { get; set; }
    }

    private async void Notify(String channel, String msg)
    {
        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_TWITCH))
        {
            await client.SendMessage(channel, msg);
        }
        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_GAME) && TwitchSharkName.InWorld())
        {
            RAPI.BroadcastChatMessage(msg);
        }
    }
}
