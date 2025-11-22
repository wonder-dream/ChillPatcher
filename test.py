import time
import ctypes
from pynput import keyboard

# Windows API 定义
user32 = ctypes.windll.user32
GetForegroundWindow = user32.GetForegroundWindow
GetClassName = user32.GetClassNameW

def is_desktop_active():
    hwnd = GetForegroundWindow()
    buff = ctypes.create_unicode_buffer(256)
    GetClassName(hwnd, buff, 256)
    class_name = buff.value
    
    # Progman 是标准桌面，WorkerW 是壁纸切换后的桌面容器
    # SysListView32 是直接选中桌面图标时的控件类名
    target_classes = ["Progman", "WorkerW", "SysListView32"]
    
    if class_name in target_classes:
        # 进一步验证：如果是 WorkerW，确保它确实是桌面而不是其他工具窗口
        # 这里为了快速检验，只要类名匹配就算通过
        return True
    return False

def on_press(key):
    if not is_desktop_active():
        return # 不在桌面，直接忽略，丢弃

    try:
        print(f"桌面捕获: {key.char}")
    except AttributeError:
        print(f"桌面捕获: {key}")

# 监听键盘
print("正在监听... 请点击桌面背景并打字（其他窗口输入会被忽略）")
with keyboard.Listener(on_press=on_press) as listener:
    listener.join()