﻿using HarmonyLib;
using TMPro;

[HarmonyPatch(typeof(AI_NetworkBehavior_Shark), "Serialize_Update_Animal")]
public static class AI_NetworkBehavior_Shark_Serialize_Update_Animal_Patch
{
    static void Postfix(AI_NetworkBehavior_Shark __instance)
    {
        if (Raft_Network.IsHost)
        {
            var nametag = __instance.stateMachineShark.GetComponentInChildren<TextMeshPro>();
            if (nametag == null)
            {
                nametag = TwitchSharkName.Instance.AddNametag(__instance.stateMachineShark);
            }

            var name = nametag.text;
            RAPI.SendNetworkMessage(new UpdateSharkNameMessage(TwitchSharkName.MESSAGE_TYPE_SET_NAME, __instance.ObjectIndex, name), channel: TwitchSharkName.CHANNEL_ID);
        }
    }
}
