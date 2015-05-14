using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using Skyreach.Jp2.Codestream;
using Skyreach.Query.Precise;
using Skyreach.Jp2.FileFormat;
using System.Collections.Generic;
using System.Drawing;
using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Query;

namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test_RtPCL_L4R5P128T200.jp2")]
    public class PreciseQueryWithPositionsTest
    {

        private Stream _rpclStream;
        private JP2Codestream _rpclCodestream;
        private QueryContext _queryContext;
        private ushort _tileIdx = 1;

        [TestInitialize]
        public void Initialize()
        {
            string testFile = "test_RtPCL_L4R5P128T200.jp2";
            _rpclStream = File.OpenRead(testFile);
            _rpclCodestream = Jp2File.Open(_rpclStream).OpenCodestream();
            _queryContext = new QueryContext(_rpclCodestream, _tileIdx);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _rpclStream.Close();
        }

        [TestMethod]
        [TestCategory("Query")]
        public void QueryResolutionRpclTest()
        {
            int resolutions = _rpclCodestream.DecompositionLevels + 1;
            PreciseQuery q = PreciseQuery.Create(_queryContext);
            IEnumerable<PacketInterval>[] intervals = 
                new IEnumerable<PacketInterval>[resolutions];
            for(int r = 0; r < resolutions; r++)
            {
                intervals[r] = q.Resolution(r).Execute();
            }
            int packetStart = 0;
            int layersMulComps =
                _rpclCodestream.QualityLayers * _rpclCodestream.Components;

            for(int r = 0; r < resolutions; r++)
            {
                int packetsInResolution = layersMulComps;
                // NOTE TO SELF: I can calculate everything
                // programatically, but then I would duplicate
                // the code that I need to test, and may
                // have two places with possible bugs.
                PacketInterval[] ivals = intervals[r].ToArray();
                Assert.AreEqual(ivals.Length, 1);
                PacketInterval ival = ivals[0];
                var cod = _rpclCodestream.Markers[MarkerType.COD] as CodMarker;
                int precinctsInRes = GetPrecinctsInResolution(r, cod);
                packetsInResolution *= precinctsInRes;
                Assert.AreEqual(packetStart, ival.PacketStart);
                Assert.AreEqual(packetsInResolution, ival.Count);
                packetStart += packetsInResolution;
            }
        }

        private int GetPrecinctsInResolution(int r, CodMarker cod)
        {
            Assert.AreEqual(_tileIdx, 1);
            Assert.AreEqual(_rpclCodestream.TileSize, new Size(200, 200));
            Assert.AreEqual(_rpclCodestream.ImageSize, new Size(968, 648));
            // image size (968,648) tile size: (200,200)
            // precinct sizes: (128,128),(128,128),(64,64),(64,64),(64,64)
            int precinctsInResolution = 0;
            Point ppxy = cod.PrecinctPartitions[r];
            switch (r)
            {
                case 0:
                    {
                        // tile TopLeft (13,0) W/H (13,13), 
                        // precincts: (64,64)
                        precinctsInResolution = 1;
                        Assert.AreEqual(ppxy, new Point(6, 6));
                    }
                    break;
                case 1:
                    {
                        Assert.AreEqual(ppxy, new Point(6, 6));
                        // tile TopLeft (25,0) W/H (25,25),
                        // precincts: (64,64)
                        precinctsInResolution = 1;
                    }
                    break;
                case 2:
                    {
                        Assert.AreEqual(ppxy, new Point(6, 6));
                        // tile TopLeft (50,0) W/H (50,50), 
                        // precincts (64,64)
                        // horizontal: 0-64,64-128
                        // vertical: 0-64
                        // total 2 precincts
                        precinctsInResolution = 2;
                    }
                    break;
                case 3:
                    {
                        Assert.AreEqual(ppxy, new Point(7, 7));
                        // tile TopLeft: (100,0), W/H (100,100)
                        // precincts(128,128)
                        // horizontal: 0-128,128-256
                        // vertical: 0-128
                        // total 2 precincts
                        precinctsInResolution = 2;
                    }
                    break;
                case 4:
                    {
                        Assert.AreEqual(ppxy, new Point(7, 7));
                        // tile TopLeft: (200,0), W/H (200,200)
                        // precincts (128,128)
                        // horizontal: 128-256, 256-384, 384-512
                        // vertical: 0-128, 128-256
                        // total 6 precincts
                        precinctsInResolution = 6;
                    }
                    break;
            }
            return precinctsInResolution;
        }
    }
}
