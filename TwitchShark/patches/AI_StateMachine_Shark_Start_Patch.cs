using HarmonyLib;

[HarmonyPatch(typeof(AI_StateMachine_Shark), "Start")]
public static class AI_StateMachine_Shark_Start_Patch
{
    static void Postfix(AI_StateMachine_Shark __instance)
    {
        TwitchSharkName.Instance.AddNametag(__instance);
    }
}
