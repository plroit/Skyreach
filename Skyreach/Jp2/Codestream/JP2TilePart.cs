using Skyreach.Jp2.Codestream.Markers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Skyreach.Jp2.Codestream
{
    /// <summary>
    /// A tile-part represents a key container in the JPEG2000 codestream. A
    /// tile-part must contain a consecutive stream of packets that belong to
    /// the same tile in the source image. The number of packets in each
    /// tile-part is dynamic. The packets of each tile-part are ordered by a
    /// progression order that is signaled in the COD marker of the codestream.
    /// 
    /// Tile-parts of the same tile may not be adjacent in the JPEG2000
    /// codestream. They are however placed in the same order however the
    /// tile-parts are placed in the packet order packets across tile-parts of
    /// the same tile must adhere to the same progression order
    /// </summary>
    public class JP2TilePart : CodestreamNode
    {
        /// <summary>
        /// <para>
        /// The empty tile part consists of a fixed SOT marker segment and an
        /// additional SOD marker.
        /// </para>
        /// <para>
        /// Sometimes Kakadu based encoders emit empty tile parts, bacause they
        /// reserve space for a TLM marker before generating the tileaprts.
        /// After data generation, unused entries in the TLM are used by adding
        /// empty tileparts.
        /// </para>
        /// </summary>
        public const int EMPTY_TILEPART_LENGTH =
            SotMarker.SOT_MARKER_LENGTH + (MarkerSegment.MarkerLength * 2);

        /// <summary>
        /// The maximum number of data bytes that is possible to place in a
        /// tile-part, including all data and headers. The TilePart length field
        /// in the SOT marker is 32 bits long, the sum of all packet lengths
        /// must be less than this number, otherwise an exception is thrown. If
        /// a tile has more data than this threshold, you should divide your
        /// tile to more tile-parts, or consider using smaller tiles.
        /// </summary>
        public const uint MAX_DATA_TILEPART_BYTES =
            UInt32.MaxValue - EMPTY_TILEPART_LENGTH;

        /// <summary>
        /// true iff no more packets can be added to this tilepart
        /// </summary>
        private bool _isSealed;

        /// <summary>
        /// A list that contains packet offsets from the end of the end of the
        /// SOD marker . It contains packets+1 elements, the first element being
        /// the zero offset and the final element is the next packet offset. The
        /// final element can be interpreted as the cumulative length of all
        /// packets that were added so far.
        /// </summary>
        private List<uint> _packetOffsets;

        /// <summary>
        /// The PLT markers that index the packets of this tile part Can be read
        /// from an existing tilepart upon opening pr can be created when
        /// sealing a tilepart
        /// 
        /// PLT is necessary to differentiate between packets without decoding
        /// the headers of each packet. For the meanwhile always create an image
        /// with PLT, deny the user the choice.
        /// </summary>
        private List<PltMarker> _pltMarkers;

        /// <summary>
        /// Offset in bytes from the beginning of the codestream where the first
        /// byte of Start-Of-Data marker is located
        /// </summary>
        private long _sodOffset;

        /// <summary>
        /// The SOT marker that describes this tile-part
        /// </summary>
        private SotMarker _sot;

        /// <summary>
        /// Create tilepart using reference to the parent codestream and indices
        /// of the tile and tilepart. Used when creating new tileparts from scratch
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="tileIdx"></param>
        /// <param name="tpIdx"></param>
        public JP2TilePart(
            JP2Codestream parent,
            ushort tileIdx,
            byte tpIdx,
            bool createPacketIndex)
            : base(parent)
        {
            // packet offsets always has (packets + 1) elements it keeps the
            // next offset at the last element
            _packetOffsets = new List<uint>() { 0 };

            _sot = new SotMarker(tileIdx, tpIdx);
            _pltMarkers = Enumerable.Empty<PltMarker>().ToList();
        }

        protected internal JP2TilePart(JP2Codestream parent, long offset, long length)
            : base(parent, offset, length)
        {
            // packet offsets always has (packets + 1) elements it keeps the
            // next offset at the last element
            _packetOffsets = new List<uint>() { 0 };
        }

        /// <summary>
        /// The offset in bytes of the first packet in the tilepart from the
        /// beginning of the tilepart. This value depends on the correct
        /// generation of PLT markers. Therefore, this value is valid only after
        /// all the packet lengths have been added to the tile part, either by
        /// directly opening the tilepart or by calling Add(packetLength) and WriteHeaders.
        /// </summary>
        public override long FirstChildOffset
        {
            get { return _sodOffset + MarkerSegment.MarkerLength; }
        }

        /// <summary>
        /// True if this is an empty tile-part that contains no coded data.
        /// Tile-part contains only SOT marker and SOD marker, nothing else.
        /// 
        /// Empty tile-parts are used by Kakadu based encoders when generating
        /// reserved space for TLM markers, prior to actual data generation. If
        /// more space for TLM entries is reserved than actual tileparts are
        /// generated, the encoder pads the codestream with empty tileparts and
        /// fills an entry inside the TLM index.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                // first packet offset is 0, for the first packet that is still
                // not there
                return !_packetOffsets.Skip(1).Any();
            }
        }

        /// <summary>
        /// Number of packets that in this tile-part
        /// </summary>
        public int Packets
        {
            get
            {
                return _packetOffsets.Count - 1;
            }
        }

        /// <summary>
        /// TileIndex for this tile part
        /// </summary>
        public ushort TileIndex { get { return _sot.TileIndex; } }

        /// <summary>
        /// The number of tile-parts in this tile. Possibly unknown. Same as
        /// TNsot field
        /// </summary>
        public byte TilePartCount
        {
            get { return _sot.TilePartCount; }
            protected internal set { _sot.TilePartCount = value; }
        }

        /// <summary>
        /// The tile-part index in the tile, ordered by the initial division of
        /// the tiles' packets into groups of tile-parts.
        /// 
        /// Valid range is 0-254
        /// </summary>
        public byte TilePartIndex { get { return _sot.TilePartIndex; } }

        /// <summary>
        /// Length of all packets that belong to this tilepart
        /// </summary>
        public uint TotalPacketLength
        {
            get
            {
                return _packetOffsets[_packetOffsets.Count - 1];
            }
        }

        /// <summary>
        /// Bulk transfer of source tile-part content to destination tile-part.
        /// </summary>
        /// <param name="dst">Destination tile-part</param>
        /// <param name="dstOffset">
        /// Offset in the destination tile-part in bytes from the start of the FirstChildOffset
        /// </param>
        /// <param name="src">Source tile-part</param>
        /// <param name="startPacket">inclusive index</param>
        /// <param name="endPacket">exclusive index</param>
        /// <param name="buffer">optional buffer</param>
        /// <returns>The number of bytes transferred</returns>
        public static uint BulkTransferData(
                    JP2TilePart dst,
                    uint dstOffset,
                    JP2TilePart src,
                    int startPacket,
                    int packetCount,
                    byte[] buffer = null)
        {
            if (buffer == null)
            {
                buffer = new byte[1 << 16];
            }
            int endPacket = startPacket + packetCount;
            uint srcOffset = src._packetOffsets[startPacket];
            uint endOffset = src._packetOffsets[endPacket];
            uint dataCount = endOffset - srcOffset;
            // make it signed integer for bounds checking
            long left = dataCount;
            long srcPos = src.Position + src.FirstChildOffset + srcOffset;
            long dstPos = dst.Position + dst.FirstChildOffset + dstOffset;
            src.UnderlyingIO.Seek(srcPos, SeekOrigin.Begin);
            dst.UnderlyingIO.Seek(dstPos, SeekOrigin.Begin);
            while (left > 0)
            {
                // a tile-part can theoretically have 2^32 bytes but a byte
                // buffer is limited to Int32.MaxValue which is only 2^31 - 1.
                long countL = Math.Min(buffer.Length, left);
                int count = (int)countL;
                src.UnderlyingIO.Read(buffer, 0, count);
                dst.UnderlyingIO.Write(buffer, 0, count);
                left -= count;
            }
            return dataCount;
        }

        /// <summary>
        /// Adds packet records to the internal representation of this
        /// tile-part. Must be used to generate correct tile-part length
        /// information in the SOT marker and packet index in the PLT marker.
        /// 
        /// PLT (Packet Length) marker cannot be generated correctly until all
        /// packet lengths have been added. Once the headers for this tile-part
        /// are written you cannot further add more packets and update the PLT.
        /// </summary>
        /// <param name="packetLengths"></param>
        public void AddPacketLengths(
            IEnumerable<uint> packetLengths)
        {
            ThrowIfSealed();

            if (!packetLengths.Any())
            {
                return;
            }
            // Does not buffer packets internally, only their offsets. There can
            // be many packets in a tile-part and many tile-parts in an image.
            // The tile-part stores only packet offsets from the start of the
            // first packet. By encoding the offsets we can use a fast and
            // efficient random access to two properties of any packet:
            // * The starting byte of any packet.
            // * The length in bytes of any packet.

            // ex. lengths = 20,35,14 ==> 3 packets to add.
            // offsets: 0, 30, 41 ==> 2 existing packets with lengths: 30, 11
            // bytes each. new offsets: 0, 30, 41, 61, 96, 110. last offset is
            // the total length in bytes of all the packets that are currently
            // placed in the tile-part.
            uint nextOffset = TotalPacketLength;
            foreach (var length in packetLengths)
            {
                nextOffset += length;
                ThrowIfDataExceedsThreshold(nextOffset);
                _packetOffsets.Add(nextOffset);
            }
        }

        /// <summary>
        /// Release resources that are associated with this tile-part. The
        /// tile-part will transition from the opened to the constructed state.
        /// You will need to call codestream.OpenTilePart() again if you wish to
        /// re-use this tile-part.
        /// </summary>
        public void Close()
        {
            _isOpened = false;
            _packetOffsets.Clear();
            // The next offset after Clear() is the first offset.
            _packetOffsets.Add(0); 
            _pltMarkers.Clear();
        }

        /// <summary>
        /// Writes tilepart header markers to the underlying stream at the
        /// given offset from its parent codestream. The headers written are
        /// SOT and PLT (packet index). PLT is not written if tilepart is
        /// empty. After using this method, you cannot add more packets.
        /// </summary>
        public override void FlushHeaders()
        {
            if (UnderlyingIO == null)
            {
                throw new InvalidOperationException(
                    @"UnderlyingI stream missing. Tilepart headers should be
                    written after codestream headers");
            }

            if (!_isSealed)
            {
                Seal();
            }

            if (_offset == OFFSET_NOT_SET)
            {
                _offset = (Parent as JP2Codestream).AssignOffset(this);
            }

            // SOT marker includes the full tile-part length we should write the
            // SOT marker after all the packets have been added, and we know in
            // advance what is the full tile-part length (markers and packets).

            UnderlyingIO.Seek(Position, SeekOrigin.Begin);
            _sot.WriteMarkerSegment(UnderlyingIO);
            foreach (var plt in _pltMarkers)
            {
                plt.WriteMarkerSegment(UnderlyingIO);
            }
            MarkerSegment.WriteMarker(MarkerType.SOD, UnderlyingIO);
        }

        /// <summary>
        /// Retrieves the packet lengths of the specified interval of packets.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<uint> GetPacketLengths(int start, int count)
        {
            ThrowIfNotOpened();
            int end = start + count;
            if (Packets < end)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(String.Concat(
                    "Tile: {0}, TilePart: {1}: Number of packets: {2} ",
                    " is less than specified limit: {3}"),
                    TileIndex, TilePartIndex, Packets, end));
            }

            // packet P length is the difference between the offsets of packet
            // P+1 and packet P. Implementation note: Skip in Linq is
            // implemented as iterating through the elements of the enumeration
            // till the first item. There is NO optimization for array or list
            // based enumeration, where random access is much faster
            for (int i = start; i < end; i++)
            {
                yield return _packetOffsets[i + 1] - _packetOffsets[i];
            }
        }

        public override IEnumerable<CodestreamElement> OpenChildren()
        {
            // step to first byte of SOD, and skip over the SOD marker
            long firstPacketOffset = _sodOffset + MarkerSegment.MarkerLength;
            // last packet is the total packets length (the next offset)
            int packetCount = _packetOffsets.Count() - 1;
            for (int cur = 0, nxt = 1; cur < packetCount; cur++, nxt++)
            {
                long pckLength = _packetOffsets[nxt] - _packetOffsets[cur];
                yield return new JP2Packet(
                    this, firstPacketOffset + _packetOffsets[cur], pckLength);
            }
            yield break;
        }

        internal override CodestreamNode Open()
        {
            if (_isOpened)
            {
                return this;
            }

            UnderlyingIO.Seek(Position, SeekOrigin.Begin);
            MarkerType marker = MarkerSegment.Peek(UnderlyingIO);
            if (marker != MarkerType.SOT)
            {
                throw new ArgumentException(
                    "SOT marker expected but was not found");
            }
            List<PltMarker> pltMarkers = new List<PltMarker>();
            int zIdx = -1; // current z-idx of plt markers
            bool isZOrderConsistent = true; // true iff every consequent plt
            // maintains ascending order from zero, without gaps.

            long offset = 0;
            while (MarkerSegment.Peek(UnderlyingIO) != MarkerType.SOD)
            {
                MarkerSegment ms = MarkerSegment.Open(UnderlyingIO);
                switch (ms.Type)
                {
                    case MarkerType.SOT:
                        _sot = ms as SotMarker;
                        break;

                    case MarkerType.PLT:
                        var plt = ms as PltMarker;
                        isZOrderConsistent &= (++zIdx) == plt.ZIndex;
                        pltMarkers.Add(plt);
                        break;

                    case MarkerType.COD:
                    case MarkerType.COC:
                    case MarkerType.QCD:
                    case MarkerType.QCC:
                        throw new NotSupportedException(
                            "Codestream and quantization markers are " +
                            " not supported in tile part header");
                    default:
                        break;
                }
                offset += MarkerSegment.MarkerLength + ms.Length;
            }

            _sodOffset = offset;
            if (!isZOrderConsistent)
            {
                // needs sorting!
                pltMarkers.Sort((PltMarker pltX, PltMarker pltY) =>
                    pltX.ZIndex.CompareTo(pltY.ZIndex));
            }

            if (!pltMarkers.Any() && Length > EMPTY_TILEPART_LENGTH)
            {
                throw new NotSupportedException(
                    "packet lengths are not specified in a non empty tile-part"
                     + " decoding packet headers is not supported yet");
            }

            _pltMarkers = pltMarkers;

            // flatten
            uint packOffset = 0;
            foreach (var plt in pltMarkers)
            {
                foreach (var packLength in plt)
                {
                    packOffset += packLength;
                    _packetOffsets.Add(packOffset);
                }
            }
            _isOpened = true;
            return this;
        }

        private List<PltMarker> BuildPltFromOffsets()
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException(
                    "Cannot build a PLT marker for an empty tilepart");
            }

            int packets = _packetOffsets.Count() - 1;
            if (packets > MarkerSegment.MAX_BODY_BYTES)
            {
                // what we should really do is divide PLT generation between
                // multiple markers if its encoded length exceeds 2^16 the max
                // length for a marker segment,

                // there is at least one byte per packet length entry.
                throw new InvalidOperationException(
                    "too many packets for a single PLt marker");
            }

            List<uint> packetLengths = new List<uint>(packets);
            for (int cur = 0, nxt = 1; cur < packets; cur++, nxt++)
            {
                uint currLength = _packetOffsets[nxt] - _packetOffsets[cur];
                packetLengths.Add(currLength);
            }

            List<PltMarker> plts = new List<PltMarker>();
            byte pltIndex = 0;
            int encodedPacketsCount = 0;
            while (encodedPacketsCount < packets)
            {
                PltMarker plt = new PltMarker(
                    packetLengths,
                    pltIndex,
                    encodedPacketsCount);
                encodedPacketsCount += plt.PacketCount;
                pltIndex++;
                plts.Add(plt);
            }
            return plts;
        }

        /// <summary>
        /// Seal this tilepart to additional packets. All of the tileparts'
        /// packets have been added. It is now safe to generate a packet index
        /// and compute the full length of this tilepart, that is comprised of:
        /// marker segments, packet index and packet content.
        /// </summary>
        private void Seal()
        {
            // calculate Length
            _sodOffset = MarkerSegment.MarkerLength; // SOT marker
            _sodOffset += SotMarker.SOT_MARKER_LENGTH; // SOT segment
            if (!IsEmpty)
            {
                _pltMarkers = BuildPltFromOffsets();
                // SOT marker includes the length of the tilepart (and the PLT
                // marker segment within). generate PLT to calculate its length,
                // then update SOT
                foreach (PltMarker plt in _pltMarkers)
                {
                    plt.ForceGeneration();
                }

                // PLT marker type and segment
                _sodOffset += _pltMarkers
                    .Sum(plt => plt.Length + MarkerSegment.MarkerLength);
            }

            Length = _sodOffset + MarkerSegment.MarkerLength; // SOD marker
            Length += TotalPacketLength; // and packets

            // cannot call WriteHeaders() until the tilepart length field is determined
            _sot.TilePartLength = (uint)Length;

            _isSealed = true;
        }

        private void ThrowIfDataExceedsThreshold(long packetLength)
        {
            uint diff = MAX_DATA_TILEPART_BYTES - TotalPacketLength;
            if (packetLength > diff)
            {
                throw new ArgumentException
                (@"total tile part length exceeds 2^32 bytes.
                 SOT marker only permits 32 bits in length field, must divide
                 packets between more tile-parts");
            }
        }

        private void ThrowIfNotOpened()
        {
            if (!_isOpened)
            {
                throw new InvalidOperationException(String.Concat(
                    "Must open the tile-part before accessing ",
                    "its packets properties"));
            }
        }

        private void ThrowIfSealed()
        {
            if (_isSealed)
            {
                throw new InvalidOperationException(
                    "Tilepart is sealed, cannot add new packets");
            }
        }
    }
}