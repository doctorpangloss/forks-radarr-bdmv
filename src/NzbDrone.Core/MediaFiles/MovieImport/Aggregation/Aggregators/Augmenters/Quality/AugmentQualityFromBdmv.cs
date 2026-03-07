using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromBdmv : IAugmentQuality
    {
        private readonly Logger _logger;

        public int Order => 0;
        public string Name => "BDMV";

        public AugmentQualityFromBdmv(Logger logger)
        {
            _logger = logger;
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

            _logger.Info("BDMV augmenter matched path: {0}", path);

            // BR-DISK quality is defined at 1080p regardless of actual disc resolution
            // Use MediaInfo confidence so MediaInfo augmenter can't override with stream resolution
            return new AugmentQualityResult(
                QualitySource.BLURAY,
                Confidence.MediaInfo,
                (int)Resolution.R1080p,
                Confidence.MediaInfo,
                Modifier.BRDISK,
                Confidence.MediaInfo,
                null,
                Confidence.Default);
        }
    }
}
