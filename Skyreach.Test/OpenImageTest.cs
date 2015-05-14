using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Skyreach.Jp2.FileFormat;
using Skyreach.Jp2.Codestream;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Skyreach.Jp2;
using Skyreach.Jp2.Codestream.Markers;


namespace Skyreach.Test
{
    [TestClass]
    [DeploymentItem(@"images\test.jp2")]
    public class OpenImageTest
    {


        private Stream _fs;

        [TestInitialize]
        public void Initialize()
        {
            _fs = File.OpenRead(Image.Default.TestImageName);
            Assert.IsNotNull(_fs);
        }

        [TestCleanup]
        private void Cleanup()
        {
            _fs.Close();
        }

        [TestMethod]
        [TestCategory("Traversal")]
        public void OpenImage()
        {
            Jp2File jp2 = Jp2File.Open(_fs);
            Assert.IsNotNull(jp2);
            JP2Codestream cs = jp2.OpenCodestream();
            Assert.IsNotNull(cs);
            Assert.IsNull(cs.Parent);
            Assert.AreEqual<Size>(cs.ImageSize, Image.Default.ImageSize);
            Assert.AreEqual<Size>(cs.TileSize, Image.Default.ImageSize);
            Assert.AreEqual(cs.TileCount, new Size(1, 1));
            Assert.AreEqual(cs.Components, Image.Default.Components);
            Assert.AreEqual(cs.Progression, Image.Default.Progression);
            Assert.AreEqual(cs.ImageOffset, Point.Empty);
            Assert.AreEqual(cs.TileOffset, Point.Empty);
            Assert.AreEqual(cs.DecompositionLevels, Image.Default.Decompositions);
            Assert.AreEqual(cs.QualityLayers, 1);

            int tileparts = cs.OpenChildren().Count();
            Assert.AreEqual(1, tileparts);

            _fs.Seek(0, SeekOrigin.Begin);
            long socOffset = Find(_fs, (ushort)MarkerType.SOC);
            long sizOffset = Find(_fs, (ushort)MarkerType.SIZ);
            long sotOffset = Find(_fs, (ushort)MarkerType.SOT);
            long sodOffset = Find(_fs, (ushort)MarkerType.SOD);
            long eocOffset = Find(_fs, (ushort)MarkerType.EOC);

            Assert.AreEqual(socOffset, cs.Position);
            Assert.AreEqual(sizOffset, cs.Position + 2);
            Assert.AreEqual(sotOffset, cs.Position + cs.FirstChildOffset);
            long sodOffsetFromCs = cs.Position + cs.FirstChildOffset;
            sodOffsetFromCs += cs.OpenTilePart(0, 0).FirstChildOffset - 2;
            Assert.AreEqual(sodOffset, sodOffsetFromCs);
            Assert.AreEqual(eocOffset, cs.Position + cs.Length - 2);
        }

        public long Find(Stream str, ushort val)
        {
            // start with the first two bytes.
            int byt = str.ReadByte();
            ushort valRead = (ushort)(byt & 0xFF);
            byt = str.ReadByte();
            while(byt != -1)
            {
                valRead <<= 8;
                valRead |= (ushort)byt;
                if(valRead == val)
                {
                    return str.Position - 2;
                }
                byt = str.ReadByte();
            }
            return -1;
        }
    }
}
