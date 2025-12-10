using System.Collections.Generic;
using Bulbul;

namespace ChillPatcher.WeatherSync.Utils
{
    /// <summary>
    /// 环境控制器注册表 - 缓存游戏中的环境控制器实例
    /// </summary>
    internal static class EnvRegistry
    {
        private static readonly Dictionary<EnvironmentType, EnviromentController> _map =
            new Dictionary<EnvironmentType, EnviromentController>();

        /// <summary>
        /// 尝试获取指定类型的环境控制器
        /// </summary>
        public static bool TryGet(EnvironmentType type, out EnviromentController ctrl)
        {
            return _map.TryGetValue(type, out ctrl);
        }

        /// <summary>
        /// 注册环境控制器
        /// </summary>
        public static void Register(EnvironmentType type, EnviromentController ctrl)
        {
            if (ctrl != null)
            {
                _map[type] = ctrl;
            }
        }

        /// <summary>
        /// 已注册的环境控制器数量
        /// </summary>
        public static int Count => _map.Count;

        /// <summary>
        /// 清空注册表
        /// </summary>
        public static void Clear()
        {
            _map.Clear();
        }
    }
}
