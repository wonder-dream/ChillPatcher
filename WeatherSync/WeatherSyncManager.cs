using System;
using System.Reflection;
using UnityEngine;
using Bulbul;
using ChillPatcher.WeatherSync.Core;
using ChillPatcher.WeatherSync.Utils;

namespace ChillPatcher.WeatherSync
{
    /// <summary>
    /// 天气同步管理器 - 主入口
    /// </summary>
    public static class WeatherSyncManager
    {
        internal static UnlockItemService UnlockItemServiceInstance;
        internal static object WindowViewServiceInstance;
        internal static MethodInfo ChangeWeatherMethod;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool Initialized { get; private set; }

        private static GameObject _runnerGO;

        /// <summary>
        /// 启动天气同步系统
        /// </summary>
        public static void Start()
        {
            try
            {
                _runnerGO = new GameObject("ChillPatcher_WeatherSyncRunner");
                _runnerGO.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_runnerGO);
                _runnerGO.SetActive(true);

                // 挂载核心组件
                _runnerGO.AddComponent<AutoEnvRunner>();
                _runnerGO.AddComponent<SceneryAutomationSystem>();

                Plugin.Logger?.LogInfo("[WeatherSync] 天气同步系统已启动");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] 启动失败: {ex}");
            }
        }

        /// <summary>
        /// 停止天气同步系统
        /// </summary>
        public static void Stop()
        {
            if (_runnerGO != null)
            {
                UnityEngine.Object.Destroy(_runnerGO);
                _runnerGO = null;
            }
            Initialized = false;
            Plugin.Logger?.LogInfo("[WeatherSync] 天气同步系统已停止");
        }

        /// <summary>
        /// 初始化（在UnlockItemService准备好后调用）
        /// </summary>
        internal static void TryInitializeOnce(UnlockItemService svc)
        {
            if (Initialized || svc == null)
                return;

            UnlockItemServiceInstance = svc;

            if (WeatherSyncConfig.UnlockEnvironments.Value)
                ForceUnlockAllEnvironments(svc);
            if (WeatherSyncConfig.UnlockDecorations.Value)
                ForceUnlockAllDecorations(svc);

            Initialized = true;
            Plugin.Logger?.LogInfo("[WeatherSync] 初始化完成");
        }

        /// <summary>
        /// 调用游戏服务切换天气
        /// </summary>
        internal static void CallServiceChangeWeather(EnvironmentType envType)
        {
            if (WindowViewServiceInstance == null || ChangeWeatherMethod == null)
                return;

            try
            {
                var parameters = ChangeWeatherMethod.GetParameters();
                if (parameters.Length == 0)
                    return;

                Type windowViewEnumType = parameters[0].ParameterType;
                object enumValue = Enum.Parse(windowViewEnumType, envType.ToString());
                ChangeWeatherMethod.Invoke(WindowViewServiceInstance, new object[] { enumValue });
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] Service调用失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 模拟点击环境按钮
        /// </summary>
        internal static void SimulateClickMainIcon(EnviromentController ctrl)
        {
            if (ctrl == null)
                return;

            try
            {
                Plugin.Logger?.LogDebug($"[WeatherSync] 模拟点击: {ctrl.name}");
                MethodInfo clickMethod = ctrl.GetType().GetMethod(
                    "OnClickButtonMainIcon",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (clickMethod != null)
                {
                    WeatherSyncPatches.UserInteractionPatch.IsSimulatingClick = true;
                    clickMethod.Invoke(ctrl, null);
                    WeatherSyncPatches.UserInteractionPatch.IsSimulatingClick = false;
                }
                else
                {
                    Plugin.Logger?.LogError($"[WeatherSync] 未找到 OnClickButtonMainIcon 方法");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] 模拟点击失败: {ex.Message}");
                WeatherSyncPatches.UserInteractionPatch.IsSimulatingClick = false;
            }
        }

        /// <summary>
        /// 强制解锁所有环境
        /// </summary>
        private static void ForceUnlockAllEnvironments(UnlockItemService svc)
        {
            try
            {
                var envProp = svc.GetType().GetProperty("Environment");
                var unlockEnvObj = envProp?.GetValue(svc);
                if (unlockEnvObj == null) return;

                var dictField = unlockEnvObj.GetType().GetField(
                    "_environmentDic",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = dictField?.GetValue(unlockEnvObj) as System.Collections.IDictionary;
                if (dict == null) return;

                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField(
                        "_isLocked",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (lockField == null) continue;

                    var reactive = lockField.GetValue(data);
                    if (reactive == null) continue;

                    var propValue = reactive.GetType().GetProperty("Value");
                    propValue?.SetValue(reactive, false, null);
                    count++;
                }
                Plugin.Logger?.LogInfo($"[WeatherSync] 已解锁 {count} 个环境");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] 解锁环境失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制解锁所有装饰品
        /// </summary>
        private static void ForceUnlockAllDecorations(UnlockItemService svc)
        {
            try
            {
                var decoProp = svc.GetType().GetProperty("Decoration");
                if (decoProp == null) return;

                var unlockDecoObj = decoProp.GetValue(svc);
                if (unlockDecoObj == null) return;

                var dictField = unlockDecoObj.GetType().GetField(
                    "_decorationDic",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (dictField == null) return;

                var dict = dictField.GetValue(unlockDecoObj) as System.Collections.IDictionary;
                if (dict == null) return;

                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField(
                        "_isLocked",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (lockField == null) continue;

                    var reactive = lockField.GetValue(data);
                    if (reactive == null) continue;

                    var propValue = reactive.GetType().GetProperty("Value");
                    if (propValue == null) continue;

                    propValue.SetValue(reactive, false, null);
                    count++;
                }
                Plugin.Logger?.LogInfo($"[WeatherSync] 已解锁 {count} 个装饰品");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] 解锁装饰品失败: {ex.Message}");
            }
        }
    }
}
