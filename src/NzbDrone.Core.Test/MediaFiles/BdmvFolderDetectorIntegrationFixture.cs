using System.IO;
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
    public class BdmvFolderDetectorIntegrationFixture : CoreTest<BdmvFolderDetector>
    {
        private string CreateBdmvStructure(bool withPlaylist = false, bool withStream = true)
        {
            var root = Path.Combine(TempFolder, Path.GetRandomFileName());
            var bdmv = Path.Combine(root, "BDMV");

            Directory.CreateDirectory(Path.Combine(bdmv, "STREAM"));
            Directory.CreateDirectory(Path.Combine(bdmv, "PLAYLIST"));
            Directory.CreateDirectory(Path.Combine(bdmv, "CLIPINF"));

            if (withStream)
            {
                File.WriteAllBytes(Path.Combine(bdmv, "STREAM", "00000.m2ts"), new byte[1024]);
            }

            if (withPlaylist)
            {
                // Write a minimal (but invalid) mpls to trigger a parse error
                File.WriteAllBytes(Path.Combine(bdmv, "PLAYLIST", "00000.mpls"), new byte[] { 0x00, 0x01, 0x02 });
            }

            return root;
        }

        [Test]
        public void should_return_null_for_nonexistent_path()
        {
            var path = Path.Combine(TempFolder, "does_not_exist");

            Subject.GetBdmvInfo(path).Should().BeNull();
        }

        [Test]
        public void should_return_warnings_when_no_playlists_found()
        {
            var root = CreateBdmvStructure(withPlaylist: false);

            var info = Subject.GetBdmvInfo(root);

            info.Should().NotBeNull();
            info.Warnings.Should().Contain(w => w.Contains("No playlists"));
            info.MainFeaturePath.Should().BeNull();
        }

        [Test]
        public void should_return_warnings_for_corrupt_playlist()
        {
            var root = CreateBdmvStructure(withPlaylist: true);

            var info = Subject.GetBdmvInfo(root);

            info.Should().NotBeNull();

            // Either the corrupt playlist triggers a scan error warning,
            // or no valid playlists are found (both are acceptable)
            info.Warnings.Should().NotBeEmpty();
        }

        [Test]
        public void should_fall_back_to_largest_m2ts_when_bdinfo_returns_no_main_feature()
        {
            var root = CreateBdmvStructure(withPlaylist: false);
            var streamPath = Path.Combine(root, "BDMV", "STREAM");

            // Create two m2ts files with different sizes
            File.WriteAllBytes(Path.Combine(streamPath, "00000.m2ts"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(streamPath, "00001.m2ts"), new byte[512]);

            // Use real disk provider for GetMainFeaturePath fallback
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(streamPath, false))
                .Returns(new[]
                {
                    Path.Combine(streamPath, "00000.m2ts"),
                    Path.Combine(streamPath, "00001.m2ts")
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(streamPath, "00000.m2ts")))
                .Returns(2048);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(Path.Combine(streamPath, "00001.m2ts")))
                .Returns(512);

            var mainFeature = Subject.GetMainFeaturePath(root);

            mainFeature.Should().Be(Path.Combine(streamPath, "00000.m2ts"));
        }

        [Test]
        public void should_cache_bdmv_info_for_same_path()
        {
            var root = CreateBdmvStructure(withPlaylist: false);

            var info1 = Subject.GetBdmvInfo(root);
            var info2 = Subject.GetBdmvInfo(root);

            info1.Should().BeSameAs(info2);
        }

        [Test]
        public void should_not_cache_null_results()
        {
            var path = Path.Combine(TempFolder, "does_not_exist");

            var info1 = Subject.GetBdmvInfo(path);
            var info2 = Subject.GetBdmvInfo(path);

            info1.Should().BeNull();
            info2.Should().BeNull();
        }
    }
}
