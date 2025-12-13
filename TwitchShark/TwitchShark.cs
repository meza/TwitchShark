using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class TwitchSharkName : Mod
{
    public static string version = "VERSION";
    public static readonly string SETTINGS_BLACKLIST = "twitchSharkBlacklistDatastore";
    public static readonly string SETTINGS_USERNAME = "twitchUsername";
    public static readonly string SETTINGS_CHANNEL = "twitchChannel";
    public static readonly string SETTINGS_DEFAULT_SHARK_NAME = "twitchDefaultSharkName";
    public static readonly string SETTINGS_SUB_ONLY = "twitchSubOnly";
    public static readonly string SETTINGS_ANNOUNCE_TWITCH = "twitchAnnounceToTwitch";
    public static readonly string SETTINGS_ANNOUNCE_GAME = "twitchAnnounceToGame";
    public static readonly string SETTINGS_TEST_TWITCH_BUTTON = "twitchSharkTestTwitch";
    public static readonly string SETTINGS_AUTHORIZE_BUTTON = "twitchSharkAuthorize";
    public static readonly string SETTINGS_DISCONNECT_BUTTON = "twitchSharkDisconnect";
    public static readonly string SETTINGS_RECONNECT_BUTTON = "twitchSharkReconnect";
    public static readonly string SETTINGS_USE_COLORS = "twitchSharkUseChatColors";
    public static readonly string SETTINGS_TIMEOUT = "twitchSharkTimeout";
    public static readonly string SETTINGS_DEBUG = "twitchDebug";
    public static readonly string SETTINGS_RESET = "twitchSharkResetEntries";
    public static readonly string SETTINGS_NAME_VISIBILITY = "twitchSharkNameVisibility";
    public static readonly string SETTINGS_AUTH_DATA = "twitchAuthDatastore";
    public static readonly string AUTH_DATA_ACCESS_TOKEN_KEY = "accessToken";
    public static readonly string AUTH_DATA_REFRESH_TOKEN_KEY = "refreshToken";
    public static readonly string AUTH_DATA_CLIENT_ID_KEY = "clientId";
    public static readonly string AUTH_DATA_USERNAME_KEY = "username";
    public static readonly string AUTH_DATA_ACCESS_EXPIRY_KEY = "accessTokenExpiresAt";
    public static readonly string TWITCH_CLIENT_ID = "4fww09bygp6gzf2cq17pv20ylyzkjd";
    static bool ExtraSettingsAPI_Loaded = false;
    //public readonly static string DEFAULT_COLOR = "#BBA16A";
    public static readonly string DEFAULT_COLOR = "#FFFFFF";
    public static int CHANNEL_ID = 588;
    public static Messages MESSAGE_TYPE_SET_NAME = (Messages)524;
    public static Messages MESSAGE_TYPE_NEW_NAME_CANDIDATE = (Messages)525;
    public static Messages MESSAGE_TYPE_NEW_CHATTER = (Messages)526;
    public static Messages MESSAGE_TYPE_REFRESH_CHATTER = (Messages)527;
    public static Messages MESSAGE_TYPE_CHATTER_REMOVED = (Messages)528;
    public static Messages MESSAGE_TYPE_CHATTER_REFRESHED = (Messages)529;
    public static Messages MESSAGE_TYPE_CLEAR_NAMES = (Messages)530;
    public static TwitchSharkName Instance;
    public static System.Random rand = new System.Random();
    public string sharkCurrentlyAttacking;
    private static bool inWorld = false;
    public NameRepository names = new NameRepository();
    private Harmony harmonyInstance;
    private AssetBundle assets;
    private string previousUsername = "";
    private string previousAccessToken = "";
    private string previousRefreshToken = "";
    private string previousClientId = "";
    private string previousChannelName = "";
    private string previousAccessTokenExpiry = "";
    private bool skipAuthInputSync = false;
    private bool authorizationInProgress = false;
    private HNotification authorizationNotification;
    private static readonly TimeSpan AuthorizationButtonTimeout = TimeSpan.FromMinutes(3);

    public IEnumerator Start()
    {
        Instance = this;

        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(GetEmbeddedFileBytes("twitch-shark-name.assets"));
        yield return request;
        assets = request.assetBundle;

        harmonyInstance = new Harmony("hu.meza.TwitchShark");
        harmonyInstance.PatchAll();

        Log($"Twitch Shark mod {version} loaded");
    }

    private void Initialise(bool isTest = false)
    {
        if (!ExtraSettingsAPI_Loaded)
        {
            ErrorNotification("Twitch Shark can't start.\nThe Extra Settings Mod is missing.");
            return;
        }

        SyncAuthInputsToDataStore();

        previousUsername = GetAuthDataValue(AUTH_DATA_USERNAME_KEY).ToLower();
        previousAccessToken = GetAuthDataValue(AUTH_DATA_ACCESS_TOKEN_KEY);
        previousRefreshToken = GetAuthDataValue(AUTH_DATA_REFRESH_TOKEN_KEY);
        previousClientId = ResolveClientId();
        previousChannelName = ExtraSettingsAPI_GetInputValue(SETTINGS_CHANNEL).ToLower();
        previousAccessTokenExpiry = GetAuthDataValue(AUTH_DATA_ACCESS_EXPIRY_KEY);

        var missingInputs = new List<string>();

        if (string.IsNullOrWhiteSpace(previousChannelName))
        {
            missingInputs.Add("Target channel");
        }

        var missingAuthData = string.IsNullOrWhiteSpace(previousAccessToken) ||
                              string.IsNullOrWhiteSpace(previousClientId) ||
                              string.IsNullOrWhiteSpace(previousUsername);

        if (missingInputs.Count > 0 || missingAuthData)
        {
            var pieces = new List<string>();

            if (missingInputs.Count > 0)
            {
                pieces.Add($"Missing Twitch settings: {string.Join(", ", missingInputs)}.");
            }

            pieces.Add("Open Settings and run the Twitch authorization flow.");

            var formatted = string.Join(" ", pieces);
            Debug.Log(formatted);
            FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, formatted, 10, HNotify.ErrorSprite);
            return;
        }

        names.Start(previousUsername, previousAccessToken, previousRefreshToken, previousClientId, previousChannelName, previousAccessTokenExpiry, isTest).ContinueWith(OnAsyncMethodFailed, TaskContinuationOptions.OnlyOnFaulted);
    }

    public TextMeshPro AddNametag(AI_StateMachine_Shark shark)
    {
        var nameTag = Instantiate(assets.LoadAsset<GameObject>("Name Tag"));
        nameTag.AddComponent<Billboard>();

        nameTag.transform.SetParent(shark.transform);
        nameTag.transform.localPosition = new Vector3(0, 2f, 0);
        nameTag.transform.localRotation = Quaternion.identity;

        var text = nameTag.GetComponentInChildren<TextMeshPro>();
        text.transform.parent.gameObject.SetActive(true);
        text.outlineWidth = 0.1f;

        var layer = LayerMask.NameToLayer("Particles");
        nameTag.gameObject.SetLayerRecursivly(layer);
        text.renderer.material.shader = Shader.Find("TextMeshPro/Distance Field");

        if (ExtraSettingsAPI_GetComboboxSelectedItem(SETTINGS_NAME_VISIBILITY) == "above all")
        {
            text.renderer.material.shader = Shader.Find("TextMeshPro/Distance Field Overlay");
        }

        if (Raft_Network.IsHost)
        {
            var potentialTextComponent = shark.GetComponentInChildren<Text>();
            var isCrowdControlShark = potentialTextComponent != null;

            if (isCrowdControlShark)
            {
                text.text = potentialTextComponent.text;
                potentialTextComponent.enabled = false;
            }
            else
            {
                var entry = names.Next();
                text.text = entry.Name;
                text.color = GetColorFromHex(DEFAULT_COLOR);

                if (ExtraSettingsAPI_GetCheckboxState(SETTINGS_USE_COLORS))
                {
                    text.color = entry.Color;
                }
            }
        }

        return text;
    }

    public void OnModUnload()
    {
        harmonyInstance.UnpatchAll("hu.meza.TwitchShark");
        assets.Unload(true);
        Instance = null;
        Log("Twitch Shark Name mod unloaded");
    }

    public override void WorldEvent_WorldLoaded()
    {
        inWorld = true;

        Initialise();
    }

    public override void WorldEvent_WorldUnloaded()
    {
        inWorld = false;


        Debug.Log("World Unloaded");
        names.Stop();
        names.Reset();

    }

    public static bool InWorld()
    {
        return inWorld;
    }

    public static Color GetColorFromHex(string hex)
    {
        Color result;
        var success = ColorUtility.TryParseHtmlString(hex, out result);

        if (!success)
        {
            ColorUtility.TryParseHtmlString(TwitchSharkName.DEFAULT_COLOR, out result);
        }

        return result;
    }

    public void FixedUpdate()
    {
        var message = RAPI.ListenForNetworkMessagesOnChannel(CHANNEL_ID);

        if (message != null)
        {
            names.OnNetworkMessage(message);

            if (message.message.Type == MESSAGE_TYPE_SET_NAME && !Raft_Network.IsHost)
            {
                if (message.message is UpdateSharkNameMessage msg)
                {
                    var maybeShark = NetworkIDManager.GetNetworkIDFromObjectIndex<AI_NetworkBehaviour>(msg.sharkId);

                    if (maybeShark is AI_NetworkBehavior_Shark shark)
                    {
                        var nameTag = shark.stateMachineShark.GetComponentInChildren<TextMeshPro>();
                        nameTag.text = msg.name;

                        var succ = ColorUtility.TryParseHtmlString(msg.color, out Color c);

                        if (succ)
                        {
                            nameTag.color = c;
                        }
                        else
                        {
                            if (ExtraSettingsAPI_GetCheckboxState(SETTINGS_DEBUG))
                            {
                                Debug.Log($"Could not convert the color: {msg.color}");
                            }
                        }
                    }
                }
            }
        }
    }

    [ConsoleCommand(name: "getnameentries", docs: "lists the entries for the shark name pool [debug/emergency use only]")]
    public static void ListEnteredNames()
    {
        var entries = NameRepository.GetAllEntries();

        foreach (var entry in entries)
        {
            Debug.Log($"{entry.Value.Name} entered at {entry.Value.EnteredOn.ToString()}");
        }
    }

    [ConsoleCommand(name: "respawnshark", docs: "respawns the shark with a new name [debug/emergency use only]")]
    public static void RespawnCommand(string[] args)
    {
        KillRandomShark();
        SpawnShark();
        Debug.Log("Respawning the shark");
    }

    private static void KillRandomShark()
    {
        List<Network_Entity> entities = new List<Network_Entity>();

        foreach (AI_NetworkBehaviour entity in FindObjectsOfType<AI_NetworkBehaviour>())
            if (entity is AI_NetworkBehavior_Shark && !entity.networkEntity.IsDead)
                entities.Add(entity.networkEntity);
        Network_Entity target = entities[(int)(rand.NextDouble() * entities.Count)];
        Raft_Network network = target.Network;
        Message_NetworkEntity_Damage message = new Message_NetworkEntity_Damage(Messages.DamageEntity, network.NetworkIDManager, ComponentManager<Network_Host_Entities>.Value.ObjectIndex, target.ObjectIndex, target.stat_health.Value, target.transform.position, Vector3.up, EntityType.Environment, null);

        if (Raft_Network.IsHost)
        {
            target.Damage(message.damage, message.HitPosition, message.HitNormal, message.damageInflictorEntityType, null);
            RAPI.SendNetworkMessage(message);
        }
        else
            network.SendP2P(network.HostID, message, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
    }

    private static void SpawnShark()
    {
        if (Raft_Network.IsHost)
        {
            Network_Host_Entities entity = ComponentManager<Network_Host_Entities>.Value;
            entity.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, entity.GetSharkSpawnPosition());
        }
    }

    private static void SyncAuthInputsToDataStore()
    {
        EnsureClientIdStored();
    }

    private static string GetAuthDataValue(string key)
    {
        var storedValue = ExtraSettingsAPI_GetDataValue(SETTINGS_AUTH_DATA, key);

        if (!string.IsNullOrWhiteSpace(storedValue))
        {
            return storedValue.Trim();
        }

        return "";
    }

    private static string ResolveClientId()
    {
        EnsureClientIdStored();
        return TWITCH_CLIENT_ID;
    }

    private static bool HasStoredAuthorization()
    {
        return !string.IsNullOrWhiteSpace(GetAuthDataValue(AUTH_DATA_ACCESS_TOKEN_KEY)) &&
               !string.IsNullOrWhiteSpace(GetAuthDataValue(AUTH_DATA_USERNAME_KEY));
    }

    private static void EnsureClientIdStored()
    {
        var trimmed = TWITCH_CLIENT_ID.Trim();
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_CLIENT_ID_KEY, trimmed);
    }

    public void ExtraSettingsAPI_Load()
    {
        Debug.Log("Settings loaded");
        UpdateUsernameDisplay(GetAuthDataValue(AUTH_DATA_USERNAME_KEY));
    }

    public void ExtraSettingsAPI_SettingsOpen() // Occurs when user opens the settings menu
    {
        UpdateUsernameDisplay(GetAuthDataValue(AUTH_DATA_USERNAME_KEY));
    }

    public void ExtraSettingsAPI_SettingsClose() // Occurs when user closes the settings menu
    {
        if (skipAuthInputSync)
        {
            skipAuthInputSync = false;
        }
        else
        {
            SyncAuthInputsToDataStore();
        }

        var newUsername = GetAuthDataValue(AUTH_DATA_USERNAME_KEY).ToLower();
        var newAccessToken = GetAuthDataValue(AUTH_DATA_ACCESS_TOKEN_KEY);
        var newRefreshToken = GetAuthDataValue(AUTH_DATA_REFRESH_TOKEN_KEY);
        var newClientId = ResolveClientId();
        var newChannelName = ExtraSettingsAPI_GetInputValue(SETTINGS_CHANNEL).ToLower();

        if ((newUsername != previousUsername) || (newAccessToken != previousAccessToken) || (newRefreshToken != previousRefreshToken) || (newClientId != previousClientId) || (newChannelName != previousChannelName))
        {
            if (inWorld && Raft_Network.IsHost)
            {
                if (ExtraSettingsAPI_GetCheckboxState(SETTINGS_DEBUG))
                {
                    Debug.Log("Twitch settings have changed, reconnecting");
                }

                names.Stop();
                Initialise();
            }
        }
    }

    public bool ExtraSettingsAPI_HandleSettingVisible(string settingName, bool isInWorld)
    {
        var isAuthorized = HasStoredAuthorization();

        if (settingName == SETTINGS_AUTHORIZE_BUTTON)
        {
            return !isAuthorized;
        }

        if (settingName == SETTINGS_USERNAME || settingName == SETTINGS_DISCONNECT_BUTTON)
        {
            return isAuthorized;
        }

        return true;
    }

    public static int ExtraSettingsAPI_GetComboboxSelectedIndex(string SettingName) => -1;
    public static string ExtraSettingsAPI_GetComboboxSelectedItem(string SettingName) => "";
    public static string ExtraSettingsAPI_GetInputValue(string SettingName) => "";
    public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
    public static void ExtraSettingsAPI_SetDataValue(string SettingName, string subname, string value) { }
    public static void ExtraSettingsAPI_SetDataValues(string SettingName, Dictionary<string, string> values) { }
    public static void ExtraSettingsAPI_SetSettingText(string SettingName, string value) { }
    public static string ExtraSettingsAPI_GetDataValue(string SettingName, string subname) => "";
    public static string[] ExtraSettingsAPI_GetDataNames(string SettingName) => new string[0];
    public static void ExtraSettingsAPI_CheckSettingVisibility() { }

    public void ExtraSettingsAPI_ButtonPress(string name) // Occurs when a settings button is clicked. "name" is set the the button's name
    {
        if (name == SETTINGS_TEST_TWITCH_BUTTON)
        {
            SyncAuthInputsToDataStore();
            Initialise(true);
            return;
        }

        if (name == SETTINGS_AUTHORIZE_BUTTON)
        {
            StartCoroutine(HandleAuthorizationButtonPress());
            return;
        }

        if (name == SETTINGS_DISCONNECT_BUTTON)
        {
            HandleDisconnectButtonPress();
            return;
        }

        if (name == SETTINGS_RECONNECT_BUTTON)
        {
            if (!inWorld)
            {
                ErrorNotification("Need to be in a session to connect to Twitch");
                return;
            }

            names.Stop();
            SyncAuthInputsToDataStore();
            Initialise();
            return;
        }

        if (name == SETTINGS_RESET)
        {
            if (!Raft_Network.IsHost)
            {
                ErrorNotification("Only the host can clear the name pool");
                return;
            }

            names.Reset();
            
        }
    }

    private IEnumerator HandleAuthorizationButtonPress()
    {
        if (authorizationInProgress)
        {
            ErrorNotification("Authorization already in progress. Complete it in your browser or wait for it to time out.");
            yield break;
        }

        var clientId = ResolveClientId();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            ErrorNotification("Unable to start Twitch authorization. Please restart the mod or reinstall it.");
            yield break;
        }

        authorizationInProgress = true;
        authorizationNotification = LoadingNotification("Waiting for Twitch authorization in your browser...");
        var flow = new TwitchAuthorizationFlow(clientId);

        using (var cts = new CancellationTokenSource(AuthorizationButtonTimeout))
        {
            var task = flow.ExecuteAsync(cts.Token);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            authorizationNotification?.Close();
            authorizationNotification = null;
            authorizationInProgress = false;

            if (task.IsCanceled || task.Status == TaskStatus.Canceled)
            {
                ErrorNotification("Authorization timed out. Please try again.");
                yield break;
            }

            if (task.IsFaulted)
            {
                var root = task.Exception?.GetBaseException();
                Debug.LogError(root ?? task.Exception);
                ErrorNotification($"Authorization failed: {root?.Message ?? "See console for details."}");
                yield break;
            }

            var result = task.Result;
            PersistAuthorizedTokens(result);
        }
    }

    private void HandleDisconnectButtonPress()
    {
        if (!HasStoredAuthorization())
        {
            UpdateUsernameDisplay("");
            return;
        }

        ClearStoredAuthorization();
        names.Stop();
        SuccessNotification("Disconnected from Twitch. Run authorization again whenever you're ready.");
        UpdateUsernameDisplay("");
    }

    private void PersistAuthorizedTokens(TwitchAuthorizationResult result)
    {
        if (result == null)
        {
            return;
        }

        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_ACCESS_TOKEN_KEY, result.AccessToken ?? "");
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_REFRESH_TOKEN_KEY, result.RefreshToken ?? "");
        EnsureClientIdStored();

        var expiryValue = result.AccessTokenExpiresAt.HasValue ? result.AccessTokenExpiresAt.Value.ToString("o") : "";
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_ACCESS_EXPIRY_KEY, expiryValue);

        previousAccessToken = result.AccessToken ?? previousAccessToken;
        previousRefreshToken = result.RefreshToken ?? "";
        previousClientId = TWITCH_CLIENT_ID;
        previousAccessTokenExpiry = expiryValue;

        skipAuthInputSync = true;
        SuccessNotification("Twitch authorization complete. Credentials saved.");
        StartCoroutine(UpdateAuthorizedUserAsync(result.AccessToken));

        if (inWorld && Raft_Network.IsHost)
        {
            names.Stop();
            Initialise();
        }
    }

    private void ClearStoredAuthorization()
    {
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_ACCESS_TOKEN_KEY, "");
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_REFRESH_TOKEN_KEY, "");
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_ACCESS_EXPIRY_KEY, "");
        ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_USERNAME_KEY, "");

        previousAccessToken = "";
        previousRefreshToken = "";
        previousAccessTokenExpiry = "";
        previousUsername = "";
    }

    public static void OnAsyncMethodFailed(Task task)
    {
        Exception ex = task.Exception;
        Debug.LogError(ex);
    }

    public static HNotification LoadingNotification(string message)
    {
        return FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.spinning, message, 30, HNotify.LoadingSprite);
    }

    public static HNotification ErrorNotification(string message)
    {
        return FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, message, 10, HNotify.ErrorSprite);
    }

    public static HNotification SuccessNotification(string message)
    {
        return FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, message, 5, HNotify.CheckSprite);
    }

    public static bool IsDebug()
    {
        return TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_DEBUG);
    }

    private IEnumerator UpdateAuthorizedUserAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            yield break;
        }

        using (var request = UnityWebRequest.Get("https://id.twitch.tv/oauth2/validate"))
        {
            request.SetRequestHeader("Authorization", $"OAuth {accessToken}");
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            var failed = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError;
#else
            var failed = request.isNetworkError || request.isHttpError;
#endif

            if (failed)
            {
                Debug.LogWarning($"Failed to fetch authorized Twitch user: {request.error}");
                UpdateUsernameDisplay("");
                yield break;
            }

            var payload = request.downloadHandler.text;
            var parsed = JsonUtility.FromJson<TwitchValidationResponse>(payload);

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.login))
            {
                Debug.LogWarning("Twitch validation response missing login.");
                UpdateUsernameDisplay("");
                yield break;
            }

            var normalizedLogin = parsed.login.Trim().ToLower();
            ExtraSettingsAPI_SetDataValue(SETTINGS_AUTH_DATA, AUTH_DATA_USERNAME_KEY, normalizedLogin);
            previousUsername = normalizedLogin;
            UpdateUsernameDisplay(normalizedLogin);
        }
    }

    private void UpdateUsernameDisplay(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            ExtraSettingsAPI_SetSettingText(SETTINGS_USERNAME, "Twitch Username: Not authorized");
        }
        else
        {
            ExtraSettingsAPI_SetSettingText(SETTINGS_USERNAME, $"Twitch Username: {username}");
        }

        RefreshConnectionUiVisibility();
    }

    private void RefreshConnectionUiVisibility()
    {
        if (ExtraSettingsAPI_Loaded)
        {
            ExtraSettingsAPI_CheckSettingVisibility();
        }
    }

    [Serializable]
    private class TwitchValidationResponse
    {
        public string client_id;
        public string login;
        public string user_id;
        public int expires_in;
    }
}
