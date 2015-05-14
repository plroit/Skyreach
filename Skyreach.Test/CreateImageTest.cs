using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skyreach.Jp2.FileFormat;
using System.IO;
using System.Linq;
using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Jp2.Codestream;
using Skyreach.Util.Streams;
using System.Collections.Generic;

namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test.jp2")]
    public class CreateImageTest
    {
        private FileStream _fs;

        private JP2Codestream _csin;

        private MemoryStream _mem;

        [TestInitialize]
        public void Initialize()
        {
            _fs = File.OpenRead("test.jp2");
            _mem = new MemoryStream();
            _csin = Jp2File.Open(_fs).OpenCodestream();
            var siz = _csin.Markers[MarkerType.SIZ] as SizMarker;
            var cod = _csin.Markers[MarkerType.COD] as CodMarker;
            var qcd = _csin.Markers[MarkerType.QCD] as QcdMarker;
            var tpin = _csin.OpenTilePart(0, 0);
            var csout = new JP2Codestream(new List<MarkerSegment>() {
                siz, cod, qcd
            }, 1);
            csout.Bind(_mem, 0);
            var tpout = csout.CreateTilePart(0, true);
            var lengths = tpin.GetPacketLengths(0, tpin.Packets);
            tpout.AddPacketLengths(lengths);
            tpout.Flush();
            csout.Flush();
            _mem.Seek(0, SeekOrigin.Begin);

        }

        [TestCleanup]
        public void Cleanup()
        {
            _mem.Close();
            _fs.Close();
        }

        [TestMethod]
        [TestCategory("Transcode")]
        public void ReadBackPacketLengths()
        {
            // Test if the packet lengths we have
            // copied from the source tile-part, and written
            // into the destination tile-part, can be now
            // read back from the destination stream.
            // The source and destination packet lengths
            // should be equal
            _mem.Seek(0, SeekOrigin.Begin);
            var tpout = Jp2File.Open(_mem)
                .OpenCodestream()
                .OpenTilePart(0, 0);
            var lengthsOut = tpout.GetPacketLengths(
                0, tpout.Packets);
            var tpin = _csin.OpenTilePart(0, 0);
            var lengthsIn = tpin.GetPacketLengths(
                0, tpin.Packets);
            bool seqEq = lengthsOut.SequenceEqual(lengthsIn);
            Assert.IsTrue(seqEq);
        }

        [TestMethod]
        [TestCategory("Transcode")]
        public void CompareMainHeaderMarkers()
        {
            // Test to see if the SIZ, COD and QCD markers
            // for the source image and the destination image
            // are equal.        
            _mem.Seek(0, SeekOrigin.Begin);
            var csout = Jp2File.Open(_mem).OpenCodestream();
            _fs.Seek(_csin.Position, SeekOrigin.Begin);
            _mem.Seek(csout.Position, SeekOrigin.Begin);

            var segmentsIn = ReadSegments(_fs);
            var segmentsOut = ReadSegments(_mem);
            Assert.IsTrue(segmentsIn.ContainsKey((ushort)MarkerType.SIZ));
            Assert.IsTrue(segmentsOut.ContainsKey((ushort)MarkerType.SIZ));
            Assert.IsTrue(segmentsIn.ContainsKey((ushort)MarkerType.COD));
            Assert.IsTrue(segmentsOut.ContainsKey((ushort)MarkerType.COD));
            Assert.IsTrue(segmentsIn.ContainsKey((ushort)MarkerType.QCD));
            Assert.IsTrue(segmentsOut.ContainsKey((ushort)MarkerType.QCD));

            byte[] sizin = segmentsIn[(ushort)MarkerType.SIZ];
            byte[] sizout = segmentsOut[(ushort)MarkerType.SIZ];
            byte[] codin = segmentsIn[(ushort)MarkerType.COD];
            byte[] codout = segmentsOut[(ushort)MarkerType.COD];
            byte[] qcdin = segmentsIn[(ushort)MarkerType.QCD];
            byte[] qcdout = segmentsOut[(ushort)MarkerType.QCD];

            Assert.IsTrue(sizin.SequenceEqual(sizout));
            Assert.IsTrue(codin.SequenceEqual(codout));
            Assert.IsTrue(qcdin.SequenceEqual(qcdout));

        }

        private static Dictionary<ushort, byte[]> ReadSegments(Stream str)
        {
            var rawSegments = new Dictionary<ushort, byte[]>();
            for (ushort mrkType = str.ReadUInt16();
                mrkType != (ushort)MarkerType.SOT;
                mrkType = str.ReadUInt16())
            {
                if (MarkerSegment.IsSegment((MarkerType)mrkType))
                {
                    ushort mrkLength = str.ReadUInt16();
                    byte[] segment = new byte[mrkLength - 2];
                    str.Read(segment, 0, mrkLength - 2);
                    rawSegments[mrkType] = segment;
                }
            }
            return rawSegments;
        }
    }
}
