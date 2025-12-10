using System;
using System.Reflection;
using HarmonyLib;
using Bulbul;
using TMPro;
using ChillPatcher.WeatherSync.Utils;
using ChillPatcher.WeatherSync.Core;

namespace ChillPatcher.WeatherSync
{
    /// <summary>
    /// 天气同步相关的 Harmony 补丁
    /// </summary>
    public static class WeatherSyncPatches
    {
        /// <summary>
        /// 用户交互检测补丁
        /// </summary>
        [HarmonyPatch(typeof(EnviromentController), "OnClickButtonMainIcon")]
        public static class UserInteractionPatch
        {
            /// <summary>
            /// 是否为模拟点击（系统自动触发）
            /// </summary>
            public static bool IsSimulatingClick = false;

            static void Prefix(EnviromentController __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (IsSimulatingClick) return;

                    EnvironmentType type = __instance.EnvironmentType;

                    if (SceneryAutomationSystem.UserInteractedMods != null &&
                        !SceneryAutomationSystem.UserInteractedMods.Contains(type))
                    {
                        SceneryAutomationSystem.UserInteractedMods.Add(type);
                        Plugin.Logger?.LogInfo($"[WeatherSync] 用户接管了 {type}，停止自动托管");
                    }

                    // 从自动托管列表移除
                    if (SceneryAutomationSystem._autoEnabledMods != null &&
                        SceneryAutomationSystem._autoEnabledMods.Contains(type))
                    {
                        SceneryAutomationSystem._autoEnabledMods.Remove(type);
                    }

                    // 特殊处理：用户关闭系统触发的鲸鱼
                    if (type == EnvironmentType.Whale && SceneryAutomationSystem.IsWhaleSystemTriggered)
                    {
                        SceneryAutomationSystem.IsWhaleSystemTriggered = false;
                        Plugin.Logger?.LogInfo("[WeatherSync] 用户关闭了系统触发的鲸鱼");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherSync] UserInteractionPatch 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// UnlockItemService 初始化补丁
        /// </summary>
        [HarmonyPatch(typeof(UnlockItemService), "Setup")]
        public static class UnlockServicePatch
        {
            static void Postfix(UnlockItemService __instance)
            {
                try
                {
                    if (__instance != null)
                    {
                        WeatherSyncManager.TryInitializeOnce(__instance);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherSync] UnlockServicePatch 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// EnviromentController 注册补丁
        /// </summary>
        [HarmonyPatch(typeof(EnviromentController), "Setup")]
        public static class EnvControllerPatch
        {
            static void Postfix(EnviromentController __instance)
            {
                try
                {
                    if (__instance != null)
                    {
                        EnvRegistry.Register(__instance.EnvironmentType, __instance);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherSync] EnvControllerPatch 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// FacilityEnviroment 服务捕获补丁
        /// </summary>
        [HarmonyPatch(typeof(FacilityEnviroment), "Setup")]
        public static class FacilityEnvPatch
        {
            static void Postfix(FacilityEnviroment __instance)
            {
                try
                {
                    FieldInfo field = typeof(FacilityEnviroment).GetField(
                        "_windowViewService",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (field != null)
                    {
                        object service = field.GetValue(__instance);
                        if (service != null)
                        {
                            WeatherSyncManager.WindowViewServiceInstance = service;
                            WeatherSyncManager.ChangeWeatherMethod = service.GetType().GetMethod(
                                "ChangeWeatherAndTime",
                                BindingFlags.Instance | BindingFlags.Public);

                            if (WeatherSyncManager.ChangeWeatherMethod != null)
                                Plugin.Logger?.LogInfo("[WeatherSync] 成功捕获 WindowViewService");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherSync] 捕获服务失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 日期UI天气显示补丁
        /// </summary>
        [HarmonyPatch(typeof(CurrentDateAndTimeUI), "UpdateDateAndTime")]
        public static class DateUIPatch
        {
            static void Postfix(CurrentDateAndTimeUI __instance)
            {
                if (__instance == null) return;

                // 显示天气
                if (WeatherSyncConfig.ShowWeatherOnUI?.Value == true && !string.IsNullOrEmpty(AutoEnvRunner.UIWeatherString))
                {
                    try
                    {
                        var field = typeof(CurrentDateAndTimeUI).GetField(
                            "_dateText",
                            BindingFlags.Instance | BindingFlags.NonPublic);

                        if (field != null)
                        {
                            var textMesh = field.GetValue(__instance) as TextMeshProUGUI;
                            if (textMesh != null)
                                textMesh.text += $" | {AutoEnvRunner.UIWeatherString}";
                        }
                    }
                    catch { }
                }

                // 详细时间段显示
                if (WeatherSyncConfig.DetailedTimeSegments?.Value == true)
                {
                    try
                    {
                        if (SaveDataManager.Instance?.SettingData?.TimeFormat == null) return;
                        var timeFormat = SaveDataManager.Instance.SettingData.TimeFormat.Value;
                        if (timeFormat.ToString() != "AMPM") return;

                        string timeSegment = TimeUtils.GetDetailedTimeSegment();

                        var amPmField = typeof(CurrentDateAndTimeUI).GetField(
                            "_amPmText",
                            BindingFlags.Instance | BindingFlags.NonPublic);

                        if (amPmField != null)
                        {
                            var localizationBehaviour = amPmField.GetValue(__instance);
                            if (localizationBehaviour != null)
                            {
                                var textProp = localizationBehaviour.GetType().GetProperty("Text");
                                if (textProp != null)
                                {
                                    var tmpro = textProp.GetValue(localizationBehaviour) as TextMeshProUGUI;
                                    if (tmpro != null)
                                        tmpro.text = timeSegment;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
