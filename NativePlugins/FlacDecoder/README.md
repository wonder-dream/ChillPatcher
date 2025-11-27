# ChillPatcher FLAC Decoder - Native Plugin

## 概述

这是 ChillPatcher 的 FLAC 解码器 Native Plugin，使用 C++ 和 [dr_flac](https://github.com/mackron/dr_libs) 实现高性能、准确的 FLAC 音频解码。

## 特性

✅ **完全准确的 FLAC 解码**
- 使用 dr_flac 单头文件库（无依赖）
- 支持所有 FLAC 采样率（8kHz ~ 192kHz）
- 支持所有声道配置（1-8 声道）
- 输出标准化 float PCM 数据（[-1.0, 1.0]）

✅ **性能优化**
- 后台线程解码（避免阻塞游戏主线程）
- 内存高效（直接创建 Unity AudioClip）
- 自动回退（Native 不可用时使用 Unity 内置解码）

✅ **跨平台兼容**
- x64 和 x86 版本
- 静态链接运行时库（无 MSVC 依赖）

## 项目结构

```
NativePlugins/FlacDecoder/
├── build.bat              # Windows 构建脚本
├── CMakeLists.txt         # CMake 配置
├── include/
│   └── flac_decoder.h     # C API 头文件
├── src/
│   └── flac_decoder.cpp   # FLAC 解码器实现
└── build/                 # 构建输出目录
    ├── x64/
    └── x86/
```

## 构建

### 前置条件

- CMake 3.15+
- Visual Studio 2019/2022（含 C++ 工具链）
- Git（用于子模块）

### 构建步骤

```batch
cd NativePlugins/FlacDecoder
build.bat
```

构建脚本会：
1. 自动检测 Visual Studio
2. 编译 x64 和 x86 两个版本
3. 复制 DLL 到 `bin/native/` 目录

### 输出文件

- `bin/native/x64/ChillFlacDecoder.dll` - 64位版本
- `bin/native/x86/ChillFlacDecoder.dll` - 32位版本

## C API 接口

### FlacAudioInfo 结构

```c
typedef struct {
    int sample_rate;       // 采样率（如 44100, 48000）
    int channels;          // 声道数（1=单声道, 2=立体声）
    unsigned long long total_pcm_frame_count;  // PCM 帧总数
    float* pcm_data;       // PCM 数据（交错格式，范围 [-1.0, 1.0]）
    size_t pcm_data_size;  // PCM 数据字节数
} FlacAudioInfo;
```

### 函数

#### DecodeFlacFile
```c
int DecodeFlacFile(const char* file_path, FlacAudioInfo* out_info);
```
解码 FLAC 文件为 PCM 数据。

**参数：**
- `file_path`: FLAC 文件路径（UTF-8）
- `out_info`: 输出音频信息

**返回值：**
- `0`: 成功
- `-1`: 参数无效
- `-2`: 文件打开失败
- `-3`: 内存分配失败
- `-4`: PCM 读取失败

#### FreeFlacData
```c
void FreeFlacData(FlacAudioInfo* info);
```
释放解码后的 PCM 数据（必须调用）。

#### FlacGetLastError
```c
const char* FlacGetLastError();
```
获取最后的错误消息（UTF-8）。

## C# 集成

### FlacDecoder 类

```csharp
using ChillPatcher.Native;

// 解码 FLAC 文件并创建 Unity AudioClip
AudioClip clip = FlacDecoder.DecodeFlacToAudioClip(
    "path/to/music.flac", 
    "My Music"
);

// 检查 Native Plugin 是否可用
bool available = FlacDecoder.IsAvailable();
```

### 自动回退机制

如果 Native Plugin 不可用（DLL 缺失或版本不匹配），系统会自动回退到 Unity 内置的音频解码器（AudioType.UNKNOWN）。

## 技术细节

### 为什么需要 Native Plugin？

Unity 默认使用 `UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN)` 加载 FLAC，这依赖于平台相关的 FMOD 后端：

**问题：**
- ❌ 采样率可能错误识别（导致播放速度不对）
- ❌ 某些平台不支持 FLAC
- ❌ 行为不一致（Windows Editor 可用，Standalone 可能失败）

**解决方案：**
- ✅ 使用 dr_flac 保证跨平台一致性
- ✅ 准确读取 FLAC 元数据（采样率、声道）
- ✅ 输出标准化 PCM 数据

### dr_flac 库

- 单头文件库：`#include "dr_flac.h"`
- MIT 许可证
- 已被 Godot、Unreal 等引擎使用
- GitHub: https://github.com/mackron/dr_libs

## 许可证

- **dr_flac**: MIT License（by David Reid）
- **ChillPatcher FLAC Decoder**: 继承 ChillPatcher 主项目许可

## 故障排除

### DLL 加载失败

**症状：** 日志显示 `Native FLAC decoder not available`

**解决：**
1. 确认 DLL 文件存在于 `BepInEx/plugins/ChillPatcher/native/` 目录
2. 检查游戏是 x64 还是 x86，使用对应版本的 DLL
3. 安装 Visual C++ Redistributable（如果使用动态链接）

### 编译错误

**CMake 未找到：**
```
ERROR: CMake not found in PATH
```
→ 安装 CMake: https://cmake.org/download/

**Visual Studio 未找到：**
→ 安装 Visual Studio 2019/2022 并选择 "C++ 桌面开发" 工作负载

## 贡献

欢迎提交 Issue 和 Pull Request！

特别关注：
- 跨平台兼容性（Linux/macOS）
- 性能优化建议
- Bug 修复
