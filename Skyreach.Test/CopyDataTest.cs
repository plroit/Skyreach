using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using Skyreach.Jp2.FileFormat;
using Skyreach.Jp2.Codestream;
using System.Collections.Generic;
using Skyreach.Jp2.Codestream.Markers;
using System.Collections;

namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test.jp2")]
    public class CopyDataTest
    {
        private FileStream _fs;
        private Jp2File _jp2;
        private MemoryStream _mem;

        [TestInitialize]
        public void Initialize()
        {
            _mem = new MemoryStream();
            _fs = File.OpenRead("test.jp2");
            _jp2 = Jp2File.Open(_fs);

            CopyImage();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _fs.Close();
        }

        [TestMethod]
        public void CopyTilePartTest()
        {
            _mem.Seek(0, SeekOrigin.Begin);
            var tpout = Jp2File
                .Open(_mem)
                .OpenCodestream()
                .OpenTilePart(0,0);
            var tpin = _jp2.OpenCodestream().OpenTilePart(0, 0);

            Assert.AreEqual(tpin.FirstChildOffset, tpout.FirstChildOffset);
            Assert.AreEqual(tpin.TotalPacketLength, tpout.TotalPacketLength);
            // lets check if SOT and PLT markers are the same

            _mem.Seek(tpout.Position, SeekOrigin.Begin);
            _fs.Seek(tpin.Position, SeekOrigin.Begin);

            int totalCount = (int) tpin.FirstChildOffset;
            // compare SOT and PLT markers
            CompareStreams(totalCount);
            // compare packet content
            _mem.Seek(tpout.Position + tpout.FirstChildOffset,
                SeekOrigin.Begin);
            _fs.Seek(tpin.Position + tpin.FirstChildOffset,
                SeekOrigin.Begin);
            CompareStreams((int)tpin.TotalPacketLength);
            
        }

        private void CompareStreams(int totalCount)
        {
            int sz = 1 << 16;
            byte[] bufin = new byte[sz];
            byte[] bufout = new byte[sz];
            int left = totalCount;
            while (left > 0)
            {
                int count = Math.Min(left, sz);
                _fs.Read(bufin, 0, count);
                _mem.Read(bufout, 0, count);
                left -= count;
                // traverse it byte by byte, test file 
                // is ~500KB long, on a 1GHZ CPU this should
                // take ~0.5 millisec. 
                // Don't optimize using unsafe byte[] to uint64
                // conversions or P/Invoking memcmp.
                bool hasDiff = false;
                int b = 0;
                for (b = 0; !hasDiff && b < count; b++)
                {
                    hasDiff = bufin[b] != bufout[b];
                }
                long pos = _fs.Position - left + b;
                Assert.IsFalse(hasDiff, string.Format("fs pos: {0}", pos));
            }
        }

        private void CopyImage()
        {
            var cs = _jp2.OpenCodestream();
            int tileparts = cs.OpenChildren().Count();
            var markers = cs.Markers.Values
                .Where(ms => ms.Type != MarkerType.TLM);
            var csout = new JP2Codestream(markers, tileparts);
            csout.Bind(_mem, 0);
            JP2TilePart tpin = cs.OpenTilePart(0, 0);
            JP2TilePart tpout = csout.CreateTilePart(0, true);
            tpout.AddPacketLengths(
                tpin.GetPacketLengths(0, tpin.Packets));
            tpout.Flush();
            uint dataCount =
                JP2TilePart.BulkTransferData(tpout, 0, tpin, 0, tpin.Packets);
            csout.Flush();
        }
    }
}
