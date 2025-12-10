using System;
using ChillPatcher.WeatherSync.Models;

namespace ChillPatcher.WeatherSync.Utils
{
    /// <summary>
    /// 时间与季节工具类
    /// </summary>
    internal static class TimeUtils
    {
        /// <summary>
        /// 获取当前季节
        /// </summary>
        public static Season GetCurrentSeason()
        {
            int month = DateTime.Now.Month;
            if (month >= 3 && month <= 5) return Season.Spring;
            if (month >= 6 && month <= 8) return Season.Summer;
            if (month >= 9 && month <= 11) return Season.Autumn;
            return Season.Winter;
        }

        /// <summary>
        /// 判断当前是否为白天
        /// </summary>
        public static bool IsDay(TimeSpan sunrise, TimeSpan sunset)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan sunsetStart = sunset.Subtract(TimeSpan.FromMinutes(30));
            return now >= sunrise && now < sunsetStart;
        }

        /// <summary>
        /// 判断当前是否为夜晚
        /// </summary>
        public static bool IsNight(TimeSpan sunrise, TimeSpan sunset)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan sunsetEnd = sunset.Add(TimeSpan.FromMinutes(30));
            return now >= sunsetEnd || now < sunrise;
        }

        /// <summary>
        /// 判断当前是否为黄昏
        /// </summary>
        public static bool IsSunset(TimeSpan sunrise, TimeSpan sunset)
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan sunsetStart = sunset.Subtract(TimeSpan.FromMinutes(30));
            TimeSpan sunsetEnd = sunset.Add(TimeSpan.FromMinutes(30));
            return now >= sunsetStart && now < sunsetEnd;
        }

        /// <summary>
        /// 判断是否为农历新年期间（正月初一到初五）
        /// </summary>
        public static bool IsLunarNewYearPeriod(DateTime date)
        {
            try
            {
                var chineseCal = new System.Globalization.ChineseLunisolarCalendar();
                int lunarMonth = chineseCal.GetMonth(date);
                int lunarDay = chineseCal.GetDayOfMonth(date);
                int leapMonth = chineseCal.GetLeapMonth(chineseCal.GetYear(date));

                // 处理闰月情况
                if (leapMonth > 0 && lunarMonth >= leapMonth)
                {
                    lunarMonth--;
                }

                // 正月初一到初五 或 除夕（腊月三十/二十九）
                if (lunarMonth == 1 && lunarDay >= 1 && lunarDay <= 5)
                    return true;

                // 除夕（腊月最后一天）
                if (lunarMonth == 12)
                {
                    int daysInMonth = chineseCal.GetDaysInMonth(chineseCal.GetYear(date), 12);
                    if (lunarDay == daysInMonth)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取详细时间段描述
        /// </summary>
        public static string GetDetailedTimeSegment()
        {
            int hour = DateTime.Now.Hour;

            if (hour >= 0 && hour < 5) return "凌晨";
            if (hour >= 5 && hour < 7) return "清晨";
            if (hour >= 7 && hour < 11) return "上午";
            if (hour >= 11 && hour < 13) return "中午";
            if (hour >= 13 && hour < 18) return "下午";
            if (hour >= 18 && hour < 19) return "傍晚";
            return "晚上";
        }
    }
}
