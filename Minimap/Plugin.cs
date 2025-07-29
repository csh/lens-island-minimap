using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FlowStudio.Map;
using HarmonyLib;
using Minimap.Behaviours;
using Minimap.Patches;

namespace Minimap;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class MinimapPlugin : BaseUnityPlugin
{
    internal static MinimapPlugin Instance { get; private set; }
    internal static string OverlayRoot { get; private set; }
    internal new static ManualLogSource Logger;

    private const string DefaultOverlayFilename = "Overlay.png";
    
    internal ConfigEntry<MinimapRenderStyle> RenderingStyle;
    internal ConfigEntry<string> OverlayFilename;
    internal ConfigEntry<bool> RotateWithPlayer;
    internal ConfigEntry<bool> ReplaceCompass;
    internal ConfigEntry<float> ZoomHeight;
    internal ConfigEntry<bool> Flatten;

    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;

        Logger = base.Logger;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(MapPatches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
        
        var isScriptEngine = transform.name.Contains("ScriptEngine");
        if (isScriptEngine)
        {
            OverlayRoot = Path.Combine(Paths.BepInExRootPath, "scripts", "Overlays");
        }
        else
        {
            OverlayRoot = Path.Combine(Assembly.GetExecutingAssembly().Location, "Overlays");
        }

        var defaultOverlayPath = Path.Combine(OverlayRoot, DefaultOverlayFilename);
        if (!File.Exists(defaultOverlayPath))
        {
            Logger.LogInfo("Saving default overlay as Overlay.png");
            var current = Assembly.GetExecutingAssembly();
            try
            {
                using var stream = current.GetManifestResourceStream("Minimap.Overlay.png");
                if (stream is null) throw new FileNotFoundException("Failed to find default overlay");
                using var writer = File.OpenWrite(defaultOverlayPath);
                stream.CopyTo(writer);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to save default overlay to disk; {e}");
            }
        }

        ReplaceCompass = Config.Bind("Minimap", "Replace Compass", true,
            "Should the minimap hide the vanilla compass and shift status effects to the left to fill the empty space?");

        RotateWithPlayer = Config.Bind("Minimap", "Rotation Enabled", true,
            "Should the minimap rotate with the player camera or remain fixed?");

        OverlayFilename = Config.Bind("Minimap", "Overlay", DefaultOverlayFilename, 
            "Name of the overlay file to use.\n" + 
            "Custom overlay files should be placed in the 'Overlays' subdirectory."
        );

        ZoomHeight = Config.Bind("Minimap", "Camera Height", 60f,
            new ConfigDescription("How many units above the player should the minimap camera be?",
                new AcceptableValueRange<float>(MinimapCameraComponent.MinCameraHeight,
                    MinimapCameraComponent.MaxCameraHeight)));

        RenderingStyle = Config.Bind("Minimap", "Rendering Style", MinimapRenderStyle.Perspective);

        Flatten = Config.Bind("Orthographic", "Flatten", false,
            "Would you like to apply a shader that makes the art style of the map appear more flat?\n" +
            "Helps with making water look slightly less bad in orthographic mode."
        );

        RenderingStyle.SettingChanged += OnRenderingStyleChanged;
        Flatten.SettingChanged += OnFlattenChanged;
        OverlayFilename.SettingChanged += OnOverlayChanged;

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

    private void OnOverlayChanged(object sender, EventArgs e)
    {
        var minimap = MinimapCameraComponent.Instance;
        if (!minimap)
        {
            Logger.LogWarning(
                "Failed to find Minimap component; if you're not in game yet you can safely ignore this message.");
            return;
        }

        minimap.ReplaceOverlay(OverlayFilename.Value);
    }

    private void OnFlattenChanged(object sender, EventArgs e)
    {
        var minimap = MinimapCameraComponent.Instance;
        if (!minimap)
        {
            Logger.LogWarning(
                "Failed to find Minimap component; if you're not in game yet you can safely ignore this message.");
            return;
        }

        if (RenderingStyle.Value != MinimapRenderStyle.Orthographic) return;

        if (Flatten.Value)
        {
            minimap.ApplyFlattenShader();
        }
        else
        {
            minimap.RemoveFlattenShader();
        }
    }

    private void OnRenderingStyleChanged(object sender, EventArgs e)
    {
        var minimap = MinimapCameraComponent.Instance;
        if (!minimap)
        {
            Logger.LogWarning(
                "Failed to find Minimap component; if you're not in game yet you can safely ignore this message.");
            return;
        }

        if (RenderingStyle.Value == MinimapRenderStyle.Perspective)
        {
            minimap.SwitchToPerspective();
        }
        else
        {
            minimap.SwitchToOrthographic();
        }
    }

    private void OnDestroy()
    {
        RenderingStyle.SettingChanged -= OnRenderingStyleChanged;
        OverlayFilename.SettingChanged -= OnOverlayChanged;
        Flatten.SettingChanged -= OnFlattenChanged;

        _harmony?.UnpatchSelf();
        DestroyMinimapComponents();
    }

    private static void DestroyMinimapComponents()
    {
        /*
         * Even though we use a singleton, let's clean up thoroughly to be safe.
         */
        Logger.LogInfo("Destroying minimap components.");
        foreach (var minimapComponent in FindObjectsOfType<MinimapCameraComponent>(true))
        {
            Destroy(minimapComponent);
        }
    }
}