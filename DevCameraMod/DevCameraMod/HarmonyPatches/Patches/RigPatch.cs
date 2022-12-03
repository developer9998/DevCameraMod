using HarmonyLib;

namespace DevCameraMod.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(VRRig))]
    [HarmonyPatch("Start", 0)]
    class RigPatch
    {
        internal static void Postfix(VRRig __instance)
        {
            //__instance.gameObject.AddComponent<TagSpawner>();
        }
    }
}
