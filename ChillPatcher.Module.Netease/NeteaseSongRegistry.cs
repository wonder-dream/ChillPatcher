using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ChillPatcher.SDK.Interfaces;
using ChillPatcher.SDK.Models;

namespace ChillPatcher.Module.Netease
{
    /// <summary>
    /// 网易云歌曲注册管理器
    /// 处理歌曲和专辑的注册逻辑
    /// </summary>
    public class NeteaseSongRegistry
    {
        private readonly ManualLogSource _logger;
        private readonly IModuleContext _context;
        private readonly string _moduleId;
        private readonly Dictionary<string, NeteaseBridge.SongInfo> _songInfoMap;
        private readonly NeteaseFavoriteManager _favoriteManager;

        public const string TAG_FAVORITES = "netease_favorites";
        public const string FAVORITES_ALBUM_ID = "netease_favorites_album";

        public NeteaseSongRegistry(
            IModuleContext context,
            string moduleId,
            Dictionary<string, NeteaseBridge.SongInfo> songInfoMap,
            NeteaseFavoriteManager favoriteManager,
            ManualLogSource logger)
        {
            _context = context;
            _moduleId = moduleId;
            _songInfoMap = songInfoMap;
            _favoriteManager = favoriteManager;
            _logger = logger;
        }

        /// <summary>
        /// 注册收藏专辑
        /// </summary>
        public void RegisterFavoritesAlbum(int songCount)
        {
            var album = new AlbumInfo
            {
                AlbumId = FAVORITES_ALBUM_ID,
                DisplayName = "网易云音乐收藏",
                Artist = "网易云音乐",
                TagIds = new List<string> { TAG_FAVORITES },
                ModuleId = _moduleId,
                SongCount = songCount,
                SortOrder = 0,
                IsGrowableAlbum = false,
                ExtendedData = "FAVORITES"
            };
            _context.AlbumRegistry.RegisterAlbum(album, _moduleId);
        }

        /// <summary>
        /// 注册收藏歌曲列表
        /// 歌曲属于收藏 Tag 的收藏专辑
        /// </summary>
        public List<MusicInfo> RegisterFavoritesSongs(IEnumerable<NeteaseBridge.SongInfo> songs)
        {
            var musicList = new List<MusicInfo>();

            foreach (var song in songs)
            {
                var uuid = GenerateUUID(song.Id);
                var isLiked = _favoriteManager.IsSongLiked(song.Id);

                var musicInfo = new MusicInfo
                {
                    UUID = uuid,
                    Title = song.Name,
                    Artist = song.ArtistName,
                    AlbumId = FAVORITES_ALBUM_ID, // 收藏专辑
                    TagId = TAG_FAVORITES, // 收藏 Tag
                    SourceType = MusicSourceType.Stream,
                    SourcePath = song.Id.ToString(),
                    Duration = (float)song.Duration,
                    ModuleId = _moduleId,
                    IsUnlocked = true,
                    IsFavorite = isLiked,
                    ExtendedData = song
                };

                musicList.Add(musicInfo);
                _songInfoMap[uuid] = song;
                _context.MusicRegistry.RegisterMusic(musicInfo, _moduleId);
            }

            return musicList;
        }

        /// <summary>
        /// 将歌曲移动到收藏专辑
        /// </summary>
        public void MoveSongToFavorites(string uuid, List<MusicInfo> sourceList, List<MusicInfo> favoritesMusicList)
        {
            // 找到源列表中的歌曲
            var music = sourceList.FirstOrDefault(m => m.UUID == uuid);
            if (music == null) return;

            // 更新为收藏专辑
            music.AlbumId = FAVORITES_ALBUM_ID;
            music.IsFavorite = true;

            // 移动到收藏列表
            sourceList.Remove(music);
            favoritesMusicList.Add(music);

            // 更新注册信息
            _context.MusicRegistry.UpdateMusic(music);
        }

        #region 自定义歌单

        /// <summary>
        /// 生成歌单专属的 Tag ID
        /// </summary>
        public static string GeneratePlaylistTagId(long playlistId)
        {
            return $"netease_playlist_{playlistId}";
        }

        /// <summary>
        /// 生成歌单专属的 Album ID
        /// </summary>
        public static string GeneratePlaylistAlbumId(long playlistId)
        {
            return $"netease_playlist_album_{playlistId}";
        }

        /// <summary>
        /// 注册自定义歌单的 Tag
        /// </summary>
        /// <param name="playlistId">歌单 ID</param>
        /// <param name="displayName">显示名称</param>
        public void RegisterPlaylistTag(long playlistId, string displayName)
        {
            var tagId = GeneratePlaylistTagId(playlistId);
            _context.TagRegistry.RegisterTag(tagId, displayName, _moduleId);
            _logger.LogInfo($"[NeteaseSongRegistry] 已注册 Tag: {displayName} ({tagId})");

            // 将收藏专辑也注册到这个 Tag 下，这样歌曲收藏后可以正确显示在收藏专辑中
            AddFavoritesAlbumToTag(tagId);
        }

        /// <summary>
        /// 将收藏专辑添加到指定 Tag 下
        /// </summary>
        private void AddFavoritesAlbumToTag(string tagId)
        {
            var favoritesAlbum = _context.AlbumRegistry.GetAlbum(FAVORITES_ALBUM_ID);
            if (favoritesAlbum == null)
            {
                _logger.LogWarning($"[NeteaseSongRegistry] 收藏专辑未找到，无法添加到 Tag: {tagId}");
                return;
            }

            // 检查是否已包含此 Tag
            if (favoritesAlbum.TagIds.Contains(tagId))
                return;

            // 添加新的 TagId
            favoritesAlbum.TagIds.Add(tagId);

            // 重新注册专辑以更新索引
            _context.AlbumRegistry.RegisterAlbum(favoritesAlbum, _moduleId);
            _logger.LogInfo($"[NeteaseSongRegistry] 已将收藏专辑添加到 Tag: {tagId}");
        }

        /// <summary>
        /// 注册自定义歌单专辑
        /// </summary>
        public void RegisterPlaylistAlbum(long playlistId, string name, int songCount, string coverUrl)
        {
            var tagId = GeneratePlaylistTagId(playlistId);
            var albumId = GeneratePlaylistAlbumId(playlistId);

            var album = new AlbumInfo
            {
                AlbumId = albumId,
                DisplayName = name,
                Artist = "网易云音乐",
                TagIds = new List<string> { tagId },
                ModuleId = _moduleId,
                SongCount = songCount,
                SortOrder = 0,
                IsGrowableAlbum = false,
                ExtendedData = $"PLAYLIST:{playlistId}:{coverUrl}"
            };
            _context.AlbumRegistry.RegisterAlbum(album, _moduleId);
            _logger.LogInfo($"[NeteaseSongRegistry] 已注册歌单专辑: {name} ({songCount} 首)");
        }

        /// <summary>
        /// 注册自定义歌单中的歌曲
        /// </summary>
        public List<MusicInfo> RegisterPlaylistSongs(long playlistId, IEnumerable<NeteaseBridge.SongInfo> songs)
        {
            var musicList = new List<MusicInfo>();
            var tagId = GeneratePlaylistTagId(playlistId);
            var albumId = GeneratePlaylistAlbumId(playlistId);

            foreach (var song in songs)
            {
                var uuid = GenerateUUID(song.Id);

                // 如果已经注册过（可能在收藏列表、个人 FM 或其他歌单中）
                if (_songInfoMap.ContainsKey(uuid))
                {
                    // 获取已存在的 MusicInfo 并添加到列表
                    var existingMusic = _context.MusicRegistry.GetMusic(uuid);
                    if (existingMusic != null)
                    {
                        musicList.Add(existingMusic);
                        // 将已存在的歌曲也添加到这个 Tag 的索引中
                        _context.MusicRegistry.AddMusicToTag(uuid, tagId);
                    }
                    continue;
                }

                var isLiked = _favoriteManager.IsSongLiked(song.Id);

                var musicInfo = new MusicInfo
                {
                    UUID = uuid,
                    Title = song.Name,
                    Artist = song.ArtistName,
                    AlbumId = albumId,
                    TagId = tagId,
                    SourceType = MusicSourceType.Stream,
                    SourcePath = song.Id.ToString(),
                    Duration = (float)song.Duration,
                    ModuleId = _moduleId,
                    IsUnlocked = true,
                    IsFavorite = isLiked,
                    ExtendedData = song
                };

                musicList.Add(musicInfo);
                _songInfoMap[uuid] = song;
                _context.MusicRegistry.RegisterMusic(musicInfo, _moduleId);
            }

            return musicList;
        }

        #endregion

        /// <summary>
        /// 生成确定性 UUID
        /// </summary>
        public static string GenerateUUID(long songId)
        {
            return MusicInfo.GenerateUUID($"netease:{songId}");
        }
    }
}
