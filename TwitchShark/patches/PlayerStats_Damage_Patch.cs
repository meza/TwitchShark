using HarmonyLib;

[HarmonyPatch(typeof(PlayerStats), "Damage")]
public static class PlayerStats_Damage_Patch
{
    static void Postfix(Network_Player ___playerNetwork, PlayerStats __instance)
    {
        if (Raft_Network.IsHost && __instance.IsDead)
        {
            if (TwitchSharkName.Instance.sharkCurrentlyAttacking != null)
            {
                RAPI.BroadcastChatMessage($"{___playerNetwork.characterSettings.Name} was eaten by {TwitchSharkName.Instance.sharkCurrentlyAttacking}");
            }
        }
    }
}