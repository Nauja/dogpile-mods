using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ShowDebugMenu
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private bool isToggled;

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F7), new ConfigDescription("Press this key to show/hide the debug menu"));

            // Plugin startup logic
            Logger = base.Logger;
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                var debugMenu = FindObjectOfType<DebugMenu>();
                if (!debugMenu)
                {
                    Logger.LogWarning("Debug menu not found");
                    return;
                }

                var canvas = debugMenu.GetComponent<Canvas>();
                if (!canvas)
                {
                    Logger.LogWarning("No canvas on debug menu");
                    return;
                }

                canvas.enabled = !canvas.enabled;
                isToggled = canvas.enabled;
            }
        }

        private void LateUpdate() {
            if (isToggled)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
