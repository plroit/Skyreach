using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class SotMarker : MarkerSegment
    {

        /// <summary>
        /// Fixed length in bytes of a valid SOT marker,
        /// without the marker 2 bytes
        /// </summary>
        public const int SOT_MARKER_LENGTH = 10; 
        
        /// <summary>
        /// Tile index. This number refers to the tiles in
        /// raster order starting at the number 0. 
        /// </summary>
        public ushort TileIndex { get; private set; }

        /// <summary>
        /// Tile-part index. There is a specific order required for 
        /// decoding tile-parts; this index denotes the order from 0.
        /// The tile-parts of this tile shall appear in the codestream 
        /// in this order, although not necessarily consecutively. 
        /// </summary>
        public byte TilePartIndex { get; private set; }

        /// <summary>
        /// Length, in bytes, from the beginning of the first byte of 
        /// this SOT marker segment of the tile-part to the end of the 
        /// data of that tile-part. 
        /// </summary>
        public uint TilePartLength { get; protected internal set; }

        /// <summary>
        /// Number of tile-parts of a tile in the codestream. 
        /// Two values are allowed: the correct number of tile- parts 
        /// for that tile and zero. A zero value indicates that the 
        /// number of tile-parts of this tile is not specified in 
        /// this tile-part.  
        /// </summary>
        public byte TilePartCount { get; protected internal set; }

        

        protected internal SotMarker(ushort length, byte[] markerBody)
            : base(MarkerType.SOT, length, markerBody)
        {
            Parse();
        }

        public SotMarker(ushort tileIndex, byte tilePartIndex)
            : base(MarkerType.SOT)
        {
            TileIndex = tileIndex;
            TilePartIndex = tilePartIndex;
        }

        protected internal override void Parse()
        {
            if(_markerLength != SOT_MARKER_LENGTH)
            {
                throw new ArgumentException(
                    "SOT marker of illegal length: " +_markerLength);
            }

            MemoryStream mem = new MemoryStream(_markerBody);

            TileIndex = mem.ReadUInt16();
  
            TilePartLength = mem.ReadUInt32();

            TilePartIndex = mem.ReadUInt8();

            TilePartCount = mem.ReadUInt8();

        }

        public override byte[] GenerateMarkerBody()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteUInt16(TileIndex);
            mem.WriteUInt32(TilePartLength);
            mem.WriteUInt8(TilePartIndex);
            mem.WriteUInt8(TilePartCount);
            return mem.ToArray();
        }
    }
}
