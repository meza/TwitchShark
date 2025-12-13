using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
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
    private string channel;
    private string accessToken;
    private string refreshToken;
    private string clientId;
    private DateTime? accessTokenExpiresAt;
    private readonly SemaphoreSlim tokenRefreshLock = new SemaphoreSlim(1, 1);
    private const string TokenRefreshUrl = "https://id.twitch.tv/oauth2/token";

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

            if (cts != null && cts.Token.CanBeCanceled)
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
        finally
        {
            if (client != null)
            {
                client.OnMessage -= OnMessage;
                client.OnConnection -= OnConnection;
                client = null;
            }

            cts = null;
        }
    }

    public async Task Start(String username, String accessToken, String refreshToken, String clientId, String channel, String accessTokenExpiry, bool isTest = false)
    {
        this.isTest = isTest;
        blacklist = new HashSet<string>(TwitchSharkName.ExtraSettingsAPI_GetDataNames(TwitchSharkName.SETTINGS_BLACKLIST));
        this.username = username;
        this.channel = channel;
        this.accessToken = accessToken?.Trim();
        this.refreshToken = refreshToken?.Trim();
        this.clientId = clientId?.Trim();
        accessTokenExpiresAt = ParseAccessTokenExpiry(accessTokenExpiry);

        if (!await EnsureTokenFreshnessAsync())
        {
            TwitchSharkName.ErrorNotification("Unable to refresh Twitch credentials. Please update your tokens.");
            return;
        }

        await ConnectToTwitchAsync();
    }

    private DateTime? ParseAccessTokenExpiry(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private async Task<bool> EnsureTokenFreshnessAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Debug.LogWarning("Cannot connect to Twitch because no access token is available.");
            return false;
        }

        if (accessTokenExpiresAt.HasValue && accessTokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(1))
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Debug.LogWarning("Access token is expiring soon but no refresh token is available. Please re-authorize Twitch Shark.");
                return true;
            }

            return await RefreshAccessTokenAsync();
        }

        return true;
    }

    private async Task ConnectToTwitchAsync()
    {
        var msg = isTest ? "Testing Twitch Connection" : "Connecting to Twitch";

        if (connectionNotification != null)
        {
            try
            {
                connectionNotification.Close();
            }
            catch (Exception ex)
            {
                if (TwitchSharkName.IsDebug())
                {
                    Debug.LogWarning($"Failed to close previous notification: {ex}");
                }
            }
            finally
            {
                connectionNotification = null;
            }
        }

        connectionNotification = TwitchSharkName.LoadingNotification(msg);
        client = new Twitch(username, PrepareIrcToken(accessToken));
        client.OnMessage += OnMessage;
        client.OnConnection += OnConnection;
        cts = new CancellationTokenSource();
        client.Start(cts);

        await client.JoinChannel(channel);
    }

    private static string PrepareIrcToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase) ? token : $"oauth:{token}";
    }

    private async Task RestartClientAsync()
    {
        Stop();

        if (!await EnsureTokenFreshnessAsync())
        {
            TwitchSharkName.ErrorNotification("Unable to refresh Twitch credentials. Please update your tokens.");
            return;
        }

        await ConnectToTwitchAsync();
    }

    private async Task<bool> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientId))
        {
            Debug.LogWarning("Cannot refresh Twitch token because refresh token or client ID is missing.");
            return false;
        }

        await tokenRefreshLock.WaitAsync();

        try
        {
            var payload = await SendRefreshRequestAsync();

            if (payload == null)
            {
                return false;
            }

            var parsed = JsonUtility.FromJson<TwitchTokenResponse>(payload);

            if (parsed == null || string.IsNullOrEmpty(parsed.access_token))
            {
                Debug.LogError("Received invalid Twitch token refresh response.");
                return false;
            }

            accessToken = parsed.access_token;

            if (!string.IsNullOrEmpty(parsed.refresh_token))
            {
                refreshToken = parsed.refresh_token;
            }

            if (parsed.expires_in > 0)
            {
                accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(parsed.expires_in);
            }

            PersistAuthTokens();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while refreshing Twitch token: {ex}");
            return false;
        }
        finally
        {
            tokenRefreshLock.Release();
        }
    }

    private void PersistAuthTokens()
    {
        TwitchSharkName.ExtraSettingsAPI_SetDataValue(TwitchSharkName.SETTINGS_AUTH_DATA, TwitchSharkName.AUTH_DATA_ACCESS_TOKEN_KEY, accessToken ?? "");
        TwitchSharkName.ExtraSettingsAPI_SetDataValue(TwitchSharkName.SETTINGS_AUTH_DATA, TwitchSharkName.AUTH_DATA_REFRESH_TOKEN_KEY, refreshToken ?? "");
        TwitchSharkName.ExtraSettingsAPI_SetDataValue(TwitchSharkName.SETTINGS_AUTH_DATA, TwitchSharkName.AUTH_DATA_CLIENT_ID_KEY, clientId ?? "");

        var expiryValue = accessTokenExpiresAt.HasValue ? accessTokenExpiresAt.Value.ToString("o") : "";
        TwitchSharkName.ExtraSettingsAPI_SetDataValue(TwitchSharkName.SETTINGS_AUTH_DATA, TwitchSharkName.AUTH_DATA_ACCESS_EXPIRY_KEY, expiryValue);
    }

    private async Task<string> SendRefreshRequestAsync()
    {
        var form = new WWWForm();
        form.AddField("client_id", clientId);
        form.AddField("grant_type", "refresh_token");
        form.AddField("refresh_token", refreshToken);

        using (var request = UnityWebRequest.Post(TokenRefreshUrl, form))
        {
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            var asyncOp = request.SendWebRequest();

            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError($"Failed to refresh Twitch token ({request.responseCode}): {request.error}");
                return null;
            }

            return request.downloadHandler.text;
        }
    }

    private async Task<bool> TryRecoverFromAuthenticationFailureAsync()
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            TwitchSharkName.ErrorNotification("Twitch authorization expired. Please open Settings and re-authorize.");
            return false;
        }

        if (!await RefreshAccessTokenAsync())
        {
            return false;
        }

        TwitchSharkName.SuccessNotification("Twitch token refreshed. Reconnecting...");
        await RestartClientAsync();
        return true;
    }

    [Serializable]
    private class TwitchTokenResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
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

        if (await TryRecoverFromAuthenticationFailureAsync())
        {
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
