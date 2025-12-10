using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Bulbul;
using ChillPatcher.WeatherSync.Services;
using ChillPatcher.WeatherSync.Utils;
using ChillPatcher.WeatherSync.Models;

namespace ChillPatcher.WeatherSync.Core
{
    /// <summary>
    /// 季节性彩蛋自动化系统 - 根据季节、时间、天气自动触发特殊效果
    /// </summary>
    public class SceneryAutomationSystem : MonoBehaviour
    {
        internal static HashSet<EnvironmentType> _autoEnabledMods = new HashSet<EnvironmentType>();
        public static HashSet<EnvironmentType> UserInteractedMods = new HashSet<EnvironmentType>();

        // 点击冷却和延迟验证
        private Dictionary<EnvironmentType, float> _lastClickTime = new Dictionary<EnvironmentType, float>();
        private Dictionary<EnvironmentType, PendingAction> _pendingActions = new Dictionary<EnvironmentType, PendingAction>();

        private const float ClickCooldown = 2.0f;
        private const float VerifyDelay = 0.5f;

        private class PendingAction
        {
            public bool TargetState;
            public float VerifyTime;
            public string RuleName;
        }

        private class SceneryRule
        {
            public EnvironmentType EnvType;
            public Func<bool> Condition;
            public string Name;
        }

        private List<SceneryRule> _rules = new List<SceneryRule>();
        private float _checkTimer = 0f;
        private const float CheckInterval = 5f;

        // 概率触发状态
        private System.Random _random = new System.Random();
        private DateTime _lastDailyCheck = DateTime.MinValue;
        private bool _windBellTriggeredToday = false;
        private bool _windBellRollDoneToday = false;
        private bool _hotSpringTriggeredToday = false;
        private bool _hotSpringRollDoneToday = false;
        private bool _whaleTriggeredToday = false;
        private bool _whaleRollDoneToday = false;
        private bool _spaceTriggeredToday = false;
        private bool _spaceRollDoneToday = false;
        private bool _blueButterflyTriggeredToday = false;
        private DateTime _blueButterflyStartTime = DateTime.MinValue;

        /// <summary>
        /// 鲸鱼是否为系统触发（用于保护天气切换）
        /// </summary>
        internal static bool IsWhaleSystemTriggered = false;

        private void Start()
        {
            InitializeRules();
        }

        private void InitializeRules()
        {
            // 1. 烟花 - 农历除夕和春节期间（夜晚）
            _rules.Add(new SceneryRule
            {
                Name = "Fireworks",
                EnvType = EnvironmentType.Fireworks,
                Condition = () =>
                {
                    DateTime now = DateTime.Now;
                    bool isNight = IsNight();
                    bool isGregorianNewYear = (now.Month == 1 && now.Day == 1);
                    bool isLunarNewYear = TimeUtils.IsLunarNewYearPeriod(now);
                    return isNight && (isGregorianNewYear || isLunarNewYear);
                }
            });

            // 2. 做饭音效 - 中午和傍晚
            _rules.Add(new SceneryRule
            {
                Name = "CookingAudio",
                EnvType = EnvironmentType.CookSimmer,
                Condition = () =>
                {
                    int h = DateTime.Now.Hour;
                    int m = DateTime.Now.Minute;
                    double time = h + m / 60.0;
                    return (time >= 11.5 && time <= 12.5) || (time >= 17.5 && time <= 18.5);
                }
            });

            // 3. 空调音效 - 极端温度
            _rules.Add(new SceneryRule
            {
                Name = "AC_Audio",
                EnvType = EnvironmentType.RoomNoise,
                Condition = () =>
                {
                    var w = WeatherService.CachedWeather;
                    if (w == null) return false;
                    return w.Temperature > 30 || w.Temperature < 5;
                }
            });

            // 4. 樱花 - 春季白天晴朗
            _rules.Add(new SceneryRule
            {
                Name = "Sakura",
                EnvType = EnvironmentType.Sakura,
                Condition = () => GetSeason() == Season.Spring && IsDay() && IsGoodWeather()
            });

            // 5. 蝉鸣 - 夏季白天晴朗
            _rules.Add(new SceneryRule
            {
                Name = "Cicadas",
                EnvType = EnvironmentType.Chicada,
                Condition = () => GetSeason() == Season.Summer && IsDay() && IsGoodWeather()
            });

            // 6. 宇宙 - 晴朗夜晚1%概率
            _rules.Add(new SceneryRule
            {
                Name = "Space",
                EnvType = EnvironmentType.Space,
                Condition = () =>
                {
                    CheckDailyReset();
                    if (!IsNight() || !IsGoodWeather()) return false;
                    if (_spaceTriggeredToday) return true;
                    if (!_spaceRollDoneToday)
                    {
                        _spaceRollDoneToday = true;
                        if (_random.NextDouble() < 0.01)
                        {
                            _spaceTriggeredToday = true;
                            return true;
                        }
                    }
                    return false;
                }
            });

            // 7. 火车 - 圣诞节夜晚
            _rules.Add(new SceneryRule
            {
                Name = "Locomotive",
                EnvType = EnvironmentType.Locomotive,
                Condition = () =>
                {
                    DateTime now = DateTime.Now;
                    bool isChristmas = (now.Month == 12 && (now.Day == 24 || now.Day == 25));
                    return isChristmas && IsNight() && IsGoodWeather();
                }
            });

            // 8. 热气球 - 儿童节白天
            _rules.Add(new SceneryRule
            {
                Name = "Balloon",
                EnvType = EnvironmentType.Balloon,
                Condition = () =>
                {
                    DateTime now = DateTime.Now;
                    return (now.Month == 6 && now.Day == 1) && IsDay() && IsGoodWeather();
                }
            });

            // 9. 魔法书 - 读书日或开学日
            _rules.Add(new SceneryRule
            {
                Name = "Books",
                EnvType = EnvironmentType.Books,
                Condition = () =>
                {
                    DateTime now = DateTime.Now;
                    return (now.Month == 4 && now.Day == 23) || (now.Month == 9 && now.Day == 1);
                }
            });

            // 10. 蓝蝶 - 5-6月夜晚15%概率
            _rules.Add(new SceneryRule
            {
                Name = "BlueButterfly",
                EnvType = EnvironmentType.BlueButterfly,
                Condition = () =>
                {
                    CheckDailyReset();
                    DateTime now = DateTime.Now;
                    int month = now.Month;
                    if (month < 5 || month > 6 || !IsNight() || !IsGoodWeather()) return false;

                    if (_blueButterflyTriggeredToday)
                    {
                        if (_autoEnabledMods.Contains(EnvironmentType.BlueButterfly))
                        {
                            if ((DateTime.Now - _blueButterflyStartTime).TotalMinutes >= 20)
                                return false;
                            return true;
                        }
                        return false;
                    }

                    int currentSegment = (now.Hour * 60 + now.Minute) / 20;
                    int seed = now.Year * 10000 + now.DayOfYear * 100 + currentSegment;
                    var segmentRandom = new System.Random(seed);
                    if (segmentRandom.NextDouble() < 0.15)
                    {
                        _blueButterflyTriggeredToday = true;
                        _blueButterflyStartTime = DateTime.Now;
                        return true;
                    }
                    return false;
                }
            });

            // 11. 风铃 - 7-8月5%概率
            _rules.Add(new SceneryRule
            {
                Name = "WindBell",
                EnvType = EnvironmentType.WindBell,
                Condition = () =>
                {
                    CheckDailyReset();
                    int month = DateTime.Now.Month;
                    if (month < 7 || month > 8 || !IsGoodWeather()) return false;
                    if (_windBellTriggeredToday) return true;
                    if (!_windBellRollDoneToday)
                    {
                        _windBellRollDoneToday = true;
                        if (_random.NextDouble() < 0.05)
                        {
                            _windBellTriggeredToday = true;
                            return true;
                        }
                    }
                    return false;
                }
            });

            // 12. 温泉 - 冬季5%概率（下雪30%）
            _rules.Add(new SceneryRule
            {
                Name = "HotSpring",
                EnvType = EnvironmentType.HotSpring,
                Condition = () =>
                {
                    CheckDailyReset();
                    int month = DateTime.Now.Month;
                    if (!((month >= 11 && month <= 12) || (month >= 1 && month <= 2)) || !IsGoodWeather())
                        return false;
                    if (_hotSpringTriggeredToday) return true;
                    if (!_hotSpringRollDoneToday)
                    {
                        _hotSpringRollDoneToday = true;
                        var w = WeatherService.CachedWeather;
                        bool isSnowing = (w != null && w.Code >= 13 && w.Code <= 17);
                        double probability = isSnowing ? 0.30 : 0.05;
                        if (_random.NextDouble() < probability)
                        {
                            _hotSpringTriggeredToday = true;
                            return true;
                        }
                    }
                    return false;
                }
            });

            // 13. 鲸鱼 - 0.05%概率（极稀有）
            _rules.Add(new SceneryRule
            {
                Name = "Whale",
                EnvType = EnvironmentType.Whale,
                Condition = () =>
                {
                    CheckDailyReset();
                    if (_whaleTriggeredToday) return true;
                    if (!_whaleRollDoneToday)
                    {
                        _whaleRollDoneToday = true;
                        if (_random.NextDouble() < 0.0005)
                        {
                            _whaleTriggeredToday = true;
                            IsWhaleSystemTriggered = true;
                            Plugin.Logger?.LogWarning("[WeatherSync] 🐋 系统抽中鲸鱼！");
                            return true;
                        }
                    }
                    return false;
                }
            });
        }

        private void Update()
        {
            try
            {
                if (WeatherSyncConfig.EnableEasterEggs?.Value != true) return;
                if (!WeatherSyncManager.Initialized) return;

                ProcessPendingActions();

                _checkTimer += Time.deltaTime;
                if (_checkTimer >= CheckInterval)
                {
                    _checkTimer = 0f;
                    RunAutomationLogic();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] SceneryAutomation.Update 异常: {ex.Message}");
            }
        }

        private void ProcessPendingActions()
        {
            if (_pendingActions == null || _pendingActions.Count == 0)
                return;

            List<EnvironmentType> completed = new List<EnvironmentType>();

            try
            {
                foreach (var kvp in _pendingActions)
                {
                    if (kvp.Value == null) continue;

                    if (Time.time >= kvp.Value.VerifyTime)
                    {
                        var env = kvp.Key;
                        var action = kvp.Value;
                        bool currentState = IsEnvActive(env);

                        if (currentState == action.TargetState)
                        {
                            if (action.TargetState)
                            {
                                _autoEnabledMods?.Add(env);
                                Plugin.Logger?.LogInfo($"[WeatherSync] ✓ 已开启: {action.RuleName}");
                            }
                            else
                            {
                                _autoEnabledMods?.Remove(env);
                                Plugin.Logger?.LogInfo($"[WeatherSync] ✓ 已关闭: {action.RuleName}");
                            }
                        }
                        else
                        {
                            Plugin.Logger?.LogInfo($"[WeatherSync] ✗ 验证失败: {action.RuleName}");
                        }
                        completed.Add(env);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] ProcessPendingActions 异常: {ex.Message}");
            }

            foreach (var env in completed)
                _pendingActions.Remove(env);
        }

        private void RunAutomationLogic()
        {
            // 深海模式下停止自动托管
            if (IsEnvActive(EnvironmentType.DeepSea))
            {
                CleanupAllAutoMods();
                return;
            }

            // 检查已托管的环境
            List<EnvironmentType> toCheck = new List<EnvironmentType>(_autoEnabledMods);
            foreach (var envType in toCheck)
            {
                if (UserInteractedMods.Contains(envType))
                {
                    _autoEnabledMods.Remove(envType);
                    continue;
                }
                if (_pendingActions.ContainsKey(envType)) continue;

                var rule = _rules.Find(r => r.EnvType == envType);
                if (rule != null && !rule.Condition())
                    DisableMod(rule.Name, envType);
            }

            // 检查未托管的环境
            foreach (var rule in _rules)
            {
                if (UserInteractedMods.Contains(rule.EnvType)) continue;
                if (_autoEnabledMods.Contains(rule.EnvType)) continue;
                if (_pendingActions.ContainsKey(rule.EnvType)) continue;
                if (IsEnvActive(rule.EnvType)) continue;

                if (rule.Condition())
                    EnableMod(rule.Name, rule.EnvType);
            }
        }

        private void EnableMod(string ruleName, EnvironmentType env)
        {
            if (_lastClickTime.TryGetValue(env, out float lastTime))
            {
                if (Time.time - lastTime < ClickCooldown) return;
            }

            if (IsEnvActive(env))
            {
                _autoEnabledMods.Add(env);
                return;
            }

            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                Plugin.Logger?.LogInfo($"[WeatherSync] → 开启: {ruleName}");
                WeatherSyncManager.SimulateClickMainIcon(ctrl);
                _lastClickTime[env] = Time.time;

                _pendingActions[env] = new PendingAction
                {
                    TargetState = true,
                    VerifyTime = Time.time + VerifyDelay,
                    RuleName = ruleName
                };
            }
        }

        private void DisableMod(string ruleName, EnvironmentType env)
        {
            if (_lastClickTime.TryGetValue(env, out float lastTime))
            {
                if (Time.time - lastTime < ClickCooldown) return;
            }

            if (!IsEnvActive(env))
            {
                _autoEnabledMods.Remove(env);
                return;
            }

            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                Plugin.Logger?.LogInfo($"[WeatherSync] → 关闭: {ruleName}");
                WeatherSyncManager.SimulateClickMainIcon(ctrl);
                _lastClickTime[env] = Time.time;

                _pendingActions[env] = new PendingAction
                {
                    TargetState = false,
                    VerifyTime = Time.time + VerifyDelay,
                    RuleName = ruleName
                };
            }
        }

        private void CleanupAllAutoMods()
        {
            List<EnvironmentType> toClean = new List<EnvironmentType>(_autoEnabledMods);
            foreach (var env in toClean)
            {
                var rule = _rules.Find(r => r.EnvType == env);
                if (rule != null)
                    DisableMod(rule.Name, env);
            }
        }

        private void CheckDailyReset()
        {
            DateTime today = DateTime.Today;
            if (_lastDailyCheck.Date != today)
            {
                _lastDailyCheck = today;
                _windBellTriggeredToday = false;
                _windBellRollDoneToday = false;
                _hotSpringTriggeredToday = false;
                _hotSpringRollDoneToday = false;
                _whaleTriggeredToday = false;
                _whaleRollDoneToday = false;
                _spaceTriggeredToday = false;
                _spaceRollDoneToday = false;
                _blueButterflyTriggeredToday = false;
                IsWhaleSystemTriggered = false;
            }
        }

        private bool IsEnvActive(EnvironmentType env)
        {
            if (!EnvRegistry.TryGet(env, out var ctrl)) return false;

            try
            {
                var ctrlType = ctrl.GetType();

                // 环境音：通过滑块值检查
                var ambientBehaviorField = ctrlType.GetField("_ambientSoundBehavior",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (ambientBehaviorField != null)
                {
                    var ambientBehavior = ambientBehaviorField.GetValue(ctrl);
                    if (ambientBehavior != null)
                    {
                        var sliderField = ambientBehavior.GetType().GetField("_volumeSlider",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                        if (sliderField != null)
                        {
                            var sliderObj = sliderField.GetValue(ambientBehavior);
                            if (sliderObj != null)
                            {
                                var valueProp = sliderObj.GetType().GetProperty("value");
                                if (valueProp != null)
                                {
                                    float val = (float)valueProp.GetValue(sliderObj);
                                    return val > 0f;
                                }
                            }
                        }
                    }
                }

                // 窗景：检查 IsActive
                var windowField = ctrlType.GetField("_windowBehavior",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (windowField != null)
                {
                    var behaviorObj = windowField.GetValue(ctrl);
                    if (behaviorObj != null)
                    {
                        var typeProp = behaviorObj.GetType().GetProperty("WindowViewType");
                        if (typeProp != null)
                        {
                            var winType = (WindowViewType)typeProp.GetValue(behaviorObj);
                            if (SaveDataManager.Instance.WindowViewDic.TryGetValue(winType, out var data))
                                return data.IsActive;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private Season GetSeason()
        {
            int month = DateTime.Now.Month;
            if (month >= 3 && month <= 5) return Season.Spring;
            if (month >= 6 && month <= 8) return Season.Summer;
            if (month >= 9 && month <= 11) return Season.Autumn;
            return Season.Winter;
        }

        private bool IsDay()
        {
            int h = DateTime.Now.Hour;
            return h >= 6 && h < 18;
        }

        private bool IsNight()
        {
            int h = DateTime.Now.Hour;
            return h >= 19 || h < 5;
        }

        private bool IsGoodWeather()
        {
            var w = WeatherService.CachedWeather;
            if (w == null) return true;
            return w.Code >= 0 && w.Code <= 9;
        }
    }
}
