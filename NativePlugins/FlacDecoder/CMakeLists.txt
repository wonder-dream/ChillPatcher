cmake_minimum_required(VERSION 3.15)
project(ChillPatcher_FlacDecoder VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_C_STANDARD 11)

# 设置输出目录
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# dr_flac 头文件路径
include_directories(${CMAKE_SOURCE_DIR}/../dr_libs)
include_directories(${CMAKE_SOURCE_DIR}/include)

# 源文件
set(SOURCES
    src/flac_decoder.cpp
)

# 创建动态库
add_library(ChillFlacDecoder SHARED ${SOURCES})

# Windows 特定设置
if(WIN32)
    # 导出所有符号
    set_target_properties(ChillFlacDecoder PROPERTIES
        PREFIX ""
        SUFFIX ".dll"
        WINDOWS_EXPORT_ALL_SYMBOLS ON
    )
    
    # 链接运行时库
    if(MSVC)
        # 使用静态运行时库（避免依赖 MSVC 运行时 DLL）
        set_property(TARGET ChillFlacDecoder PROPERTY
            MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()
endif()

# 安装规则 - 复制到项目 bin/native 目录
set(NATIVE_OUTPUT_DIR "${CMAKE_SOURCE_DIR}/../../bin/native")
install(TARGETS ChillFlacDecoder
    RUNTIME DESTINATION "${NATIVE_OUTPUT_DIR}/$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>"
    LIBRARY DESTINATION "${NATIVE_OUTPUT_DIR}/$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>"
)

# 打印配置信息
message(STATUS "========================================")
message(STATUS "ChillPatcher FLAC Decoder Native Plugin")
message(STATUS "========================================")
message(STATUS "Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "Architecture: ${CMAKE_SYSTEM_PROCESSOR}")
message(STATUS "Output: ${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")
message(STATUS "========================================")
