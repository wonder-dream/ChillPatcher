using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace ChillPatcher.Patches
{
    /// <summary>
    /// 全局键盘钩子补丁 - 用于在壁纸引擎中捕获桌面键盘输入
    /// </summary>
    public class KeyboardHookPatch
    {
        private static IntPtr hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc hookCallback;
        private static readonly Queue<char> inputQueue = new Queue<char>();
        private static readonly object queueLock = new object();
        private static Thread hookThread;
        private static bool isRunning = false;
        
        // Windows API
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        // 常量
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 初始化键盘钩子
        /// </summary>
        public static void Initialize()
        {
            if (hookThread != null && hookThread.IsAlive)
            {
                Plugin.Logger.LogWarning("[KeyboardHook] 钩子线程已经在运行");
                return;
            }

            isRunning = true;
            hookThread = new Thread(HookThreadProc);
            hookThread.IsBackground = true;
            hookThread.Start();
            
            Plugin.Logger.LogInfo("[KeyboardHook] 钩子线程已启动");
        }

        /// <summary>
        /// 钩子线程过程
        /// </summary>
        private static void HookThreadProc()
        {
            try
            {
                hookCallback = HookCallback;
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, GetModuleHandle(curModule.ModuleName), 0);
                }

                if (hookId == IntPtr.Zero)
                {
                    Plugin.Logger.LogError("[KeyboardHook] 钩子设置失败");
                    return;
                }

                Plugin.Logger.LogInfo("[KeyboardHook] 钩子设置成功，开始消息循环");

                // 消息循环
                MSG msg;
                while (isRunning && GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                Plugin.Logger.LogInfo("[KeyboardHook] 消息循环退出");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[KeyboardHook] 线程异常: {ex}");
            }
            finally
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// 清理键盘钩子
        /// </summary>
        public static void Cleanup()
        {
            isRunning = false;
            
            if (hookThread != null && hookThread.IsAlive)
            {
                PostQuitMessage(0);
                hookThread.Join(1000); // 等待最多1秒
            }

            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            Plugin.Logger.LogInfo("[KeyboardHook] 钩子已清理");
        }

        /// <summary>
        /// 检查当前前台窗口是否是桌面
        /// </summary>
        private static bool IsDesktopActive()
        {
            IntPtr hwnd = GetForegroundWindow();
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, 256);
            string classNameStr = className.ToString();

            // 桌面窗口类名：Progman, WorkerW, SysListView32
            return classNameStr == "Progman" || classNameStr == "WorkerW" || classNameStr == "SysListView32";
        }

        /// <summary>
        /// 键盘钩子回调
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;

                // 检测是否在桌面
                bool isDesktop = IsDesktopActive();
                
                if (isDesktop)
                {
                    Plugin.Logger.LogInfo($"[KeyboardHook] 桌面激活，捕获按键: VK={vkCode}");
                    
                    char? inputChar = null;
                    
                    // 处理特殊按键
                    if (vkCode == 0x08) // Backspace
                    {
                        inputChar = '\b';
                        Plugin.Logger.LogInfo("[KeyboardHook] 捕获: Backspace");
                    }
                    else if (vkCode == 0x0D) // Enter
                    {
                        inputChar = '\n';
                        Plugin.Logger.LogInfo("[KeyboardHook] 捕获: Enter");
                    }
                    else if (vkCode >= 0x20 && vkCode <= 0x7E) // 可打印字符
                    {
                        // 转换为字符
                        char ch = (char)vkCode;
                        
                        // 检查Shift状态
                        bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                        
                        // 处理大小写
                        if (ch >= 'A' && ch <= 'Z')
                        {
                            if (!isShiftPressed)
                            {
                                ch = char.ToLower(ch);
                            }
                        }
                        
                        inputChar = ch;
                        Plugin.Logger.LogInfo($"[KeyboardHook] 捕获字符: {ch}");
                    }

                    // 添加到队列
                    if (inputChar.HasValue)
                    {
                        lock (queueLock)
                        {
                            inputQueue.Enqueue(inputChar.Value);
                        }
                    }
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// 获取并清空输入队列
        /// </summary>
        public static string GetAndClearInputBuffer()
        {
            lock (queueLock)
            {
                if (inputQueue.Count == 0)
                    return string.Empty;

                StringBuilder result = new StringBuilder();
                while (inputQueue.Count > 0)
                {
                    result.Append(inputQueue.Dequeue());
                }
                return result.ToString();
            }
        }
    }

    /// <summary>
    /// Patch Update - 每帧检测鼠标点击，清空输入队列
    /// </summary>
    [HarmonyPatch(typeof(EventSystem), "Update")]
    public class EventSystem_Update_Patch
    {
        static void Prefix()
        {
            // 检测鼠标点击（左键按下）
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                // 清空输入队列
                KeyboardHookPatch.GetAndClearInputBuffer();
            }
        }
    }

    /// <summary>
    /// Patch TMP_InputField 来注入键盘输入
    /// </summary>
    [HarmonyPatch(typeof(TMP_InputField), "LateUpdate")]
    public class TMP_InputField_LateUpdate_Patch
    {
        private static System.Reflection.MethodInfo keyPressedMethod = null;

        static void Postfix(TMP_InputField __instance)
        {
            // 只在输入框激活且获得焦点时注入
            if (!__instance.isFocused)
                return;

            string input = KeyboardHookPatch.GetAndClearInputBuffer();
            if (string.IsNullOrEmpty(input))
                return;

            // 获取KeyPressed方法（只获取一次）
            if (keyPressedMethod == null)
            {
                keyPressedMethod = typeof(TMP_InputField).GetMethod("KeyPressed",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            if (keyPressedMethod == null)
            {
                Plugin.Logger.LogError("[桌面输入] 无法获取KeyPressed方法");
                return;
            }

            // 调用KeyPressed处理每个字符
            foreach (char c in input)
            {
                UnityEngine.Event evt = new UnityEngine.Event();
                evt.type = UnityEngine.EventType.KeyDown;

                if (c == '\b')
                {
                    evt.keyCode = KeyCode.Backspace;
                    evt.character = '\0';
                }
                else if (c == '\n')
                {
                    evt.keyCode = KeyCode.Return;
                    evt.character = '\n';
                }
                else
                {
                    evt.keyCode = KeyCode.None;
                    evt.character = c;
                }

                // 调用KeyPressed（返回EditState枚举）
                keyPressedMethod.Invoke(__instance, new object[] { evt });
            }

            // 强制更新显示
            __instance.ForceLabelUpdate();
        }
    }
}
