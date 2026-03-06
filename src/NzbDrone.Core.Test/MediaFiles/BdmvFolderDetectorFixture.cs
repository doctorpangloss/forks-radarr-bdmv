using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class BdmvFolderDetectorFixture : CoreTest<BdmvFolderDetector>
    {
        private string _bdmvRoot;
        private string _bdmvPath;
        private string _streamPath;

        [SetUp]
        public void Setup()
        {
            _bdmvRoot = @"C:\Movies\Snowpiercer.2013.KOR.BluRay.1080p.AVC.DTS-HD.MA7.1-CHDBits".AsOsAgnostic();
            _bdmvPath = Path.Combine(_bdmvRoot, "BDMV");
            _streamPath = Path.Combine(_bdmvPath, "STREAM");
        }

        private void GivenValidBdmvStructure()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_streamPath))
                .Returns(true);

            var m2tsFiles = new[]
            {
                Path.Combine(_streamPath, "00000.m2ts"),
                Path.Combine(_streamPath, "00001.m2ts"),
                Path.Combine(_streamPath, "00002.m2ts"),
                Path.Combine(_streamPath, "00003.m2ts"),
                Path.Combine(_streamPath, "00004.m2ts"),
                Path.Combine(_streamPath, "00005.m2ts")
            };

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(_streamPath, false))
                .Returns(m2tsFiles.AsEnumerable());

            // Main feature is 00000.m2ts at 25GB, others are smaller
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00000.m2ts")))
                .Returns(25_000_000_000L);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00001.m2ts")))
                .Returns(3_700_000_000L);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00002.m2ts")))
                .Returns(500_000_000L);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00003.m2ts")))
                .Returns(200_000_000L);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00004.m2ts")))
                .Returns(100_000_000L);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(_streamPath, "00005.m2ts")))
                .Returns(50_000_000L);
        }

        [Test]
        public void should_detect_valid_bdmv_folder()
        {
            GivenValidBdmvStructure();

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeTrue();
        }

        [Test]
        public void should_reject_folder_without_bdmv_directory()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvPath))
                .Returns(false);

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeFalse();
        }

        [Test]
        public void should_reject_folder_without_stream_directory()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_streamPath))
                .Returns(false);

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeFalse();
        }

        [Test]
        public void should_reject_folder_with_empty_stream_directory()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_streamPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(_streamPath, false))
                .Returns(Enumerable.Empty<string>());

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeFalse();
        }

        [Test]
        public void should_reject_nonexistent_path()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(false);

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeFalse();
        }

        [Test]
        public void should_return_largest_m2ts_as_main_feature()
        {
            GivenValidBdmvStructure();

            var mainFeature = Subject.GetMainFeaturePath(_bdmvRoot);

            mainFeature.Should().Be(Path.Combine(_streamPath, "00000.m2ts"));
        }

        [Test]
        public void should_calculate_total_size_of_m2ts_streams()
        {
            GivenValidBdmvStructure();

            var totalSize = Subject.GetTotalSize(_bdmvRoot);

            totalSize.Should().Be(25_000_000_000L + 3_700_000_000L + 500_000_000L + 200_000_000L + 100_000_000L + 50_000_000L);
        }

        [Test]
        public void should_ignore_non_m2ts_files_in_stream_directory()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvRoot))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_bdmvPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_streamPath))
                .Returns(true);

            var files = new[]
            {
                Path.Combine(_streamPath, "readme.txt"),
                Path.Combine(_streamPath, "thumbnail.jpg")
            };

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(_streamPath, false))
                .Returns(files.AsEnumerable());

            Subject.IsBdmvFolder(_bdmvRoot).Should().BeFalse();
        }

        [TestCase(@"C:\Movies\Movie.2013.COMPLETE.BLURAY-GROUP")]
        [TestCase(@"C:\Movies\Movie.2019.SUBBED.COMPLETE.BLURAY-VEXHD")]
        [TestCase(@"C:\Movies\Movie 2010 1080p GBR Blu-ray AVC DTS-HD MA 5.1-PzD")]
        [TestCase(@"C:\Movies\Movie.2013.KOR.BluRay.1080p.AVC.DTS-HD.MA7.1-CHDBits")]
        [TestCase(@"C:\Movies\PIXAR_SHORT_FILMS_COLLECTION_VOLUME_1")]
        public void should_detect_bdmv_for_common_release_folder_names(string folderPath)
        {
            folderPath = folderPath.AsOsAgnostic();
            var bdmv = Path.Combine(folderPath, "BDMV");
            var stream = Path.Combine(bdmv, "STREAM");

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(folderPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(bdmv))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(stream))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(stream, false))
                .Returns(new[] { Path.Combine(stream, "00000.m2ts") }.AsEnumerable());

            Subject.IsBdmvFolder(folderPath).Should().BeTrue();
        }
    }
}
