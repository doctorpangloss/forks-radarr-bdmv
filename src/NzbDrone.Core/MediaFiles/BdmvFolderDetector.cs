using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDInfo;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.MediaFiles
{
    public class BdmvInfo
    {
        public string MainPlaylistPath { get; set; }
        public string MainFeaturePath { get; set; }
        public double DurationSeconds { get; set; }
        public long TotalSize { get; set; }
        public bool IsUhd { get; set; }
        public bool Is3D { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public interface IBdmvFolderDetector
    {
        bool IsBdmvFolder(string path);
        string GetMainFeaturePath(string path);
        long GetTotalSize(string path);
        BdmvInfo GetBdmvInfo(string path);
    }

    public class BdmvFolderDetector : IBdmvFolderDetector
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;
        private string _cachedPath;
        private BdmvInfo _cachedInfo;

        public BdmvFolderDetector(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public bool IsBdmvFolder(string path)
        {
            if (!_diskProvider.FolderExists(path))
            {
                return false;
            }

            var bdmvPath = Path.Combine(path, "BDMV");

            if (!_diskProvider.FolderExists(bdmvPath))
            {
                return false;
            }

            var streamPath = Path.Combine(bdmvPath, "STREAM");

            if (!_diskProvider.FolderExists(streamPath))
            {
                return false;
            }

            var m2tsFiles = _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return m2tsFiles.Any();
        }

        public BdmvInfo GetBdmvInfo(string path)
        {
            if (_cachedPath == path && _cachedInfo != null)
            {
                return _cachedInfo;
            }

            _cachedPath = path;
            _cachedInfo = null;

            try
            {
                var warnings = new List<string>();
                var bdrom = new BDROM(new BDInfo.IO.DirectoryInfo(path));

                bdrom.PlaylistFileScanError += (playlist, ex) =>
                {
                    var msg = string.Format("Playlist '{0}' could not be read: {1}", playlist.Name, ex.Message);
                    _logger.Warn(msg);
                    warnings.Add(msg);
                    return true;
                };

                bdrom.StreamClipFileScanError += (streamClip, ex) =>
                {
                    var msg = string.Format("Stream clip '{0}' could not be read: {1}", streamClip.Name, ex.Message);
                    _logger.Warn(msg);
                    warnings.Add(msg);
                    return true;
                };

                bdrom.StreamFileScanError += (streamFile, ex) =>
                {
                    var msg = string.Format("Stream file '{0}' could not be read: {1}", streamFile.Name, ex.Message);
                    _logger.Warn(msg);
                    warnings.Add(msg);
                    return true;
                };

                bdrom.Scan();

                if (bdrom.PlaylistFiles == null || !bdrom.PlaylistFiles.Any())
                {
                    _logger.Warn("BDMV at '{0}' contains no playlists", path);
                    return null;
                }

                foreach (var playlist in bdrom.PlaylistFiles.Values)
                {
                    playlist.Scan(bdrom.StreamFiles, bdrom.StreamClipFiles);
                }

                var candidates = bdrom.PlaylistFiles.Values
                    .Where(p => p.IsValid && !p.HasLoops)
                    .Where(p => p.TotalLength >= 120)
                    .OrderByDescending(p => p.TotalLength)
                    .ToList();

                var mainPlaylist = candidates.FirstOrDefault();

                if (mainPlaylist == null)
                {
                    var allPlaylists = bdrom.PlaylistFiles.Values.ToList();
                    _logger.Warn("BDMV at '{0}' has {1} playlists but none are valid main features (all are looping, invalid, or under 2 minutes)", path, allPlaylists.Count);
                    return null;
                }

                var mainClip = mainPlaylist.StreamClips
                    .OrderByDescending(c => c.Length)
                    .FirstOrDefault();

                string mainFeaturePath = null;
                if (mainClip?.StreamFile?.FileInfo != null)
                {
                    mainFeaturePath = mainClip.StreamFile.FileInfo.FullName;
                }

                _logger.Debug("BDMV at '{0}': main playlist '{1}' ({2:F0}min), {3} playlists total, {4}",
                    path,
                    mainPlaylist.Name,
                    mainPlaylist.TotalLength / 60.0,
                    candidates.Count,
                    bdrom.IsUHD ? "UHD" : "HD");

                _cachedInfo = new BdmvInfo
                {
                    MainPlaylistPath = mainPlaylist.GetFilePath(),
                    MainFeaturePath = mainFeaturePath,
                    DurationSeconds = mainPlaylist.TotalLength,
                    TotalSize = (long)mainPlaylist.TotalSize,
                    IsUhd = bdrom.IsUHD,
                    Is3D = bdrom.Is3D,
                    Warnings = warnings
                };

                return _cachedInfo;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read BDMV disc structure at '{0}'", path);
                return null;
            }
        }

        public string GetMainFeaturePath(string path)
        {
            var info = GetBdmvInfo(path);
            if (info?.MainFeaturePath != null)
            {
                return info.MainFeaturePath;
            }

            _logger.Debug("Falling back to largest m2ts for BDMV at '{0}'", path);
            return GetMainFeatureBySize(path);
        }

        public long GetTotalSize(string path)
        {
            var info = GetBdmvInfo(path);
            if (info != null)
            {
                return info.TotalSize;
            }

            return GetTotalSizeByFiles(path);
        }

        private string GetMainFeatureBySize(string path)
        {
            var streamPath = Path.Combine(path, "BDMV", "STREAM");
            var m2tsFiles = _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!m2tsFiles.Any())
            {
                return null;
            }

            return m2tsFiles.OrderByDescending(f => _diskProvider.GetFileSize(f)).First();
        }

        private long GetTotalSizeByFiles(string path)
        {
            var streamPath = Path.Combine(path, "BDMV", "STREAM");
            return _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", StringComparison.OrdinalIgnoreCase))
                .Sum(f => _diskProvider.GetFileSize(f));
        }
    }
}
