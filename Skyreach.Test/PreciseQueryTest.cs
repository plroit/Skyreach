using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using Skyreach.Jp2.Codestream;
using Skyreach.Jp2.FileFormat;
using Skyreach.Query;
using Skyreach.Query.Precise;

namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test_LRtCP_L2R3T512.jp2")]
    public class PreciseQueryTest
    {
        private Stream _lrcpStream;
        private JP2Codestream _lrcpCodestream;
        private QueryContext _queryContext;
        private ushort _tileIdx = 2;

        [TestInitialize]
        public void Initialize()
        {
            string testFile = "test_LRtCP_L2R3T512.jp2";
            _lrcpStream = File.OpenRead(testFile);
            _lrcpCodestream = Jp2File.Open(_lrcpStream).OpenCodestream();
            _queryContext = new QueryContext(_lrcpCodestream, _tileIdx);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _lrcpStream.Close();
        }

        [TestMethod]
        [TestCategory("Query")]
        public void EmptyQueryTest()
        {            
            int layers = 2;
            int resolutions = 3;
            int precinctsPerResolution = 1;
            int components = 3;
            int expectedPackets = 1;
            expectedPackets *= layers;
            expectedPackets *= resolutions;
            expectedPackets *= components;
            expectedPackets *= precinctsPerResolution;
            int actualPackets = _lrcpCodestream.Tiles[_tileIdx].GetPacketCounts().Sum();
            Assert.AreEqual(expectedPackets, actualPackets);
            PreciseQuery q = PreciseQuery.Create(_queryContext);
            PacketInterval[] intervals = q.Execute().ToArray();
            Assert.AreEqual(intervals.Length, 1);
            Assert.AreEqual(intervals[0].TileIndex, _tileIdx);
            Assert.AreEqual(intervals[0].PacketStart, 0);
            Assert.AreEqual(intervals[0].Count, actualPackets);
            Assert.AreEqual(intervals[0].PacketEnd, actualPackets);
        }

        [TestMethod]
        [TestCategory("Query")]
        public void QueryLayerLrcpTest()
        {
            int resolutions = 3;
            int precinctsPerResolution = 1;
            int components = 3;
            int packetsInLayer = 1;
            packetsInLayer *= resolutions;
            packetsInLayer *= precinctsPerResolution;
            packetsInLayer *= components;
            PreciseQuery q = PreciseQuery.Create(_queryContext);
            PacketInterval[] intervals = q.Quality(1).Execute().ToArray();
            Assert.AreEqual(intervals.Length, 1);
            Assert.AreEqual(intervals[0].PacketStart, packetsInLayer);
            Assert.AreEqual(intervals[0].PacketEnd, 2*packetsInLayer);
            Assert.AreEqual(intervals[0].Count, packetsInLayer);
        }

        [TestMethod]
        [TestCategory("Query")]
        public void QueryLayerResolutionLrcpTest()
        {
            int layerIdx = 1;
            int resIdx = 1;

            int precinctsInResolution = 1;
            int components = 3;
            int resolutions = 3;
            int packetsInLayerResolution = precinctsInResolution * components;
            int packetsInLayer = packetsInLayerResolution * resolutions;

            int expectedStart = layerIdx * packetsInLayer;
            expectedStart += resIdx * packetsInLayerResolution;
            int expectedEnd = expectedStart + packetsInLayerResolution;
            PreciseQuery q = PreciseQuery.Create(_queryContext);
            PacketInterval[] intervals = q
                .Quality(layerIdx)
                .Resolution(resIdx)
                .Execute()
                .ToArray();

            Assert.AreEqual(intervals.Length, 1);
            Assert.AreEqual(intervals[0].PacketStart, expectedStart);
            Assert.AreEqual(intervals[0].PacketEnd, expectedEnd);
            Assert.AreEqual(intervals[0].Count, packetsInLayerResolution);
        }

        [TestMethod]
        [TestCategory("Query")]
        public void QueryResolutionLrcpTest()
        {
            int resIdx = 1;

            int precinctsInResolution = 1;
            int layers = 2;
            int components = 3;
            int resolutions = 3;
            int packetsInLayerResolution = precinctsInResolution * components;
            int packetsInLayer = packetsInLayerResolution * resolutions;

            int expectedStartInLayer = resIdx * packetsInLayerResolution;
            int[] expectedStarts = new int[layers];
            for(int ly = 0; ly < layers; ly++)
            {
                expectedStarts[ly] = ly * packetsInLayer;
                expectedStarts[ly] += expectedStartInLayer;
            }

            PreciseQuery q = PreciseQuery.Create(_queryContext);
            PacketInterval[] intervals = q
                .Resolution(resIdx)
                .Execute()
                .ToArray();

            Assert.AreEqual(intervals.Length, layers);
            for (int ly = 0; ly < layers; ly++ )
            {
                Assert.AreEqual(intervals[ly].PacketStart, expectedStarts[ly]);
                Assert.AreEqual(intervals[ly].Count, packetsInLayerResolution);
            }
        }
    }
}
