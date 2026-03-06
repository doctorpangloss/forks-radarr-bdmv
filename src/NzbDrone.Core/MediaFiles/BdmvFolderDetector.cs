using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.MediaFiles
{
    public interface IBdmvFolderDetector
    {
        bool IsBdmvFolder(string path);
        string GetMainFeaturePath(string path);
        long GetTotalSize(string path);
    }

    public class BdmvFolderDetector : IBdmvFolderDetector
    {
        private readonly IDiskProvider _diskProvider;

        public BdmvFolderDetector(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;
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
                .Where(f => Path.GetExtension(f).Equals(".m2ts", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            return m2tsFiles.Any();
        }

        public string GetMainFeaturePath(string path)
        {
            var streamPath = Path.Combine(path, "BDMV", "STREAM");
            var m2tsFiles = _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!m2tsFiles.Any())
            {
                return null;
            }

            return m2tsFiles.OrderByDescending(f => _diskProvider.GetFileSize(f)).First();
        }

        public long GetTotalSize(string path)
        {
            var streamPath = Path.Combine(path, "BDMV", "STREAM");
            return _diskProvider.GetFiles(streamPath, false)
                .Where(f => Path.GetExtension(f).Equals(".m2ts", System.StringComparison.OrdinalIgnoreCase))
                .Sum(f => _diskProvider.GetFileSize(f));
        }
    }
}
