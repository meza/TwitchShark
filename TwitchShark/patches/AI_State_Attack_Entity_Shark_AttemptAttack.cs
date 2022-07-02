using HarmonyLib;
using TMPro;

[HarmonyPatch(typeof(AI_State_Attack_Entity_Shark), "AttemptAttack")]
public static class AI_State_Attack_Entity_Shark_AttemptAttack
{
    static void Prefix(AI_StateMachine_Shark ___stateMachineShark)
    {
        if (Raft_Network.IsHost)
        {
            var nametag = ___stateMachineShark.GetComponentInChildren<TextMeshPro>();
            if (nametag != null)
            {
                TwitchShark.Instance.sharkCurrentlyAttacking = nametag.text;
            }
        }
    }

    static void Postfix()
    {
        if (Raft_Network.IsHost)
        {
            TwitchShark.Instance.sharkCurrentlyAttacking = null;
        }
    }
}