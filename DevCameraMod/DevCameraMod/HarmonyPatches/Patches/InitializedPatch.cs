using HarmonyLib;
using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace DevCameraMod.HarmonyPatches.Patches
{
    [HarmonyPatch(typeof(GorillaLocomotion.Player))]
    [HarmonyPatch("Awake", MethodType.Normal)]
    internal class InitializedPatch
    {
        internal static void Postfix(GorillaLocomotion.Player __instance) => __instance.StartCoroutine(Delay());

        internal static IEnumerator Delay()
        {
            yield return 0;
            
            Plugin.Instance.OnInitialized();
            yield break;
        }
    }
}
