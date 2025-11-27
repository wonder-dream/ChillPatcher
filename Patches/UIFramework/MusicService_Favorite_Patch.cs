using System;
using Bulbul;
using ChillPatcher.UIFramework.Data;
using HarmonyLib;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 拦截MusicService的收藏操作，自定义Tag存储到数据库
    /// </summary>
    [HarmonyPatch(typeof(MusicService))]
    public class MusicService_Favorite_Patch
    {
        /// <summary>
        /// 拦截添加收藏
        /// </summary>
        [HarmonyPatch("RegisterFavoriteMusic")]
        [HarmonyPrefix]
        static bool RegisterFavoriteMusic_Prefix(GameAudioInfo gameAudioInfo)
        {
            try
            {
                if (gameAudioInfo == null)
                    return true;

                // 检查是否已经收藏
                if (gameAudioInfo.Tag.HasFlagFast(AudioTag.Favorite))
                    return false;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(gameAudioInfo.Tag))
                {
                    // 自定义Tag → 存储到数据库
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(gameAudioInfo);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            // 添加收藏标记
                            gameAudioInfo.Tag = gameAudioInfo.Tag | AudioTag.Favorite;
                            
                            // 保存到数据库
                            manager.AddFavorite(tagId, gameAudioInfo.UUID);
                            
                            Plugin.Log.LogInfo($"[Favorite] 添加到数据库: Tag={tagId}, UUID={gameAudioInfo.UUID}");
                            
                            // 阻止原方法执行（不保存到存档）
                            return false;
                        }
                    }
                }
                
                // 非自定义Tag → 执行原逻辑（保存到存档）
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Favorite] 添加收藏失败: {ex}");
                return true; // 出错时执行原逻辑
            }
        }

        /// <summary>
        /// 拦截移除收藏
        /// </summary>
        [HarmonyPatch("UnregisterFavoriteMusic")]
        [HarmonyPrefix]
        static bool UnregisterFavoriteMusic_Prefix(MusicService __instance, GameAudioInfo gameAudioInfo)
        {
            try
            {
                if (gameAudioInfo == null)
                    return true;

                // 检查是否已收藏
                if (!gameAudioInfo.Tag.HasFlagFast(AudioTag.Favorite))
                    return false;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(gameAudioInfo.Tag))
                {
                    // 自定义Tag → 从数据库移除
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(gameAudioInfo);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            // 移除收藏标记
                            gameAudioInfo.Tag = gameAudioInfo.Tag & ~AudioTag.Favorite;
                            
                            // 更新播放列表（复制原逻辑）
                            var currentAudioTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag;
                            var currentValue = currentAudioTag.CurrentValue;
                            
                            if (!currentValue.HasFlagFast(gameAudioInfo.Tag))
                            {
                                var currentPlayList = Traverse.Create(__instance).Field("CurrentPlayList").GetValue<System.Collections.Generic.List<GameAudioInfo>>();
                                var shuffleList = Traverse.Create(__instance).Field("shuffleList").GetValue<System.Collections.Generic.List<GameAudioInfo>>();
                                
                                currentPlayList?.Remove(gameAudioInfo);
                                shuffleList?.Remove(gameAudioInfo);
                            }
                            
                            // 从数据库移除
                            manager.RemoveFavorite(tagId, gameAudioInfo.UUID);
                            
                            Plugin.Log.LogInfo($"[Favorite] 从数据库移除: Tag={tagId}, UUID={gameAudioInfo.UUID}");
                            
                            // 阻止原方法执行（不保存到存档）
                            return false;
                        }
                    }
                }
                
                // 非自定义Tag → 执行原逻辑（保存到存档）
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Favorite] 移除收藏失败: {ex}");
                return true; // 出错时执行原逻辑
            }
        }
    }
}
