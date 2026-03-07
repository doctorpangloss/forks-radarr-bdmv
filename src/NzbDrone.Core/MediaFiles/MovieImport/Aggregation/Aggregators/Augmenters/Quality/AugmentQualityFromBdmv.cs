using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Quality
{
    public class AugmentQualityFromBdmv : IAugmentQuality
    {
        private readonly Logger _logger;

        // Run before MediaInfo (Order 4) so equal-confidence values aren't overridden
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

            var normalized = path.Replace('\\', '/');

            if (!normalized.Contains("BDMV/STREAM/", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            _logger.Debug("BDMV structure detected: {0}", path);

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
