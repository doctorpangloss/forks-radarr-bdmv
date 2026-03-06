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

            return _diskProvider.GetFiles(streamPath, false)
                .Any(f => Path.GetExtension(f).Equals(".m2ts", StringComparison.OrdinalIgnoreCase));
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
                    warnings.Add(string.Format("Corrupt playlist {0}: {1}", playlist.Name, ex.Message));
                    return true;
                };

                bdrom.StreamClipFileScanError += (streamClip, ex) =>
                {
                    warnings.Add(string.Format("Corrupt clip {0}: {1}", streamClip.Name, ex.Message));
                    return true;
                };

                bdrom.StreamFileScanError += (streamFile, ex) =>
                {
                    warnings.Add(string.Format("Corrupt stream {0}: {1}", streamFile.Name, ex.Message));
                    return true;
                };

                bdrom.Scan();

                if (bdrom.PlaylistFiles == null || !bdrom.PlaylistFiles.Any())
                {
                    warnings.Add("No playlists found");
                    _cachedInfo = new BdmvInfo { Warnings = warnings };
                    return _cachedInfo;
                }

                foreach (var playlist in bdrom.PlaylistFiles.Values)
                {
                    playlist.Scan(bdrom.StreamFiles, bdrom.StreamClipFiles);
                }

                var mainPlaylist = bdrom.PlaylistFiles.Values
                    .Where(p => p.IsValid && !p.HasLoops && p.TotalLength >= 120)
                    .OrderByDescending(p => p.TotalLength)
                    .FirstOrDefault();

                if (mainPlaylist == null)
                {
                    warnings.Add(string.Format("{0} playlists found but none qualify as main feature", bdrom.PlaylistFiles.Count));
                    _cachedInfo = new BdmvInfo { Warnings = warnings };
                    return _cachedInfo;
                }

                var mainClip = mainPlaylist.StreamClips
                    .OrderByDescending(c => c.Length)
                    .FirstOrDefault();

                _cachedInfo = new BdmvInfo
                {
                    MainPlaylistPath = mainPlaylist.GetFilePath(),
                    MainFeaturePath = mainClip?.StreamFile?.FileInfo?.FullName,
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
                _logger.Debug(ex, "BDInfo parse failed for '{0}'", path);
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

            return GetMainFeatureBySize(path);
        }

        public long GetTotalSize(string path)
        {
            var info = GetBdmvInfo(path);
            if (info != null && info.TotalSize > 0)
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
