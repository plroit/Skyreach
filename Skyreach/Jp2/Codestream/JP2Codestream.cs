using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Skyreach.Jp2.Codestream
{
    /// <summary>
    /// JPEG2000 codestream is the most minimal structural element that can be
    /// decoded into an image using the JPEG2000 compression algorithm. A
    /// JPEG2000 Codestream contains metadata about image elements such as size,
    /// number of color components, bit-depth o original samples, as well as
    /// metadata about the JPEG2000 compression that is required for
    /// decompression: such as progression order precinct partition, tiling,
    /// quantization, DWT transform and more.
    /// 
    /// A JPEG2000 codestream structure is a root element that contains JPEG2000
    /// tile-parts as direct children.
    /// </summary>
    public class JP2Codestream : CodestreamNode
    {
        /// <summary>
        /// JPEG2000 codestream can hold up to 2^16 tiles. Isot field of SOT
        /// marker (tile index in codestream) is limited to 16 bits.
        /// 
        /// Other limitations are on total tile-part count when using tile-part
        /// index (TLM). There can be only 256 instances of a TLM. (Ztlm field)
        /// Each TLM may have a total length of 2^16 bytes, limited by the
        /// marker segment length. Every entry takes up to 6 bytes. Therefore
        /// total tile-part count is limited by: 2^(8+16)/6;
        /// </summary>
        public const ushort MAX_TILES = UInt16.MaxValue;

        public static readonly IEnumerable<MarkerType> MandatoryMarkers =
            new List<MarkerType>{
                MarkerType.SIZ,
                MarkerType.COD,
                MarkerType.QCD };

        /// <summary>
        /// Expected number of tile parts. Used to reserve space for a tile-part
        /// index in the main header (TLM).
        /// 
        /// An entry in a tile-part index has a fixed length. To generate a
        /// tile-part directly without buffering to the UnderlyingIO, a
        /// tile-part offset must be provided. This offset depends on the size
        /// of the optional TLM. A TLM must contain all the tile-parts in the
        /// codestream, which were not generated yet. This number can be either
        /// pre-calculated (if the user employs a non-dynamic tile-part policy),
        /// or can be expected.
        /// </summary>
        private int _expectedTileParts;

        private Lazy<Size> _imageSize;
        /// <summary>
        /// The next tile-part index that has not yet called 'Bind' on his
        /// parent codestream node to get his offset
        /// </summary>
        private int _nextUnboundTilepartIdx;

        /// <summary>
        /// Offset in bytes of the first SOT marker from the beginning of the 
        /// codestream
        /// </summary>
        private long _sotOffset;

        private Lazy<Size> _tileCount;
        private List<JP2TilePart> _tileparts;
        public JP2Codestream(Stream underlyingIO, long offset, long length)
            : base(null, OFFSET_NOT_SET, length)
        {
            ConstructCommon();
            Bind(underlyingIO, offset);
        }

        /// <summary>
        /// Constructor for generating new JPEG2000 Codestream, without an
        /// backing IO. Codestream is created with a collection of functional
        /// and informative markers that supply basic information about the
        /// image and coding parameters.
        /// 
        /// To fill this image with data you should create new tile parts with
        /// the parent CodestreamNode set to this instance.
        /// 
        /// There are 3 mandatory marker segments that must be provided: SIZ,
        /// COD and QCD.
        /// </summary>
        /// <param name="markers"></param>
        /// <param name="expectedTileParts"></param>
        public JP2Codestream(IEnumerable<MarkerSegment> markers,
            int expectedTileParts)
            : base(null)
        {
            _expectedTileParts = expectedTileParts;
            ConstructCommon();

            Markers = markers.ToDictionary(marker => marker.Type);

            var missing = MandatoryMarkers
                .Where(mandatory => !Markers.ContainsKey(mandatory));

            if (missing.Any())
            {
                throw new ArgumentException(
                    "Mandatory marker is missing: " + missing.First());
            }

            Tiles = ConstructTiles();
        }

        public ushort Components
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).Components;
            }
        }

        public byte DecompositionLevels
        {
            get
            {
                return
                    (Markers[MarkerType.COD] as CodMarker).DecompositionLevels;
            }
        }

        public override long FirstChildOffset
        {
            get { return _sotOffset; }
        }

        public Point ImageOffset
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).ImageOffset;
            }
        }

        /// <summary>
        /// Image dimensions on the reference grid relative to the image offset
        /// </summary>
        public Size ImageSize { get { return _imageSize.Value; } }

        public IReadOnlyDictionary<MarkerType, MarkerSegment> Markers
        {
            get;
            protected set;
        }

        public byte[] Precisions
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).Precisions;
            }
        }

        public ProgressionOrder Progression
        {
            get
            {
                return (Markers[MarkerType.COD] as CodMarker).Progression;
            }
        }

        public int QualityLayers
        {
            get
            {
                return (Markers[MarkerType.COD] as CodMarker).QualityLayers;
            }
        }

        public byte[] SubSamplingX
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).SubSamplingX;
            }
        }

        public byte[] SubSamplingY
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).SubSamplingY;
            }
        }

        /// <summary>
        /// Number of tiles in this image
        /// </summary>
        public Size TileCount { get { return _tileCount.Value; } }

        public Point TileOffset
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).TileOffset;
            }
        }

        /// <summary>
        /// Tile collection. The index of each tile in this list corresponds to
        /// its raster-order location in the image.
        /// </summary>
        public IReadOnlyList<JP2Tile> Tiles { get; protected set; }

        public Size TileSize
        {
            get
            {
                return (Markers[MarkerType.SIZ] as SizMarker).TileSize;
            }
        }

        /// <summary>
        /// The underlying IO stream
        /// </summary>
        protected internal override Stream UnderlyingIO { get; protected set; }

        /// <summary>
        /// Adds the specified tile-part to the ordered list of
        /// tile-parts that compose this codestream object.
        /// </summary>
        /// <param name="tilePart"></param>
        public void Add(JP2TilePart tilePart)
        {
            if (tilePart.Parent != this)
            {
                throw new InvalidOperationException(
                    "tile-part parent must be this codestream");
            }
            if (tilePart.TileIndex >= Tiles.Count())
            {
                throw new ArgumentOutOfRangeException(
                    "tile-part tile index is greater than the number of tiles");
            }

            Tiles[tilePart.TileIndex].Add(tilePart);
            _tileparts.Add(tilePart);
        }

        /// <summary>
        /// Binds this codestream instance to an IO stream with the given
        /// absolute offset. Useful when creating new codestreams
        /// </summary>
        /// <param name="io"></param>
        /// <param name="csOffset"></param>
        public void Bind(Stream io, long csOffset)
        {
            if (io == null)
            {
                throw new ArgumentException("io is null or closed");
            }
            if (_offset > OFFSET_NOT_SET)
            {
                throw new InvalidOperationException(
                    "trying to re-bind an already bounded codestream");
            }

            UnderlyingIO = io;
            _offset = csOffset;
        }

        /// <summary>
        /// Closes this codestream and transfers it to the constructed state.
        /// Closes and releases all tile-parts which are associated with this
        /// codestream. GC may collect those references in a later cycle. Useful
        /// due to the sheer amount of packets that may be associated with each tile-part.
        /// 
        /// Must call again to Jp2File.OpenCodestream() to retrieve an opened
        /// codestream instance.
        /// </summary>
        public void Close()
        {
            foreach (var tilepart in _tileparts)
            {
                tilepart.Close();
            }
            _tileparts.Clear();

            _isOpened = false;
        }

        /// <summary>
        /// Writes the headers of the codestream to the underlying IO stream.
        /// Should be used only when writing raw codestream images.
        /// When using full-fledged JPEG2000 files with Jp2File.Create
        /// use Jp2File.FlushHeaders instead.
        /// </summary>
        public override void FlushHeaders()
        {
            if (_offset == OFFSET_NOT_SET)
            {
                throw new InvalidOperationException(
                    "Should bind CodestreamNode to a stream");
            }
            // Writes the main headers of this codestream The headers include
            // SOC and every other marker in the Markers collection except TLM.
            // TLM is written when the codestream is sealed, after all of the
            // tileparts are generated. To write the TLM and EOC marker invoke 
            // WriteFinalizers().
            UnderlyingIO.Seek(Position, SeekOrigin.Begin);
            int offset = 0;
            offset += MarkerSegment.WriteMarker(MarkerType.SOC, UnderlyingIO);
            offset += Markers[MarkerType.SIZ].WriteMarkerSegment(UnderlyingIO);
            offset += Markers[MarkerType.COD].WriteMarkerSegment(UnderlyingIO);
            offset += Markers[MarkerType.QCD].WriteMarkerSegment(UnderlyingIO);
            var optionalMarkers = Markers.Keys.Except(MandatoryMarkers);
            foreach (var opt in optionalMarkers)
            {
                offset += Markers[opt].WriteMarkerSegment(UnderlyingIO);
            }

            // Reserve space for TLM. Write tile-part index after codestream
            // generation has ended. Tile parts may be produced incrementally,
            // and their lengths remain unknown till WriteFinalizers is called.
            if (_expectedTileParts >= 0)
            {
                int tileparts = Math.Max(_expectedTileParts, _tileparts.Count());
                offset += GetTotalTlmLength(tileparts);
            }

            _sotOffset = offset;
        }

        public override IEnumerable<CodestreamElement> OpenChildren()
        {
            int tpCount = _tileparts.Count();
            for (int tp = 0; tp < tpCount; tp++)
            {
                yield return (_tileparts[tp].Open());
            }
            yield break;
        }

        /// <summary>
        /// Opens a tile-part with the specified indices.
        /// Reads and parses all associated headers and 
        /// fills information about the child packets of this 
        /// tile-part.
        /// </summary>
        /// <param name="tileIdx">Tile index in raster order</param>
        /// <param name="tilepartIdx">The in tile index of the tile-part</param>
        /// <returns></returns>
        public JP2TilePart OpenTilePart(int tileIdx, int tilepartIdx)
        {
            if (tileIdx > Tiles.Count())
            {
                throw new IndexOutOfRangeException("tile index: " + tileIdx);
            }
            return Tiles[tileIdx].OpenTilePart(tilepartIdx);
        }

        /// <summary>
        /// Writes the final marker EOC of this codestream, as well as a TLM
        /// marker segment. A space for a TLM segment has been reserved upon
        /// creation of this codestream. After sealing, all of the tilepart
        /// lengths and their general amount are known and can be safely written
        /// to the underlying IO.
        /// </summary>
        public void WriteFinalizers()
        {
            if (UnderlyingIO == null)
            {
                throw new Exception(
                    @"attempt to write stream headers without supplying
                     an underlying IO stream using WriteHeaders");
            }

            // calculate space for tile parts. packets and SOT
            long tpLength = _tileparts.Sum(tp => tp.Length);
            if (_expectedTileParts > _tileparts.Count())
            {
                throw new ArgumentException(
                    @"Expected more tile-parts than actual amount
                     does not support generating empty tile parts
                     to fill reserved space in TLM");
            }

            //int batch = TlmMarker.MaxEntries;
            int tpCount = _tileparts.Count;
            int tlmLength = GetTotalTlmLength(tpCount);
            int tlmCount = BitHacks.DivCeil(tpCount, TlmMarker.MaxEntries);
            long tlmOffset = _sotOffset - tlmLength;
            UnderlyingIO.Seek(Position + tlmOffset, SeekOrigin.Begin);
            for(byte tlmIdx = 0; tlmIdx < tlmCount; tlmIdx++)
            {
                var tpInTlm = _tileparts
                    .Skip(tlmIdx*TlmMarker.MaxEntries)
                    .Take(TlmMarker.MaxEntries);
                var tlm = new TlmMarker(tlmIdx, tpInTlm);
                tlm.WriteMarkerSegment(UnderlyingIO);
            }

            long eocOffset = _sotOffset + tpLength;
            UnderlyingIO.Seek(Position + eocOffset, SeekOrigin.Begin);
            MarkerSegment.WriteMarker(MarkerType.EOC, UnderlyingIO);
            // account for all headers, all tile-parts and EOC marker
            Length = _sotOffset + tpLength + MarkerSegment.MarkerLength;
        }
        internal long AssignOffset(JP2TilePart tp)
        {
            bool rc = _nextUnboundTilepartIdx < _tileparts.Count;
            rc = rc && tp == _tileparts[_nextUnboundTilepartIdx];
            if (!rc)
            {
                throw new InvalidOperationException(String.Concat(
                    "Must add the tile-part to the codestream before assigning",
                    " offsets. Call FlushHeaders in the same order that the ",
                    " tile-parts were inserted into the codestream"));
            }

            long tpOffset = FirstChildOffset;
            if (_nextUnboundTilepartIdx > 0)
            {
                int prevIdx = _nextUnboundTilepartIdx - 1;
                var prev = _tileparts[prevIdx];
                tpOffset = prev.Position - Position;
                tpOffset += prev.Length;
                if (prev.Length <= 0)
                {
                    throw new InvalidOperationException(string.Format(
                        "tilepart at position {0}, was not sealed", prevIdx));
                }
            }
            _nextUnboundTilepartIdx++;
            return tpOffset;
        }

        internal override CodestreamNode Open()
        {
            if (_isOpened)
            {
                return this;
            }

            UnderlyingIO.Seek(Position, SeekOrigin.Begin);

            if (MarkerSegment.Peek(UnderlyingIO) != MarkerType.SOC)
            {
                throw new ArgumentException(
                    "expected SOC marker at stream position: " + Position);
            }

            var markers = new Dictionary<MarkerType, MarkerSegment>();

            bool isZOrderConsistent = true; // true iff every consequent tlm
            // maintains ascending order from zero, without gaps.
            int zIdx = -1; // current z-index of plt markers
            List<TlmMarker> tlmMarkers = new List<TlmMarker>();

            MarkerSegment ms;
            long offset = 0; // offset from the beginning of the codestream
            while (MarkerSegment.Peek(UnderlyingIO) != MarkerType.SOT)
            {
                ms = MarkerSegment.Open(UnderlyingIO);
                switch (ms.Type)
                {
                    case MarkerType.SIZ:
                    case MarkerType.COD:
                    case MarkerType.QCD:
                        if (markers.ContainsKey(ms.Type))
                        {
                            throw new InvalidDataException(
                                "Already have a " + ms.Type + "marker");
                        }
                        markers[ms.Type] = ms;
                        break;

                    case MarkerType.TLM:
                        var tlm = ms as TlmMarker;
                        isZOrderConsistent &= (++zIdx) == tlm.ZIndex;
                        tlmMarkers.Add(tlm);
                        break;

                    case MarkerType.COC:
                    case MarkerType.QCC:
                        throw new NotSupportedException(
                            "Codestream and Quantization markers for specific" +
                            " components are not supported in main header");
                    default:
                        break;
                }
                offset += MarkerSegment.MarkerLength + ms.Length;
            }

            _sotOffset = offset;
            Markers = markers;

            if (!isZOrderConsistent)
            {
                // need sorting!
                tlmMarkers.Sort((TlmMarker tlmX, TlmMarker tlmY) =>
                    tlmX.ZIndex.CompareTo(tlmY.ZIndex));
            }

            Tiles = tlmMarkers.Any() ?
                ConstructTilesFromTlm(tlmMarkers) :
                ConstructTilesFromCodestream();

            _isOpened = true;
            return this;
        }

        private void ConstructCommon()
        {
            _tileparts = new List<JP2TilePart>();
            _imageSize = new Lazy<Size>(() =>
            {
                var siz = Markers[MarkerType.SIZ] as SizMarker;
                return new Size(
                    (int)(siz.RefGridSize.Width - siz.ImageOffset.X),
                    (int)(siz.RefGridSize.Height - siz.ImageOffset.Y));
            });
            _tileCount = new Lazy<Size>(() =>
            {
                var siz = Markers[MarkerType.SIZ] as SizMarker;
                return new Size(
                    BitHacks.DivCeil(
                        siz.RefGridSize.Width - siz.TileOffset.X,
                        TileSize.Width),
                    BitHacks.DivCeil(
                        siz.RefGridSize.Height - siz.TileOffset.Y,
                        siz.TileSize.Height));
            });
        }

        private IReadOnlyList<JP2Tile> ConstructTiles()
        {
            // we should have checked tile count somewhere else.. perhaps in the
            // SIZ marker, say?
            ushort tileCount = (ushort)(TileCount.Width * TileCount.Height);
            JP2Tile[] tiles = new JP2Tile[tileCount];
            for (ushort t = 0; t < tileCount; t++)
            {
                tiles[t] = new JP2Tile(this, t);
            }
            return tiles.ToList();
        }

        private IReadOnlyList<JP2Tile> ConstructTilesFromCodestream()
        {
            if (_sotOffset == 0)
            {
                throw new InvalidOperationException(
                    "SOT offset uninitialized, cannot traverse tile-parts " +
                    "before main header was parsed and first tile part" +
                    " was encountered");
            }

            IReadOnlyList<JP2Tile> tiles = ConstructTiles();
            int tileCount = tiles.Count();

            UnderlyingIO.Seek(Position + _sotOffset, SeekOrigin.Begin);

            MarkerType type;
            long offset = _sotOffset;
            while ((type = MarkerSegment.Peek(UnderlyingIO)) != MarkerType.EOC)
            {
                if (type != MarkerType.SOT)
                {
                    throw new InvalidOperationException(
                        "Expected only SOT markers in codestream traversal");
                }

                // get the length of the tile part from the SOT and continue as
                // if we have read a TLM entry.
                SotMarker sot = MarkerSegment.Open(UnderlyingIO) as SotMarker;

                if (sot.TileIndex >= tileCount)
                {
                    throw new ArgumentOutOfRangeException(
                        "SOT TileIndex is too large for number of tiles" +
                        " calculated from SIZ tile and image size");
                }

                tiles[sot.TileIndex].Add(new JP2TilePart(
                    this,
                    offset,
                    sot.TilePartLength));
                long skip = sot.TilePartLength - SotMarker.SOT_MARKER_LENGTH;
                offset += sot.TilePartLength;

                UnderlyingIO.Seek(skip, SeekOrigin.Current);
            }
            return tiles;
        }

        private IReadOnlyList<JP2Tile> ConstructTilesFromTlm(
                    List<TlmMarker> zOrderedTlmMarkers)
        {
            IReadOnlyList<JP2Tile> tiles = ConstructTiles();
            // flatten
            long offset = _sotOffset;
            foreach (var tlm in zOrderedTlmMarkers)
            {
                foreach (TlmMarker.TlmEntry entry in tlm)
                {
                    JP2Tile tile = tiles[entry.TileIndex];
                    tile.Add(new JP2TilePart(
                        this,
                        offset,
                        entry.TilePartLength));
                    offset += entry.TilePartLength;
                }
            }
            return tiles;
        }

        /// <summary>
        /// Calculates the length in bytes of all the TLM markers
        /// that are required to 
        /// </summary>
        /// <param name="tileparts"></param>
        /// <returns></returns>
        private static int GetTotalTlmLength(int tileparts)
        {
            int tlmCount = BitHacks.DivCeil(tileparts, TlmMarker.MaxEntries);
            if(tlmCount > TlmMarker.MaxMarkers)
            {
                throw new InvalidOperationException(
                    String.Concat(string.Format(
                    "too many tile-parts {0}, cannot create more ",
                     "than {1} TLM markers, each marker containing up to ",
                     "{2} entries",
                     tileparts, TlmMarker.MaxMarkers, TlmMarker.MaxEntries)));
            }
            int tilepartsRemainder = 
                tileparts - ((tlmCount - 1) * TlmMarker.MaxEntries);
            // calculate length for all the full TLM markers
            int tlmLength =
                (tlmCount - 1) * TlmMarker.LengthOf(TlmMarker.MaxEntries);
            // add the length of all TLM markers which are left.
            tlmLength += TlmMarker.LengthOf(tilepartsRemainder);
            // TlmMarker.LengthOf returns the Length of the TLM
            // field as signaled in the marker segment, without 
            // without the 2 bytes of the marker itself.
            tlmLength += tlmCount * MarkerSegment.MarkerLength;
            return tlmLength;
        }
    }
}