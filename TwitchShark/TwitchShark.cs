using HarmonyLib;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TwitchSharkName: Mod
{
    public static int CHANNEL_ID = 588;
    public static Messages MESSAGE_TYPE_SET_NAME = (Messages)524;
    public static TwitchSharkName Instance;
    public static System.Random rand = new System.Random();
    public string SharkCurrentlyAttacking;
    public NameRepository Names;

    private Harmony harmonyInstance;
    private AssetBundle assets;

    public IEnumerator Start()
    {

        Instance = this;

        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(GetEmbeddedFileBytes("twitch-shark-name.assets"));
        yield return request;
        assets = request.assetBundle;

        harmonyInstance = new Harmony("hu.meza.TwitchSharkName");
        harmonyInstance.PatchAll();

        Names = new NameRepository();
        Log("Twitch Shark mod loaded");
    }

    public void ExtraSettingsAPI_Load()
    {
        Debug.Log("Settings loaded");
        var username = ExtraSettingsAPI_GetInputValue("twitchUsername");
        var token = ExtraSettingsAPI_GetInputValue("twitchToken");
        var channel = ExtraSettingsAPI_GetInputValue("twitchChannel");

        Names.Start(username, token, channel);
    }

    public void OnModUnload()
    {
        Names.Stop();
        harmonyInstance.UnpatchAll("hu.meza.TwitchSharkName");
        assets.Unload(true);
        Instance = null;
        Log("Twitch Shark Name mod unloaded");
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

    [ConsoleCommand(name: "respawnshark", docs: "respawns the shark with a new name")]
    public void RespawnCommand(string[] args)
    {
        KillRandomShark();
        SpawnShark();
        Debug.Log("Respawning the shark");
    }

    private void KillRandomShark()
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

    private void SpawnShark()
    {
        if (Raft_Network.IsHost)
        {
            Network_Host_Entities entity = ComponentManager<Network_Host_Entities>.Value;
            entity.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, entity.GetSharkSpawnPosition());
        }
    }


    public TextMeshPro AddNametag(AI_StateMachine_Shark shark)
    {
        var nameTag = Instantiate(assets.LoadAsset<GameObject>("Name Tag"));
        nameTag.AddComponent<Billboard>();

        nameTag.transform.SetParent(shark.transform);
        nameTag.transform.localPosition = new Vector3(0, 2f, 0);
        nameTag.transform.localRotation = Quaternion.identity;

        var text = nameTag.GetComponentInChildren<TextMeshPro>();
        if (Raft_Network.IsHost)
        {
            text.text = Names.Next();
            Debug.Log($"Adding the name: {text.text} to the shark");
        }

        return text;
    }

    public static string ExtraSettingsAPI_GetInputValue(string SettingName) => "";
    public static bool ExtraSettingsAPI_GetCheckboxState(string SettingName) => false;

}