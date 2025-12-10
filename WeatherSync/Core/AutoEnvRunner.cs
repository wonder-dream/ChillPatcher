using System;
using System.Reflection;
using UnityEngine;
using Bulbul;
using ChillPatcher.WeatherSync.Models;
using ChillPatcher.WeatherSync.Services;
using ChillPatcher.WeatherSync.Utils;

namespace ChillPatcher.WeatherSync.Core
{
    /// <summary>
    /// 自动环境切换运行器 - 根据时间和天气自动切换游戏环境
    /// </summary>
    public class AutoEnvRunner : MonoBehaviour
    {
        private float _nextWeatherCheckTime;
        private float _nextTimeCheckTime;
        private EnvironmentType? _lastAppliedEnv;
        private bool _isFetching;
        private bool _pendingForceRefresh;

        private static AutoEnvRunner _instance;

        // 基础环境类型（互斥）
        private static readonly EnvironmentType[] BaseEnvironments = new[]
        {
            EnvironmentType.Day,
            EnvironmentType.Sunset,
            EnvironmentType.Night,
            EnvironmentType.Cloudy
        };

        // 降水效果类型
        private static readonly EnvironmentType[] SceneryWeathers = new[]
        {
            EnvironmentType.ThunderRain,
            EnvironmentType.HeavyRain,
            EnvironmentType.LightRain,
            EnvironmentType.Snow
        };

        // 所有主要环境类型
        private static readonly EnvironmentType[] MainEnvironments = new[]
        {
            EnvironmentType.Day,
            EnvironmentType.Sunset,
            EnvironmentType.Night,
            EnvironmentType.Cloudy,
            EnvironmentType.LightRain,
            EnvironmentType.HeavyRain,
            EnvironmentType.ThunderRain,
            EnvironmentType.Snow
        };

        /// <summary>
        /// 当前显示在UI上的天气字符串
        /// </summary>
        public static string UIWeatherString { get; set; } = "";

        private void Start()
        {
            _instance = this;
            _nextWeatherCheckTime = Time.time + 10f;
            _nextTimeCheckTime = Time.time + 10f;
            Plugin.Logger?.LogInfo("[WeatherSync] AutoEnvRunner 启动...");

            // 首次同步日出日落
            CheckAndSyncSunSchedule();
        }

        private void CheckAndSyncSunSchedule()
        {
            if (!WeatherSyncConfig.EnableWeatherSync.Value)
                return;

            string lastSync = WeatherSyncConfig.LastSunSyncDate.Value;
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (lastSync != today)
            {
                StartCoroutine(SyncSunScheduleRoutine(today));
            }
        }

        private System.Collections.IEnumerator SyncSunScheduleRoutine(string targetDate)
        {
            int retryCount = 0;
            float delay = 1f;
            const int MaxRetries = 10;

            while (retryCount < MaxRetries)
            {
                bool success = false;
                string apiKey = WeatherSyncConfig.SeniverseKey.Value;
                string location = WeatherSyncConfig.Location.Value;

                yield return WeatherService.FetchSunSchedule(apiKey, location, (data) =>
                {
                    if (data != null)
                    {
                        Plugin.Logger?.LogInfo($"[WeatherSync] 日出日落同步成功: 日出{data.Sunrise} 日落{data.Sunset}");

                        WeatherSyncConfig.SunriseTime.Value = data.Sunrise;
                        WeatherSyncConfig.SunsetTime.Value = data.Sunset;
                        WeatherSyncConfig.LastSunSyncDate.Value = targetDate;

                        success = true;
                    }
                });

                if (success)
                    yield break;

                Plugin.Logger?.LogWarning($"[WeatherSync] 日出日落同步失败，{delay}秒后重试 ({retryCount + 1}/{MaxRetries})");
                yield return new WaitForSeconds(delay);

                delay *= 2f;
                retryCount++;
            }
            Plugin.Logger?.LogError("[WeatherSync] 达到最大重试次数，今日放弃日出日落同步");
        }

        private void Update()
        {
            try
            {
                if (!WeatherSyncManager.Initialized || EnvRegistry.Count == 0)
                    return;

                // 快捷键
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    Plugin.Logger?.LogInfo("[WeatherSync] F9: 强制同步");
                    TriggerSync(false, true);
                }
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    ShowStatus();
                }
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    Plugin.Logger?.LogInfo("[WeatherSync] F7: 强制刷新天气");
                    ForceRefreshWeather();
                }

                // 定时检查
                if (Time.time >= _nextTimeCheckTime)
                {
                    _nextTimeCheckTime = Time.time + 30f;
                    TriggerSync(false, false);
                }
                if (Time.time >= _nextWeatherCheckTime)
                {
                    int refreshMinutes = WeatherSyncConfig.WeatherRefreshMinutes?.Value ?? 30;
                    _nextWeatherCheckTime = Time.time + (Mathf.Max(1, refreshMinutes) * 60f);
                    TriggerSync(true, false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[WeatherSync] AutoEnvRunner.Update 异常: {ex.Message}");
            }
        }

        private void ShowStatus()
        {
            var now = DateTime.Now;
            Plugin.Logger?.LogInfo($"--- WeatherSync 状态 [{now:HH:mm:ss}] ---");
            Plugin.Logger?.LogInfo($"插件记录: {_lastAppliedEnv}");
            var currentActive = GetCurrentActiveEnvironment();
            Plugin.Logger?.LogInfo($"游戏实际: {currentActive}");
            Plugin.Logger?.LogInfo($"UI文本: {UIWeatherString}");
            if (WeatherSyncConfig.DebugMode.Value)
                Plugin.Logger?.LogWarning("【警告】调试模式已开启！");
        }

        private void ForceRefreshWeather()
        {
            if (_isFetching)
            {
                _pendingForceRefresh = true;
                Plugin.Logger?.LogInfo("[WeatherSync] 正在请求中，已排队强制刷新");
                return;
            }

            _nextWeatherCheckTime = Time.time + (WeatherSyncConfig.WeatherRefreshMinutes.Value * 60f);
            TriggerSync(true, false);
        }

        /// <summary>
        /// 外部触发天气刷新
        /// </summary>
        public static void TriggerWeatherRefresh()
        {
            if (_instance != null)
            {
                Plugin.Logger?.LogInfo("[WeatherSync] 外部触发天气刷新");
                _instance.ForceRefreshWeather();
            }
        }

        /// <summary>
        /// 外部触发日出日落刷新
        /// </summary>
        public static void TriggerSunScheduleRefresh()
        {
            if (_instance != null)
            {
                Plugin.Logger?.LogInfo("[WeatherSync] 外部触发日出日落刷新");
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                _instance.StartCoroutine(_instance.SyncSunScheduleRoutine(today));
            }
        }

        private void TriggerSync(bool forceApi, bool forceApply)
        {
            Plugin.Logger?.LogDebug($"[WeatherSync] TriggerSync (forceApi={forceApi}, forceApply={forceApply})");

            // 调试模式
            if (WeatherSyncConfig.DebugMode.Value)
            {
                Plugin.Logger?.LogWarning("[WeatherSync] 调试模式使用模拟数据");
                int mockCode = WeatherSyncConfig.DebugCode.Value;
                var mockWeather = new WeatherInfo
                {
                    Code = mockCode,
                    Temperature = WeatherSyncConfig.DebugTemp.Value,
                    Text = WeatherSyncConfig.DebugText.Value,
                    Condition = WeatherService.MapCodeToCondition(mockCode),
                    UpdateTime = DateTime.Now
                };
                ApplyEnvironment(mockWeather, forceApply);
                return;
            }

            bool weatherEnabled = WeatherSyncConfig.EnableWeatherSync.Value;
            string apiKey = WeatherSyncConfig.SeniverseKey.Value;

            if (!(weatherEnabled && (!string.IsNullOrEmpty(apiKey) || WeatherService.HasDefaultKey)))
            {
                ApplyTimeBasedEnvironment(forceApply);
                return;
            }

            string location = WeatherSyncConfig.Location.Value;

            if (forceApi || WeatherService.CachedWeather == null)
            {
                if (_isFetching)
                {
                    Plugin.Logger?.LogWarning("[WeatherSync] 请求进行中，跳过");
                    return;
                }

                _isFetching = true;
                StartCoroutine(WeatherService.FetchWeather(apiKey, location, forceApi, (weather) =>
                {
                    _isFetching = false;
                    if (weather != null)
                    {
                        ApplyEnvironment(weather, forceApply);
                    }
                    else
                    {
                        Plugin.Logger?.LogWarning("[WeatherSync] API异常，使用时间兜底");
                        ApplyTimeBasedEnvironment(forceApply);
                    }

                    if (_pendingForceRefresh)
                    {
                        _pendingForceRefresh = false;
                        ForceRefreshWeather();
                    }
                }));
            }
            else
            {
                ApplyEnvironment(WeatherService.CachedWeather, forceApply);
            }
        }

        private EnvironmentType? GetCurrentActiveEnvironment()
        {
            try
            {
                if (SaveDataManager.Instance?.WindowViewDic == null)
                    return null;

                var dict = SaveDataManager.Instance.WindowViewDic;
                foreach (var env in MainEnvironments)
                {
                    var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString());
                    if (dict.ContainsKey(winType) && dict[winType]?.IsActive == true)
                        return env;
                }
            }
            catch { }
            return null;
        }

        private bool IsEnvironmentActive(EnvironmentType env)
        {
            try
            {
                if (SaveDataManager.Instance?.WindowViewDic == null)
                    return false;

                var dict = SaveDataManager.Instance.WindowViewDic;
                var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString());
                if (dict.ContainsKey(winType))
                    return dict[winType]?.IsActive == true;
            }
            catch { }
            return false;
        }

        private void SimulateClick(EnvironmentType env)
        {
            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                WeatherSyncManager.SimulateClickMainIcon(ctrl);
            }
        }

        private bool IsBadWeather(int code)
        {
            // 排除太阳雨/雪
            if (code == 10 || code == 13 || code == 21 || code == 22) return false;
            if (code == 4) return true;
            if (code >= 7 && code <= 31) return true;
            if (code >= 34 && code <= 36) return true;
            return false;
        }

        private EnvironmentType? GetSceneryType(int code)
        {
            if (code >= 20 && code <= 25) return EnvironmentType.Snow;
            if (code == 11 || code == 12 || (code >= 16 && code <= 18)) return EnvironmentType.ThunderRain;
            if (code == 10 || code == 14 || code == 15) return EnvironmentType.HeavyRain;
            if (code == 13 || code == 19) return EnvironmentType.LightRain;
            return null;
        }

        private EnvironmentType GetTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now;
            TimeSpan cur = now.TimeOfDay;
            TimeSpan.TryParse(WeatherSyncConfig.SunriseTime.Value, out TimeSpan sunrise);
            TimeSpan.TryParse(WeatherSyncConfig.SunsetTime.Value, out TimeSpan sunset);

            if (cur >= sunrise && cur < sunset.Subtract(TimeSpan.FromMinutes(30)))
                return EnvironmentType.Day;
            else if (cur >= sunset.Subtract(TimeSpan.FromMinutes(30)) && cur < sunset.Add(TimeSpan.FromMinutes(30)))
                return EnvironmentType.Sunset;
            else
                return EnvironmentType.Night;
        }

        private void ApplyBaseEnvironment(EnvironmentType target, bool force)
        {
            if (!force && IsEnvironmentActive(target))
                return;

            foreach (var env in BaseEnvironments)
            {
                if (env != target && IsEnvironmentActive(env))
                    SimulateClick(env);
            }

            if (!IsEnvironmentActive(target))
                SimulateClick(target);

            WeatherSyncManager.CallServiceChangeWeather(target);
            Plugin.Logger?.LogInfo($"[WeatherSync] 环境切换至: {target}");
        }

        private void ApplyScenery(EnvironmentType? target, bool force)
        {
            foreach (var env in SceneryWeathers)
            {
                bool shouldBeActive = (target.HasValue && target.Value == env);
                bool isActive = IsEnvironmentActive(env);

                if (shouldBeActive && !isActive)
                {
                    SimulateClick(env);
                    Plugin.Logger?.LogInfo($"[WeatherSync] 开启景色: {env}");
                }
                else if (!shouldBeActive && isActive)
                {
                    SimulateClick(env);
                }
            }
        }

        private void ApplyEnvironment(WeatherInfo weather, bool force)
        {
            if (weather == null)
            {
                ApplyTimeBasedEnvironment(force);
                return;
            }

            // 鲸鱼保护
            if (SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                Plugin.Logger?.LogInfo("[WeatherSync] 鲸鱼彩蛋生效中，跳过天气切换");
                return;
            }

            if (force || _lastAppliedEnv == null)
                Plugin.Logger?.LogInfo($"[WeatherSync] 天气:{weather.Text}(Code:{weather.Code})");

            UIWeatherString = $"{weather.Text} {weather.Temperature}°C";

            EnvironmentType baseEnv = GetTimeBasedEnvironment();
            EnvironmentType finalEnv = baseEnv;

            if (IsBadWeather(weather.Code))
            {
                if (baseEnv != EnvironmentType.Night)
                    finalEnv = EnvironmentType.Cloudy;
            }

            ApplyBaseEnvironment(finalEnv, force);
            ApplyScenery(GetSceneryType(weather.Code), force);
            _lastAppliedEnv = finalEnv;
        }

        private void ApplyTimeBasedEnvironment(bool force)
        {
            // 鲸鱼保护
            if (SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                Plugin.Logger?.LogInfo("[WeatherSync] 鲸鱼彩蛋生效中，跳过天气切换");
                return;
            }

            UIWeatherString = "";
            EnvironmentType targetEnv = GetTimeBasedEnvironment();
            ApplyBaseEnvironment(targetEnv, force);
            ApplyScenery(null, force);
        }
    }
}
