using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FlowStudio.Map;
using HarmonyLib;
using Minimap.Behaviours;
using Minimap.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Minimap;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class MinimapPlugin : BaseUnityPlugin
{
    // ReSharper disable once MemberCanBePrivate.Global
    internal static MinimapPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger;

    internal ConfigEntry<bool> RotateWithPlayer;
    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;

        Logger = base.Logger;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(MapPatches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");

        RotateWithPlayer = Config.Bind("Minimap", "Rotation Enabled", true,
            "Should the minimap rotate with the player camera or remain fixed?");
        
#if DEBUG
        /*
         * Live reload helper for development using ScriptEngine.
         */

        if (MapUIManager.Instance && MapManager.Instance.GetCurrentMapInView() != null)
        {
            MapPatches.InjectMinimap(MapUIManager.Instance);
        }
#endif
    }

    private void OnDestroy()
    {
        DestroyMinimapComponents();
    }

    private static void DestroyMinimapComponents()
    {
        Logger.LogInfo("Destroying minimap components...");
        foreach (var minimapComponent in FindObjectsOfType<MinimapCameraComponent>(true))
        {
            Destroy(minimapComponent);
        }
    }
}