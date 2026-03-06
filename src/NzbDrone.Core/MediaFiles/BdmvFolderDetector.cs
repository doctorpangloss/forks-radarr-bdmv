using System;
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
            try
            {
                var bdrom = new BDROM(new BDInfo.IO.DirectoryInfo(path));
                bdrom.Scan();

                if (bdrom.PlaylistFiles == null || !bdrom.PlaylistFiles.Any())
                {
                    _logger.Warn("No playlists found in BDMV: {0}", path);
                    return null;
                }

                foreach (var playlist in bdrom.PlaylistFiles.Values)
                {
                    playlist.Scan(bdrom.StreamFiles, bdrom.StreamClipFiles);
                }

                var mainPlaylist = bdrom.PlaylistFiles.Values
                    .Where(p => p.IsValid && !p.HasLoops)
                    .OrderByDescending(p => p.TotalLength)
                    .FirstOrDefault();

                if (mainPlaylist == null)
                {
                    _logger.Warn("No valid playlists found in BDMV: {0}", path);
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

                return new BdmvInfo
                {
                    MainPlaylistPath = mainPlaylist.GetFilePath(),
                    MainFeaturePath = mainFeaturePath,
                    DurationSeconds = mainPlaylist.TotalLength,
                    TotalSize = (long)mainPlaylist.TotalSize,
                    IsUhd = bdrom.IsUHD,
                    Is3D = bdrom.Is3D
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to parse BDMV structure: {0}", path);
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

            // Fallback to largest m2ts if BDInfo parsing fails
            return GetMainFeatureBySize(path);
        }

        public long GetTotalSize(string path)
        {
            var info = GetBdmvInfo(path);
            if (info != null)
            {
                return info.TotalSize;
            }

            // Fallback to summing m2ts files
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
