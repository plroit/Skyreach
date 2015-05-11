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
    public class TlmMarker : MarkerSegment, IEnumerable<TlmEntry>
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

        private readonly List<JP2TilePart> _tileparts;

        public int SizeTileIdx { get; private set; }

        public int SizeTilePartLength { get; private set; }

        public bool IsFull { get { return _tileparts.Count >= MaxEntries; } }

        /// <summary>
        /// The maximal number of TLM markers that may appear 
        /// in a codestream, limited by zTLM field.
        /// </summary>
        public const int MaxMarkers = byte.MaxValue; 

        public byte ZIndex { get; private set; }

        protected internal TlmMarker(ushort markerLength, byte[] markerBody)
            : base(MarkerType.TLM, markerLength, markerBody)
        {
            _tileparts = null;
            Parse();
        }

        public TlmMarker(byte zTlm) : 
            base(MarkerType.TLM)
        {
            ZIndex = zTlm;
            _tileparts = new List<JP2TilePart>();
        }

        protected override void Parse()
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
            SizeTilePartLength = sizTilePartLen;
            SizeTileIdx = sizTileIdx;
        }

        public void Add(JP2TilePart tp)
        {
            if(_tileparts.Count >= MaxEntries)
            {
                throw new InvalidOperationException(String.Concat(
                    "Exceeded limit for this TLM marker, ",
                    "must create a new TLM"));
            }
            _tileparts.Add(tp);
            _isDirty = true;
        }

        protected override byte[] GenerateMarkerBody()
        {
            var mem = new MemoryStream();
            mem.WriteUInt8(ZIndex);
            // 2 bytes == 16 bits for tile index
            byte sizTileIndex = (byte) 2; 
            // 4 bytes == 32 bits for tile-part length
            byte sizTilePartLength = 1;
            byte sTlm = (byte)(((sizTilePartLength << 2) | sizTileIndex) << 4);
            mem.WriteUInt8(sTlm);

            foreach(var tp in _tileparts)
            {
                mem.WriteUInt16(tp.TileIndex);
                mem.WriteUInt32((uint) tp.Length);
            }

            return mem.ToArray();

        }

        public IEnumerator<TlmEntry> GetEnumerator()
        {
            var mem = new MemoryStream(_markerBody, 2, _markerLength - 4);
            int sizTlmEntry = SizeTileIdx + SizeTilePartLength;
            int tpCount = (_markerLength - 4) / sizTlmEntry;
            for (ushort tp = 0; tp < tpCount; tp++)
            {
                ushort tileIdx = 0;
                uint tilePartLength;
                if (SizeTileIdx == 0)
                {
                    // one tile-part per tile
                    tileIdx = tp;
                }
                else if (SizeTileIdx == 1)
                {
                    tileIdx = mem.ReadUInt8();
                }
                else
                {
                    tileIdx = mem.ReadUInt16();
                }

                if (SizeTilePartLength == 2)
                {
                    tilePartLength = mem.ReadUInt16();
                }
                else
                {
                    tilePartLength = mem.ReadUInt32();
                }
                yield return new TlmEntry(tileIdx, tilePartLength);
            }
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
}
