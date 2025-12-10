using System;
using System.Collections;
using UnityEngine.Networking;
using ChillPatcher.WeatherSync.Models;
using Bulbul;

namespace ChillPatcher.WeatherSync.Services
{
    /// <summary>
    /// 天气服务 - 通过心知天气API获取天气数据
    /// </summary>
    public static class WeatherService
    {
        private static WeatherInfo _cachedWeather;
        private static DateTime _lastFetchTime;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(60);
        private static string _lastLocation;

        // 内置的加密 API Key（来自原 RealTimeWeatherMod）
        private static readonly string _encryptedDefaultKey = "7Mr4YSR87bFvE4zDgj6NbuBKgz4EiPYEnRTQ0RIaeSU=";

        /// <summary>
        /// 缓存的天气信息
        /// </summary>
        public static WeatherInfo CachedWeather => _cachedWeather;

        /// <summary>
        /// 是否有内置的默认 API Key
        /// </summary>
        public static bool HasDefaultKey => !string.IsNullOrEmpty(_encryptedDefaultKey);

        /// <summary>
        /// 获取天气数据
        /// </summary>
        /// <param name="apiKey">用户自定义的 API Key（可为空，使用内置key）</param>
        /// <param name="location">城市名称（拼音或中文）</param>
        /// <param name="force">是否强制刷新（忽略缓存）</param>
        /// <param name="onComplete">完成回调</param>
        public static IEnumerator FetchWeather(string apiKey, string location, bool force, Action<WeatherInfo> onComplete)
        {
            string normalizedLocation = location?.Trim() ?? "";

            // 检查缓存
            if (!force &&
                _cachedWeather != null &&
                DateTime.Now - _lastFetchTime < CacheExpiry &&
                string.Equals(_lastLocation, normalizedLocation, StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(_cachedWeather);
                yield break;
            }

            // 确定使用的 API Key
            string finalKey = apiKey;
            if (string.IsNullOrEmpty(finalKey) && HasDefaultKey)
            {
                finalKey = KeySecurity.Decrypt(_encryptedDefaultKey);
            }

            if (string.IsNullOrEmpty(finalKey))
            {
                Plugin.Logger?.LogWarning("[WeatherAPI] 未配置 API Key 且无内置 Key");
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&unit=c";
            Plugin.Logger?.LogInfo($"[WeatherAPI] 发起请求: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Plugin.Logger?.LogWarning($"[WeatherAPI] 请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var weather = ParseWeatherJson(request.downloadHandler.text);
                    if (weather != null)
                    {
                        _cachedWeather = weather;
                        _lastFetchTime = DateTime.Now;
                        _lastLocation = normalizedLocation;
                        Plugin.Logger?.LogInfo($"[WeatherAPI] 数据更新: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        Plugin.Logger?.LogWarning($"[WeatherAPI] 解析失败");
                        onComplete?.Invoke(null);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherAPI] 解析异常: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// 获取日出日落数据
        /// </summary>
        public static IEnumerator FetchSunSchedule(string apiKey, string location, Action<SunData> onComplete)
        {
            string finalKey = apiKey;
            if (string.IsNullOrEmpty(finalKey) && HasDefaultKey)
            {
                finalKey = KeySecurity.Decrypt(_encryptedDefaultKey);
            }

            if (string.IsNullOrEmpty(finalKey))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/geo/sun.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&start=0&days=1";
            Plugin.Logger?.LogInfo($"[WeatherAPI] 请求日出日落: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Plugin.Logger?.LogWarning($"[WeatherAPI] 日出日落请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var sunData = ParseSunJson(request.downloadHandler.text);
                    onComplete?.Invoke(sunData);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[WeatherAPI] 日出日落解析失败: {ex}");
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// 清除缓存（如切换城市时）
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedWeather = null;
            _lastLocation = null;
            _lastFetchTime = DateTime.MinValue;
        }

        /// <summary>
        /// 解析天气JSON响应
        /// </summary>
        private static WeatherInfo ParseWeatherJson(string json)
        {
            try
            {
                if (json.Contains("\"status\"") && !json.Contains("\"results\""))
                    return null;

                int nowIndex = json.IndexOf("\"now\"");
                if (nowIndex < 0)
                    return null;

                int code = ExtractIntValue(json, "\"code\":\"", "\"");
                int temp = ExtractIntValue(json, "\"temperature\":\"", "\"");
                string text = ExtractStringValue(json, "\"text\":\"", "\"");

                if (string.IsNullOrEmpty(text))
                    return null;

                return new WeatherInfo
                {
                    Code = code,
                    Text = text,
                    Temperature = temp,
                    Condition = MapCodeToCondition(code),
                    UpdateTime = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析日出日落JSON响应
        /// </summary>
        private static SunData ParseSunJson(string json)
        {
            int sunIndex = json.IndexOf("\"sun\"");
            if (sunIndex < 0)
                return null;

            string sunrise = ExtractStringValue(json, "\"sunrise\":\"", "\"");
            string sunset = ExtractStringValue(json, "\"sunset\":\"", "\"");

            if (!string.IsNullOrEmpty(sunrise) && !string.IsNullOrEmpty(sunset))
            {
                return new SunData { Sunrise = sunrise, Sunset = sunset };
            }
            return null;
        }

        private static int ExtractIntValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix);
            if (start < 0) return 0;
            start += prefix.Length;
            int end = json.IndexOf(suffix, start);
            if (end < 0) return 0;
            string val = json.Substring(start, end - start);
            int.TryParse(val, out int res);
            return res;
        }

        private static string ExtractStringValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix);
            if (start < 0) return null;
            start += prefix.Length;
            int end = json.IndexOf(suffix, start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        /// <summary>
        /// 将心知天气代码映射到天气状况枚举
        /// </summary>
        public static WeatherCondition MapCodeToCondition(int code)
        {
            if (code >= 0 && code <= 3) return WeatherCondition.Clear;       // 晴
            if (code >= 4 && code <= 9) return WeatherCondition.Cloudy;      // 阴/多云
            if (code >= 10 && code <= 20) return WeatherCondition.Rainy;     // 雨
            if (code >= 21 && code <= 25) return WeatherCondition.Snowy;     // 雪
            if (code >= 30 && code <= 32) return WeatherCondition.Foggy;     // 雾/霾
            return WeatherCondition.Unknown;
        }
    }
}
