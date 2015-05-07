using Skyreach.Util.Streams;
using Skyreach.Jp2.Codestream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class TlmMarker : MarkerSegment, IEnumerable<TlmMarker.TlmEntry>
    {
        /// <summary>
        /// Each TLM entry may use up to 6 bytes, 
        /// the actual size is signaled in the Stlm field.
        /// </summary>
        public const int TlmEntryMaxLength = 6;

        /// <summary>
        /// The maximal number of TLM entries in a single TLM marker.
        /// </summary>
        public const int MaxEntries =
            (MarkerSegment.MAX_BODY_BYTES - 2) / TlmEntryMaxLength;

        /// <summary>
        /// The maximal number of TLM markers that may appear 
        /// in a codestream, limited by zTLM field.
        /// </summary>
        public const int MaxMarkers = byte.MaxValue; 

        private readonly List<TlmEntry> _tilePartLengths;

        public byte ZIndex { get; private set; }

        protected internal TlmMarker(ushort markerLength, byte[] markerBody)
            : base(MarkerType.TLM, markerLength, markerBody)
        {
            _tilePartLengths = new List<TlmEntry>();
            Parse();
        }

        public TlmMarker(byte zTlm, IEnumerable<JP2TilePart> tileparts) : 
            base(MarkerType.TLM)
        {
            ZIndex = zTlm;
            _tilePartLengths = new List<TlmEntry>(tileparts.Count());
            _tilePartLengths = tileparts
                .Select(tp => new TlmEntry(tp.TileIndex, (uint) tp.Length))
                .ToList();
        }

        protected internal override void Parse()
        {
            if(_markerLength < 4)
            {
                throw new ArgumentOutOfRangeException(
                    "TLM is too short");
            }
            var mem = new MemoryStream(_markerBody);

            byte zTlm = mem.ReadUInt8();
            byte sTlm = mem.ReadUInt8();
            // the size in bytes, of each component in a TLM entry:
            // the tile index and the tile-part length
            // Bits 5,6 (starting from LSB) determine sizTileIdx
            // Bit 7 determines sizTilePartLen
            int sizTileIdx = (sTlm >> 4) & (0x2 | 0x1);
            int sizTilePartLen = (sTlm >> 6) & 0x1;
            // SP = 0 ==> siz = 16 bits, SP =1 ==> siz = 32 bits
            sizTilePartLen = 1 << (sizTilePartLen + 1);
 
            if (sizTileIdx > 2)
            {
                throw new ArgumentOutOfRangeException(
                    "sTLM - ST is in illegal range");
            }

            int sizTlmEntry = sizTileIdx + sizTilePartLen;
            int rem = (_markerLength - 4) % sizTlmEntry;
            if (rem != 0)
            {
                throw new ArgumentException(
                    "not enough bytes in TLM to describe an integer amount of tile parts");
            }
            int tpCount =  (_markerLength - 4) / sizTlmEntry;
            for(ushort tp = 0; tp < tpCount; tp++)
            {
                ushort tileIdx = 0;
                uint tilePartLength;
                if (sizTileIdx == 0)
                {
                    // one tile-part per tile
                    tileIdx = tp;
                }
                else if (sizTileIdx == 1)
                {
                    tileIdx = mem.ReadUInt8();
                }
                else
                {
                    tileIdx = mem.ReadUInt16();
                }

                if(sizTilePartLen == 2)
                {
                    tilePartLength = mem.ReadUInt16();
                }
                else
                {
                    tilePartLength = mem.ReadUInt32();
                }
                _tilePartLengths.Add(new TlmEntry(tileIdx, tilePartLength));
            }

        }

        public override byte[] GenerateMarkerBody()
        {
            var mem = new MemoryStream();
            mem.WriteUInt8(ZIndex);
            // 2 bytes == 16 bits for tile index
            byte sizTileIndex = (byte) 2; 
            // 4 bytes == 32 bits for tile-part length
            byte sizTilePartLength = 1;
            byte sTlm = (byte)(((sizTilePartLength << 2) | sizTileIndex) << 4);
            mem.WriteUInt8(sTlm);

            foreach(var entry in _tilePartLengths)
            {
                mem.WriteUInt16(entry.TileIndex);
                mem.WriteUInt32(entry.TilePartLength);
            }

            return mem.ToArray();

        }

        public class TlmEntry
        {
            public readonly ushort TileIndex;
            public readonly uint TilePartLength;

            public TlmEntry(ushort tileIndex, uint tilePartLength)
            {
                TileIndex = tileIndex;
                TilePartLength = tilePartLength;
            }
        }

        public IEnumerator<TlmMarker.TlmEntry> GetEnumerator()
        {
            return _tilePartLengths.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Calculates the length of the TLM marker segment
        /// given the number of tile-parts. This length would appear
        /// in the codestream under the length field of the marker segment,
        /// and would not include the marker itself.
        /// </summary>
        /// <param name="tileparts"></param>
        /// <returns></returns>
        public static int LengthOf(int tileparts)
        {
            // fixed length fields 
            int tlmLength = /*Length*/2 + /*Ztlm*/1 + /*Stlm*/1;
            // variable length
            tlmLength += tileparts * TlmEntryMaxLength;
            return tlmLength;
        }
    }
}
