using NzbDrone.Core.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromBdmv : IAugmentQuality
    {
        private readonly IBdmvFolderDetector _bdmvFolderDetector;

        public int Order => 5;
        public string Name => "BDMV";

        public AugmentQualityFromBdmv(IBdmvFolderDetector bdmvFolderDetector)
        {
            _bdmvFolderDetector = bdmvFolderDetector;
        }

        public AugmentQualityResult AugmentQuality(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            var path = localMovie.Path;

            if (path == null)
            {
                return null;
            }

            // Check if the file is inside a BDMV structure
            var normalized = path.Replace('\\', '/');

            if (!normalized.Contains("BDMV/STREAM/", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Walk up to the BDMV root and get info
            var dir = System.IO.Path.GetDirectoryName(path);
            while (dir != null)
            {
                var dirName = System.IO.Path.GetFileName(dir);
                if (dirName.Equals("STREAM", System.StringComparison.OrdinalIgnoreCase))
                {
                    var bdmvDir = System.IO.Path.GetDirectoryName(dir);
                    if (bdmvDir != null && System.IO.Path.GetFileName(bdmvDir).Equals("BDMV", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var bdmvRoot = System.IO.Path.GetDirectoryName(bdmvDir);
                        var info = _bdmvFolderDetector.GetBdmvInfo(bdmvRoot);

                        // BR-DISK quality is defined at 1080p regardless of actual disc resolution
                        return new AugmentQualityResult(
                            QualitySource.BLURAY,
                            Confidence.Tag,
                            (int)Resolution.R1080p,
                            Confidence.Tag,
                            Modifier.BRDISK,
                            Confidence.Tag,
                            null,
                            Confidence.Default);
                    }
                }

                dir = System.IO.Path.GetDirectoryName(dir);
            }

            return null;
        }
    }
}
