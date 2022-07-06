using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(PlayerStats), "Damage")]
public static class PlayerStats_Damage_Patch
{
    static void Postfix(Network_Player ___playerNetwork, PlayerStats __instance)
    {
        if (Raft_Network.IsHost && __instance.IsDead)
        {
            if (TwitchSharkName.Instance.sharkCurrentlyAttacking != null)
            {
                if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_DEBUG))
                {
                    Debug.Log($"Shark damaged: {___playerNetwork.characterSettings.Name}");
                    Debug.Log($"The health of the player is: {__instance.stat_health.Value}");
                    Debug.Log($"The isDead of the player is: {__instance.IsDead}");
                }
                //RAPI.BroadcastChatMessage($"{___playerNetwork.characterSettings.Name} was eaten by {TwitchSharkName.Instance.sharkCurrentlyAttacking}");
            }
        }
    }
}