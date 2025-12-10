using BepInEx.Configuration;

namespace ChillPatcher.WeatherSync
{
    /// <summary>
    /// 天气同步功能配置
    /// </summary>
    public static class WeatherSyncConfig
    {
        // 天气同步设置
        public static ConfigEntry<bool> EnableWeatherSync { get; private set; }
        public static ConfigEntry<int> WeatherRefreshMinutes { get; private set; }
        public static ConfigEntry<string> SeniverseKey { get; private set; }
        public static ConfigEntry<string> Location { get; private set; }

        // 时间设置
        public static ConfigEntry<string> SunriseTime { get; private set; }
        public static ConfigEntry<string> SunsetTime { get; private set; }

        // 解锁设置
        public static ConfigEntry<bool> UnlockEnvironments { get; private set; }
        public static ConfigEntry<bool> UnlockDecorations { get; private set; }

        // UI 设置
        public static ConfigEntry<bool> ShowWeatherOnUI { get; private set; }
        public static ConfigEntry<bool> DetailedTimeSegments { get; private set; }

        // 彩蛋设置
        public static ConfigEntry<bool> EnableEasterEggs { get; private set; }

        // 调试设置
        public static ConfigEntry<bool> DebugMode { get; private set; }
        public static ConfigEntry<int> DebugCode { get; private set; }
        public static ConfigEntry<int> DebugTemp { get; private set; }
        public static ConfigEntry<string> DebugText { get; private set; }

        // 内部状态
        public static ConfigEntry<string> LastSunSyncDate { get; private set; }

        /// <summary>
        /// 初始化配置
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            // 天气同步
            EnableWeatherSync = config.Bind(
                "WeatherSync",
                "EnableWeatherSync",
                false,
                "是否启用天气API同步（需要网络连接）");

            WeatherRefreshMinutes = config.Bind(
                "WeatherSync",
                "RefreshMinutes",
                30,
                "天气API刷新间隔（分钟）");

            SeniverseKey = config.Bind(
                "WeatherSync",
                "SeniverseKey",
                "",
                "心知天气 API Key（留空使用内置key）");

            Location = config.Bind(
                "WeatherSync",
                "Location",
                "beijing",
                "城市名称（拼音或中文，如 beijing、上海）");

            // 时间设置
            SunriseTime = config.Bind(
                "WeatherSync.Time",
                "Sunrise",
                "06:30",
                "日出时间（HH:mm格式）");

            SunsetTime = config.Bind(
                "WeatherSync.Time",
                "Sunset",
                "18:30",
                "日落时间（HH:mm格式）");

            // 解锁设置
            UnlockEnvironments = config.Bind(
                "WeatherSync.Unlock",
                "UnlockAllEnvironments",
                false,
                "自动解锁所有环境（不写入存档）");

            UnlockDecorations = config.Bind(
                "WeatherSync.Unlock",
                "UnlockAllDecorations",
                false,
                "自动解锁所有装饰品（不写入存档）");

            // UI 设置
            ShowWeatherOnUI = config.Bind(
                "WeatherSync.UI",
                "ShowWeatherOnDate",
                true,
                "在日期栏显示天气信息");

            DetailedTimeSegments = config.Bind(
                "WeatherSync.UI",
                "DetailedTimeSegments",
                true,
                "使用12小时制时显示详细时段（凌晨/清晨/上午等）");

            // 彩蛋设置
            EnableEasterEggs = config.Bind(
                "WeatherSync.Automation",
                "EnableSeasonalEasterEggs",
                true,
                "启用季节性彩蛋与环境音效自动托管");

            // 调试设置
            DebugMode = config.Bind(
                "WeatherSync.Debug",
                "EnableDebugMode",
                false,
                "调试模式（使用模拟天气数据）");

            DebugCode = config.Bind(
                "WeatherSync.Debug",
                "SimulatedCode",
                1,
                "模拟天气代码");

            DebugTemp = config.Bind(
                "WeatherSync.Debug",
                "SimulatedTemp",
                25,
                "模拟温度");

            DebugText = config.Bind(
                "WeatherSync.Debug",
                "SimulatedText",
                "DebugWeather",
                "模拟天气描述");

            // 内部状态
            LastSunSyncDate = config.Bind(
                "WeatherSync.Internal",
                "LastSunSyncDate",
                "",
                new ConfigDescription("上次同步日出日落的日期", null, new ConfigurationManagerAttributes { Browsable = false }));
        }
    }

    /// <summary>
    /// 配置属性特性（用于隐藏内部配置）
    /// </summary>
    internal class ConfigurationManagerAttributes
    {
        public bool Browsable { get; set; } = true;
    }
}
