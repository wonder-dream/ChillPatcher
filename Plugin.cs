using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ChillPatcher.Patches;

namespace ChillPatcher
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            // 初始化配置
            PluginConfig.Initialize(Config);

            // Apply Harmony patches
            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo("Harmony patches applied!");

            // 初始化全局键盘钩子（用于壁纸引擎模式）
            KeyboardHookPatch.Initialize();
            Logger.LogInfo("Keyboard hook initialized!");

            // 注册游戏退出事件来清理钩子
            UnityEngine.Application.quitting += OnApplicationQuitting;
        }

        private void OnApplicationQuitting()
        {
            // 清理键盘钩子
            KeyboardHookPatch.Cleanup();
            Logger.LogInfo("Application quitting - cleanup done!");
        }
    }
}
