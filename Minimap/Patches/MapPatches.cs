using System.Diagnostics.CodeAnalysis;
using FlowStudio.Map;
using HarmonyLib;
using Minimap.Behaviours;

namespace Minimap.Patches;

public static class MapPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.Start))]
    public static void StartPostfix(
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony")]
        MapUIManager __instance)
    {
        InjectMinimap(__instance);
    }

    internal static void InjectMinimap(MapUIManager mapManager)
    {
        if (!mapManager || mapManager.gameObject.TryGetComponent<MinimapCameraComponent>(out _)) return;
        
        mapManager.gameObject.AddComponent<MinimapCameraComponent>();
        MinimapPlugin.Logger.LogInfo("Minimap injected successfully!");
    }
}