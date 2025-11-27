using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul;
using ChillPatcher.UIFramework.Data;
using HarmonyLib;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 拦截MusicService的播放顺序操作，自定义Tag存储到数据库
    /// </summary>
    [HarmonyPatch(typeof(MusicService))]
    public class MusicService_PlaylistOrder_Patch
    {
        /// <summary>
        /// 拦截添加音乐到播放列表（AddMusicItem）
        /// </summary>
        [HarmonyPatch("AddMusicItem")]
        [HarmonyPostfix]
        static void AddMusicItem_Postfix(bool __result, GameAudioInfo music)
        {
            try
            {
                // 只在成功添加后处理
                if (!__result || music == null)
                    return;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(music.Tag))
                {
                    // 自定义Tag → 添加到数据库的播放顺序
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(music);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            manager.AddToPlaylistOrder(tagId, music.UUID);
                            Plugin.Log.LogInfo($"[PlaylistOrder] 添加到数据库: Tag={tagId}, UUID={music.UUID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PlaylistOrder] 添加失败: {ex}");
            }
        }

        /// <summary>
        /// 拦截添加本地音乐（AddLocalMusicItem）
        /// </summary>
        [HarmonyPatch("AddLocalMusicItem")]
        [HarmonyPostfix]
        static void AddLocalMusicItem_Postfix(bool __result, GameAudioInfo music)
        {
            try
            {
                // 只在成功添加后处理
                if (!__result || music == null)
                    return;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(music.Tag))
                {
                    // 自定义Tag → 添加到数据库的播放顺序
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(music);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            manager.AddToPlaylistOrder(tagId, music.UUID);
                            Plugin.Log.LogInfo($"[PlaylistOrder] 添加本地音乐到数据库: Tag={tagId}, UUID={music.UUID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PlaylistOrder] 添加本地音乐失败: {ex}");
            }
        }

        /// <summary>
        /// 拦截移除本地音乐（RemoveLocalMusicItem）
        /// </summary>
        [HarmonyPatch("RemoveLocalMusicItem")]
        [HarmonyPostfix]
        static void RemoveLocalMusicItem_Postfix(GameAudioInfo music)
        {
            try
            {
                if (music == null)
                    return;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(music.Tag))
                {
                    // 自定义Tag → 从数据库移除
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(music);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            manager.RemoveFromPlaylistOrder(tagId, music.UUID);
                            manager.RemoveFavorite(tagId, music.UUID); // 同时移除收藏
                            Plugin.Log.LogInfo($"[PlaylistOrder] 从数据库移除: Tag={tagId}, UUID={music.UUID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PlaylistOrder] 移除失败: {ex}");
            }
        }

        /// <summary>
        /// 拦截交换顺序（SwapAfter）
        /// </summary>
        [HarmonyPatch("SwapAfter")]
        [HarmonyPostfix]
        static void SwapAfter_Postfix(MusicService __instance, GameAudioInfo target, GameAudioInfo origin)
        {
            try
            {
                if (target == null)
                    return;

                // 检查是否在收藏模式或随机播放模式
                var currentAudioTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag;
                var currentValue = currentAudioTag.CurrentValue;
                var isShuffle = Traverse.Create(__instance).Property("IsShuffle").GetValue<bool>();

                // 随机播放不保存顺序
                if (isShuffle)
                    return;

                // ✅ 判断是否是自定义Tag
                if (CustomPlaylistDataManager.IsCustomTag(target.Tag))
                {
                    var tagId = CustomPlaylistDataManager.GetTagIdFromAudio(target);
                    
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        var manager = CustomPlaylistDataManager.Instance;
                        if (manager != null)
                        {
                            // 如果是收藏模式，不更新播放顺序（只更新收藏列表）
                            if (currentValue == AudioTag.Favorite)
                            {
                                // 收藏模式下不需要更新数据库（收藏列表不需要排序）
                                return;
                            }

                            // 获取当前所有音乐列表
                            var allMusicList = Traverse.Create(__instance)
                                .Field("_allMusicList")
                                .GetValue<List<GameAudioInfo>>();

                            if (allMusicList != null)
                            {
                                // 提取同Tag的所有UUID（按当前顺序）
                                var sameTagUuids = allMusicList
                                    .Where(m => CustomPlaylistDataManager.GetTagIdFromAudio(m) == tagId)
                                    .Select(m => m.UUID)
                                    .ToList();

                                // 更新数据库中的播放顺序
                                manager.SetPlaylistOrder(tagId, sameTagUuids);
                                
                                Plugin.Log.LogInfo($"[PlaylistOrder] 更新排序到数据库: Tag={tagId}, Count={sameTagUuids.Count}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PlaylistOrder] 交换顺序失败: {ex}");
            }
        }
    }
}
