using System;

namespace ChillPatcher.WeatherSync.Models
{
    /// <summary>
    /// 天气状况枚举
    /// </summary>
    public enum WeatherCondition
    {
        Clear,      // 晴朗
        Cloudy,     // 多云/阴天
        Rainy,      // 下雨
        Snowy,      // 下雪
        Foggy,      // 雾
        Unknown     // 未知
    }

    /// <summary>
    /// 天气信息
    /// </summary>
    public class WeatherInfo
    {
        /// <summary>
        /// 天气状况
        /// </summary>
        public WeatherCondition Condition { get; set; }

        /// <summary>
        /// 温度（摄氏度）
        /// </summary>
        public int Temperature { get; set; }

        /// <summary>
        /// 天气描述文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 心知天气 API 的天气代码
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        public override string ToString()
        {
            return $"{Text}({Condition}), {Temperature}°C, Code={Code}";
        }
    }

    /// <summary>
    /// 日出日落数据
    /// </summary>
    public class SunData
    {
        /// <summary>
        /// 日出时间（HH:mm格式）
        /// </summary>
        public string Sunrise { get; set; }

        /// <summary>
        /// 日落时间（HH:mm格式）
        /// </summary>
        public string Sunset { get; set; }
    }

    /// <summary>
    /// 季节枚举
    /// </summary>
    public enum Season
    {
        Spring,     // 春季 (3-5月)
        Summer,     // 夏季 (6-8月)
        Autumn,     // 秋季 (9-11月)
        Winter      // 冬季 (12-2月)
    }
}
