using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Skyreach.Jp2.FileFormat;
using System.Drawing;
using System.Linq;
using Skyreach.Jp2;
using Skyreach.Jp2.Codestream;
using System.Collections.Generic;

namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test_LRtCP_L2R3T512.jp2")]
    public class StructuralTraversalTest
    {
        private Stream _fs;
        private Jp2File _jp2;
        private JP2Codestream _cs; 

        [TestInitialize]
        public void Initialize()
        {
            _fs = File.OpenRead("test_LRtCP_L2R3T512.jp2");
            _jp2 = Jp2File.Open(_fs);
            _cs = _jp2.OpenCodestream();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _fs.Close();
        }

        [TestMethod]
        public void CountTilePartsTest()
        {
            // in LRCP progression, created a tile-part for
            // each resolution level and quality layer.
            // make sure we have enumerated all the tile-parts.
            Assert.AreEqual<Size>(_cs.TileSize, Image.Default.TileSize);
            Assert.AreEqual<Size>(_cs.ImageSize, Image.Default.ImageSize);
            Assert.AreEqual<Size>(_cs.TileCount, Image.Default.TileCount);

            Assert.AreEqual(_cs.Progression, ProgressionOrder.LRCP);

            Assert.AreEqual(_cs.DecompositionLevels, 2);
            Assert.AreEqual(_cs.QualityLayers, 2);

            int tileparts = _cs.OpenChildren().Count();
            Size tc = Image.Default.TileCount;
            int expected = tc.Width * tc.Height;
            expected *= _cs.QualityLayers;
            expected *= (_cs.DecompositionLevels + 1);
            Assert.AreEqual(tileparts, expected);
        }

        [TestMethod]
        public void CountPacketsInFirstTile()
        {
            var packets = _cs.Tiles[0].GetPacketCounts().ToArray();
            int qLayers = _cs.QualityLayers;
            int rLevels = _cs.DecompositionLevels + 1;
            // LRtCP - tile-parts at resolution groups
            // no precincts. Packet sequence must be:
            var expectedPacketCounts = 
                Enumerable.Repeat<int>(_cs.Components, qLayers * rLevels);
            Assert.IsTrue(packets.SequenceEqual(expectedPacketCounts));
        }

        [TestMethod]
        public void TestEqualTilePartAndPacketLengths()
        {
            // Tile-Part Length property is taken from either
            // the TLM entry or from the SOT marker.
            // TotalPacketLength is known after parsing
            // of the PLT marker.
            var tpReportedLengths = _cs.OpenChildren()
                .Cast<JP2TilePart>()
                .Select(tp => tp.Length);
            var tpPacketAndHeaderLengths = _cs.OpenChildren()
                .Cast<JP2TilePart>()
                .Select(tp => tp.TotalPacketLength + tp.FirstChildOffset);
            bool seqEq = Enumerable.SequenceEqual(
                tpReportedLengths, tpPacketAndHeaderLengths);
            Assert.IsTrue(seqEq);
        }
    }
}
