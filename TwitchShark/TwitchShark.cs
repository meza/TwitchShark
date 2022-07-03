using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TwitchSharkName : Mod
{
    public readonly static string SETTINGS_BLACKLIST = "twitchSharkBlacklistDatastore";
    public readonly static string SETTINGS_USERNAME = "twitchUsername";
    public readonly static string SETTINGS_TOKEN = "twitchToken";
    public readonly static string SETTINGS_CHANNEL = "twitchChannel";
    public readonly static string SETTINGS_DEFAULT_SHARK_NAME = "twitchDefaultSharkName";
    public readonly static string SETTINGS_SUB_ONLY = "twitchSubOnly";
    public readonly static string SETTINGS_ANNOUNCE_TWITCH = "twitchAnnounceToTwitch";
    public readonly static string SETTINGS_ANNOUNCE_GAME = "twitchAnnounceToGame";
    public readonly static string SETTINGS_TEST_TWITCH_BUTTON = "twitchSharkTestTwitch";
    public readonly static string SETTINGS_RECONNECT_BUTTON = "twitchSharkReconnect";
    public readonly static string SETTINGS_USE_COLORS = "twitchSharkUseChatColors";
    static bool ExtraSettingsAPI_Loaded = false;
    public readonly static string DEFAULT_COLOR = "#BB7C6A";
    public static int CHANNEL_ID = 588;
    public static Messages MESSAGE_TYPE_SET_NAME = (Messages)524;
    public static TwitchSharkName Instance;
    public static System.Random rand = new System.Random();
    public string sharkCurrentlyAttacking;
    private static bool inWorld = false;
    public NameRepository names = new NameRepository();
    private Harmony harmonyInstance;
    private AssetBundle assets;

    public IEnumerator Start()
    {
        Instance = this;

        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(GetEmbeddedFileBytes("twitch-shark-name.assets"));
        yield return request;
        assets = request.assetBundle;

        harmonyInstance = new Harmony("hu.meza.TwitchShark");
        harmonyInstance.PatchAll();


        Log("Twitch Shark mod loaded");
    }

    private void Initialise(bool isTest = false)
    {
        if (Raft_Network.IsHost && !ExtraSettingsAPI_Loaded)
        {
            ErrorNotification("Twitch Shark can't start.\nThe Extra Settings Mod is missing.");
            return;
        }

        var username = ExtraSettingsAPI_GetInputValue(SETTINGS_USERNAME);
        var token = ExtraSettingsAPI_GetInputValue(SETTINGS_TOKEN);
        var channel = ExtraSettingsAPI_GetInputValue(SETTINGS_CHANNEL);

        if (token == "" || username == "" || channel == "")
        {
            Debug.Log("Missing Twitch Details. Please go to Settings");
            FindObjectOfType<HNotify>().AddNotification(HNotify.NotificationType.normal, "Missing Twitch Details. Please go to Settings", 10, HNotify.ErrorSprite);
            return;
        }

        names.Start(username, token, channel, isTest).ContinueWith(OnAsyncMethodFailed, TaskContinuationOptions.OnlyOnFaulted);
    }
    public TextMeshPro AddNametag(AI_StateMachine_Shark shark)
    {
        var potentialTextComponent = shark.GetComponentInChildren<Text>();
        var isCrowdControlShark = potentialTextComponent != null;

        var nameTag = Instantiate(assets.LoadAsset<GameObject>("Name Tag"));
        nameTag.AddComponent<Billboard>();

        nameTag.transform.SetParent(shark.transform);
        nameTag.transform.localPosition = new Vector3(0, 2f, 0);
        nameTag.transform.localRotation = Quaternion.identity;

        var text = nameTag.GetComponentInChildren<TextMeshPro>();
        if (Raft_Network.IsHost)
        {
            if (isCrowdControlShark)
            {
                text.text = potentialTextComponent.text;
                potentialTextComponent.enabled = false;
            }
            else
            {
                var user = names.Next();
                text.text = user.Username;
                text.color = GetColorFromHex(DEFAULT_COLOR);

                if (ExtraSettingsAPI_GetCheckboxState(SETTINGS_USE_COLORS))
                {
                    text.color = user.Color;
                }
            }
            Debug.Log($"Adding the name: {text.text} to the shark");
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

    override public void WorldEvent_WorldLoaded()
    {
        inWorld = true;

        if (Raft_Network.IsHost)
        {
            Initialise();
        }
        else
        {
            SuccessNotification("Twitch Shark enabled on host. Have fun!");
        }
    }

    override public void WorldEvent_WorldUnloaded()
    {
        inWorld = false;
        if (Raft_Network.IsHost)
        {
            Debug.Log("World Unloaded");
            names.Stop();
        }
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
            if (message.message.Type == MESSAGE_TYPE_SET_NAME && !Raft_Network.IsHost)
            {
                if (message.message is UpdateSharkNameMessage msg)
                {
                    var maybeShark = NetworkIDManager.GetNetworkIDFromObjectIndex<AI_NetworkBehaviour>(msg.sharkId);
                    if (maybeShark is AI_NetworkBehavior_Shark shark)
                    {
                        var nameTag = shark.stateMachineShark.GetComponentInChildren<TextMeshPro>();
                        nameTag.text = msg.name;
                    }
                }
            }
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

    public void ExtraSettingsAPI_Load()
    {
        Debug.Log("Settings loaded");

    }

    public static string ExtraSettingsAPI_GetInputValue(string SettingName) => "";
    public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;
    public static void ExtraSettingsAPI_SetDataValue(string SettingName, string subname, string value) { }
    public static void ExtraSettingsAPI_SetDataValues(string SettingName, Dictionary<string, string> values) { }
    public static string ExtraSettingsAPI_GetDataValue(string SettingName, string subname) => "";
    public static string[] ExtraSettingsAPI_GetDataNames(string SettingName) => new string[0];
    public void ExtraSettingsAPI_ButtonPress(string name) // Occurs when a settings button is clicked. "name" is set the the button's name
    {
        if (name == SETTINGS_TEST_TWITCH_BUTTON)
        {
            Initialise(true);
            return;
        }

        if (name == SETTINGS_RECONNECT_BUTTON)
        {
            if (!inWorld)
            {
                ErrorNotification("Need to be in a session to connect to Twitch");
                return;
            }

            if (!Raft_Network.IsHost)
            {
                ErrorNotification("Only the Host can connect to Twitch");
                return;
            }

            names.Stop();
            Initialise();
            return;
        }
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

}