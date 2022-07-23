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
    private CancellationTokenSource cts;
    private static readonly Dictionary<string, NameEntry> activeChattersWithColours = new Dictionary<string, NameEntry>();
    private HashSet<string> blacklist;
    private bool isTest = false;
    private String lastName = "";

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

    public void Stop()
    {
        try
        {
            Debug.Log("Stop requested");

            if (cts.Token.CanBeCanceled)
            {
                cts.Cancel();
            }
        }
        catch (ObjectDisposedException e)
        {
            if (TwitchSharkName.IsDebug())
            {
                Debug.Log("Already cancelled, no need to recancel");
            }
        }
    }

    public async Task Start(String username, String token, String channel, bool isTest = false)
    {
        this.isTest = isTest;
        blacklist = new HashSet<string>(TwitchSharkName.ExtraSettingsAPI_GetDataNames(TwitchSharkName.SETTINGS_BLACKLIST));
        this.username = username;
        var msg = isTest ? "Testing Twitch Connection" : "Connecting to Twitch";
        connectionNotification = TwitchSharkName.LoadingNotification(msg);
        client = new Twitch(username, token);
        client.OnMessage += OnMessage;
        client.OnConnection += OnConnection;
        cts = new CancellationTokenSource();
        client.Start(cts);

        await client.JoinChannel(channel);
    }

    public void Reset()
    {
        if (Raft_Network.IsHost)
        {
            RAPI.SendNetworkMessage(new ClearNamesMessage(TwitchSharkName.MESSAGE_TYPE_CLEAR_NAMES), TwitchSharkName.CHANNEL_ID);
        }
        activeChattersWithColours.Clear();
        if (TwitchSharkName.IsDebug())
        {
            Debug.Log("The entries have been cleared");
        }
        TwitchSharkName.SuccessNotification("Entries have been cleared.\nA new pool has been opened!");
    }

    public void OnNetworkMessage(NetworkMessage message)
    {

        // New Chatter From Clients
        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_NEW_NAME_CANDIDATE && Raft_Network.IsHost)
        {
            if (message.message is NewChatterCandidateMessage msg)
            {
                uint originPlayer = msg.originId;
                AddClientChatter(msg.message, originPlayer);

            }
        }

        // Chatter Added message from the Host
        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_NEW_CHATTER && !Raft_Network.IsHost)
        {
            if (message.message is NewChatterAddedMessage msg)
            {
                if (TwitchSharkName.IsDebug())
                {
                    Debug.Log("Received the following message:");
                    Debug.Log($"username: {msg.message.Sender.Username} color: {msg.message.Sender.Color} originId: {msg.originId} date: {msg.message.DateTime}");
                }
                StoreName(msg.message, msg.originId);
            }
        }

        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_REFRESH_CHATTER && Raft_Network.IsHost)
        {
            if (message.message is RefreshChatterMessage msg)
            {
                UpdateTime(msg.message);
            }
        }

        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_CHATTER_REMOVED && !Raft_Network.IsHost)
        {
            if (message.message is ChatterRemovedMessage msg)
            {
                RemoveName(msg.username);
            }
        }

        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_CHATTER_REFRESHED && !Raft_Network.IsHost)
        {
            if (message.message is ChatterRefreshedMessage msg)
            {
                UpdateClientTime(msg.message);
            }
        }

        if (message.message.Type == TwitchSharkName.MESSAGE_TYPE_CLEAR_NAMES && !Raft_Network.IsHost)
        {
            if (message.message is ClearNamesMessage msg)
            {
                Reset();
            }
        }
    }

    public static Dictionary<string, NameEntry> GetAllEntries()
    {
        return activeChattersWithColours;
    }

    private bool IsEntry(TwitchChatMessage message)
    {
        return activeChattersWithColours.Keys.Contains(message.Sender.Username);
    }

    private bool IsEligible(TwitchChatMessage message)
    {
        if (message.Sender.Username.ToLower() == username.ToLower()) return false;

        if (blacklist.Contains(message.Sender.Username.ToLower())) return false;

        return true;
    }

    private bool ShouldAddName(TwitchChatMessage message)
    {
        if (!IsEligible(message)) return false;

        var subOnly = TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_SUB_ONLY);

        if (IsEntry(message)) return false;

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

            if (isTest)
            {
                Debug.Log("Test successful");
                Stop();
            }

            return;
        }

        TwitchSharkName.ErrorNotification("Could not connect to Twitch. Please check your settings.");
    }

    private void UpdateTime(TwitchChatMessage message)
    {
        if (IsEligible(message))
        {
            if (!IsEntry(message)) return;
            activeChattersWithColours[message.Sender.Username].EnteredOn = message.DateTime;

            RAPI.SendNetworkMessage(new ChatterRefreshedMessage(TwitchSharkName.MESSAGE_TYPE_CHATTER_REFRESHED, message), TwitchSharkName.CHANNEL_ID);

        }
    }

    private void UpdateClientTime(TwitchChatMessage message)
    {
        if (!IsEntry(message))
        {
            StoreName(message, 0);
        }
        activeChattersWithColours[message.Sender.Username].EnteredOn = message.DateTime;
    }

    private async void NotifyChatter(TwitchChatMessage message, uint origin)
    {
        if (origin != RAPI.GetLocalPlayer().ObjectIndex) return;

        var msg = $"{message.Sender.Username} just entered the Shark Name Pool";

        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_TWITCH))
        {
            await client.SendMessage(message.Channel, $"@{msg}");
        }

        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_ANNOUNCE_GAME) && TwitchSharkName.InWorld() && Raft_Network.IsHost)
        {
            RAPI.BroadcastChatMessage(msg);
        }
    }

    private void StoreName(TwitchChatMessage message, uint origin)
    {
        activeChattersWithColours.Add(message.Sender.Username, new NameEntry
        {
            Color = TwitchSharkName.GetColorFromHex(message.Sender.Color),
            Name = message.Sender.Username,
            EnteredOn = message.DateTime
        });
        NotifyChatter(message, origin);
    }

    private async void AddClientChatter(TwitchChatMessage message, uint origin)
    {
        if (!IsEligible(message)) return;
        if (IsEntry(message))
        {
            UpdateTime(message);
            return;
        }
        StoreName(message, origin);
        RAPI.SendNetworkMessage(new NewChatterAddedMessage(TwitchSharkName.MESSAGE_TYPE_NEW_CHATTER, origin, message), TwitchSharkName.CHANNEL_ID);

    }

    private async void AddHostChatter(TwitchChatMessage message)
    {
        if (!ShouldAddName(message))
        {
            if (IsEntry(message))
            {
                UpdateTime(message);
            }
            return;
        };
        uint origin = RAPI.GetLocalPlayer().ObjectIndex;

        StoreName(message, origin);
        RAPI.SendNetworkMessage(new NewChatterAddedMessage(TwitchSharkName.MESSAGE_TYPE_NEW_CHATTER, origin, message), TwitchSharkName.CHANNEL_ID);

    }
    private async void ProcessName(TwitchChatMessage message, Network_Player origin = null)
    {

        if (!Raft_Network.IsHost) // if client
        {
            if (origin == null)
            {
                origin = RAPI.GetLocalPlayer();
            }

            if (!ShouldAddName(message))
            {
                if (IsEntry(message))
                {
                    RAPI.SendNetworkMessage(new RefreshChatterMessage(TwitchSharkName.MESSAGE_TYPE_REFRESH_CHATTER, origin.ObjectIndex, message), TwitchSharkName.CHANNEL_ID);
                }
                return;
            };

            RAPI.SendNetworkMessage(new NewChatterCandidateMessage(TwitchSharkName.MESSAGE_TYPE_NEW_NAME_CANDIDATE, origin.ObjectIndex, message), TwitchSharkName.CHANNEL_ID);
            return;
        }

        // if is host
        AddHostChatter(message);
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
            ProcessName(message);
        }
    }

    public NameEntry Next()
    {
        if (activeChattersWithColours.Count == 0)
        {
            return new NameEntry
            {
                Name = TwitchSharkName.ExtraSettingsAPI_GetInputValue(TwitchSharkName.SETTINGS_DEFAULT_SHARK_NAME),
                Color = TwitchSharkName.GetColorFromHex(TwitchSharkName.DEFAULT_COLOR),
                EnteredOn = DateTime.UtcNow
            };
        }

        var random = new System.Random();
        var array = activeChattersWithColours.Keys.ToArray();
        var username = array[random.Next(array.Length)];
        var entry = activeChattersWithColours[username];

        if (HasEntryTimedOut(entry))
        {
            if (TwitchSharkName.IsDebug())
            {
                Debug.Log($"{username}'s entry has timed out. Removing them from the list");
            }

            RemoveName(username);
            return Next();
        }

        if (lastName == username && activeChattersWithColours.Count > 1)
        {
            if (TwitchSharkName.IsDebug())
            {
                Debug.Log($"{username} was the previous shark. Trying someone new");
            }
            return Next();
        }

        Debug.Log($"Randomly chosen the name: {username}");
        RemoveName(username);
        lastName = username;
        return entry;
    }

    public void RemoveName(String username)
    {
        activeChattersWithColours.Remove(username);
        if (Raft_Network.IsHost)
        {
            RAPI.SendNetworkMessage(new ChatterRemovedMessage(TwitchSharkName.MESSAGE_TYPE_CHATTER_REMOVED, username), TwitchSharkName.CHANNEL_ID);
            if (TwitchSharkName.IsDebug())
            {
                Debug.Log($"Removing {username}'s entry. Sending message");
            }
        }
        else
        {
            if (TwitchSharkName.IsDebug())
            {
                Debug.Log($"Host asked us to remove {username}'s entry.");
            }
        }
    }

    private bool HasEntryTimedOut(NameEntry entry)
    {
        var timeout = TwitchSharkName.ExtraSettingsAPI_GetComboboxSelectedItem(TwitchSharkName.SETTINGS_TIMEOUT).ToLower();

        if (timeout == "never") return false;

        switch (timeout)
        {
            case "5 minutes":
                return entry.EnteredOn.AddMinutes(5) < DateTime.UtcNow;
            case "10 minutes":
                return entry.EnteredOn.AddMinutes(10) < DateTime.UtcNow;
            case "15 minutes":
                return entry.EnteredOn.AddMinutes(15) < DateTime.UtcNow;
            case "30 minutes":
                return entry.EnteredOn.AddMinutes(30) < DateTime.UtcNow;
            case "1 hour":
                return entry.EnteredOn.AddHours(1) < DateTime.UtcNow;
            case "2 hours":
                return entry.EnteredOn.AddHours(2) < DateTime.UtcNow;
            case "4 hours":
                return entry.EnteredOn.AddHours(4) < DateTime.UtcNow;
            default:
                return false;
        }
    }

    private ControlCommand ProcessMessage(TwitchChatMessage message)
    {
        var result = new ControlCommand
        {
            Original = message,
            Type = message.Message.StartsWith("!") ? CommandType.COMMAND : CommandType.REGULAR,
            Message = message.Message
        };

        if (result.Type == CommandType.COMMAND && message.Message.Length == 1)
        {
            //If the message is just a single !, disregard it as a command
            result.Type = CommandType.REGULAR;
            return result;
        }

        if (result.Type == CommandType.COMMAND)
        {
            result.Message = "";
            result.Command = message.Message.Substring(1).TrimEnd().ToLower();

            var delimPos = message.Message.IndexOf(" ");

            if (delimPos >= 0)
            {
                result.Command = message.Message.Substring(1, delimPos).TrimEnd().ToLower();
                result.Message = message.Message.Substring(delimPos).Trim();
            }
        }

        return result;
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
