using Skyreach.Jp2.Codestream.Markers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Skyreach.Jp2.Codestream
{
    /// <summary>
    /// <para>
    /// A tile-part represents a key container in the JPEG2000 codestream. A
    /// tile-part must contain a consecutive stream of packets that belong to
    /// the same tile in the source image. The number of packets in each
    /// tile-part is dynamic. The packets of each tile-part are ordered by a
    /// progression order that is signaled in the COD marker of the codestream.
    /// </para>
    /// <para>
    /// Tile-parts can be viewed as containers for the packet sequence
    /// that is generated for a tile. Each container holds a portion
    /// of the consecutive sequence, that has been cut off at 
    /// specific packet boundaries.
    /// </para>
    /// <para>
    /// Tile-parts of the same tile may not be adjacent in the JPEG2000
    /// codestream. However the packet order across multiple tile-parts
    /// of the same tile must adhere to the same progression order as inside
    /// the tile-part. Therefore, tileparts of different tiles can be 
    /// interleaved in the codestream, but the consecutive order of the 
    /// tile-parts of the same tile must remain the same.
    /// </para>
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
        internal JP2TilePart(
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
            _pltMarkers = new List<PltMarker>() { new PltMarker(0) };
        }

        internal JP2TilePart(JP2Codestream parent, long offset, long length)
            : base(parent, offset, length)
        {
            // packet offsets always has (packets + 1) elements it keeps the
            // next offset at the last element
            _packetOffsets = new List<uint>() { 0 };
            _pltMarkers = new List<PltMarker>();
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
        /// TileIndex for this tile part, 
        /// Available when the tile-part is opened
        /// </summary>
        public ushort TileIndex { get { return _sot.TileIndex; } }

        /// <summary>
        /// The number of tile-parts in this tile. Possibly unknown. Same as
        /// TNsot field
        /// Available when the tile-part is opened
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
        /// Available when the tile-part is opened
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
            if(!dst.IsFlushed)
            {
                throw new InvalidOperationException(String.Concat(
                    "trying to transfer data between IO streams ",
                    "without sealing the destination tile-part to ",
                    "further addition of packets. ",
                    "Must call to JP2TilePart.Flush()"));
            }
            if(!dst.IsOpened || !src.IsOpened)
            {
                throw new InvalidOperationException(String.Concat(
                    "Trying to transfer data between unopened ",
                    "tile-parts. Call OpenTilePart on the source ",
                    "or destination codestreams"));
            }
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
            if (IsFlushed)
            {
                throw new InvalidOperationException(String.Concat(
                    "Cannot add packets to a tile-part that ",
                    "has already been flushed to the underlying IO ",
                    "stream. You should call Flush only after you have ",
                    "added all packets"));
            }

            if (!IsOpened)
            {
                throw new InvalidOperationException(String.Concat(
                    "Cannot add packets to a tile-part that has ",
                    "been closed. You can add packets only to a ",
                    "tile-part which has been newly created"));
            }

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

            int done = 0;
            while(done < packetLengths.Count())
            {
                var plt = _pltMarkers.Last();
                if (plt.IsFull)
                {
                    byte newZIdx = (byte)(plt.ZIndex + 1);
                    _pltMarkers.Add(new PltMarker(newZIdx));
                    plt = _pltMarkers.Last();
                }
                done += plt.Ingest(packetLengths.Skip(done));
            }

            UpdateLengthFields();
        }

        /// <summary>
        /// Release resources that are associated with this tile-part. The
        /// tile-part will transition from the opened to the constructed state.
        /// You will need to call codestream.OpenTilePart() again if you wish to
        /// re-use this tile-part.
        /// </summary>
        public void Close()
        {
            if(!IsFlushed)
            {
                throw new InvalidProgramException(String.Concat(
                    "Trying to close a tile-part without ",
                    "flushing it to the underlying IO ",
                    "leads to a loss of data"));
            }

            // must be re-opened to be re-used.
            IsOpened = false;
            _packetOffsets.Clear();
            // The next offset after Clear() is the first offset.
            _packetOffsets.Add(0); 
            _pltMarkers.Clear();
        }

        public override void Flush()
        {
            if (UnderlyingIO == null)
            {
                throw new InvalidOperationException(
                    @"UnderlyingI stream missing. Tilepart headers should be
                    written after codestream headers");
            }

            if(!IsOpened)
            {
                throw new InvalidOperationException(String.Concat(
                    "Trying to flush to IO stream ",
                    "a closed tile-part"));
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
            // can be safely closed and reopened from the underlying stream.
            IsFlushed = true;
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
            if (IsOpened)
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
            // read and parsed successfully.
            IsOpened = true;
            return this;
        }

        /// <summary>
        /// Calculates the length of the PLT markers
        /// and updates the _sodOffset and tile-part length fields
        /// according to the new tile-part contents.
        /// Should be called after each time new packets are added
        /// to the tile-part for consistency
        /// </summary>
        private void UpdateLengthFields()
        {
            _sodOffset = MarkerSegment.MarkerLength; // SOT marker
            _sodOffset += SotMarker.SOT_MARKER_LENGTH; // SOT segment
            // PLT marker type and segment
            _sodOffset += _pltMarkers
                .Sum(plt => plt.Length + MarkerSegment.MarkerLength);
            Length = _sodOffset + MarkerSegment.MarkerLength; // SOD marker
            Length += TotalPacketLength; // and packets
            _sot.TilePartLength = (uint)Length;
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
            if (!IsOpened)
            {
                throw new InvalidOperationException(String.Concat(
                    "Must open the tile-part before accessing ",
                    "its packets properties"));
            }
        }
    }
}