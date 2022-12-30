using HarmonyLib;
using Photon.Pun;

namespace DevCameraMod.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(PhotonNetwork))]
    [HarmonyPatch("Disconnect", MethodType.Normal)]
    internal class DisconnectPatch
    {
        internal static void Prefix()
        {
            if (!PhotonNetwork.InRoom) return;
            if (Plugin.Instance.intMode >= 3)
            {
                Plugin.Instance.cameraMode = 0;
                Plugin.Instance.intMode = 0;
                Plugin.Instance.OnModeChange();
            }
        }
    }
}
