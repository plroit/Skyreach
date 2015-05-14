using Skyreach.Jp2.Codestream;
using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Jp2.FileFormat;
using Skyreach.Query;
using Skyreach.Query.Precise;
using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace jp2merge
{
    /// <summary>
    /// This program takes separate JPEG2000 images and merges them into one.
    /// Typically these images are non-overlapping, equally sized rectangular
    /// parts of a source image. Each image has been encoded separately, but
    /// with the same encoding parameters, on a map-reduce framework. This
    /// program implements the 'reduce' step that combines together the
    /// rectangular parts.
    /// </summary>
    internal class Program
    {

        public const float JP2MERGE_VERSION = 0.5f;

        public const string AUTHOR = "Paul Roit";

        /// <summary>
        /// File streams of input images.
        /// </summary>
        private readonly FileStream[] _files;

        /// <summary>
        /// The first input codestream, used as a template.
        /// </summary>
        private readonly JP2Codestream _firstCodestream;

        /// <summary>
        /// Number of images in each direction.
        /// </summary>
        private readonly Size _imageCount;

        /// <summary>
        /// The jp2 objects associated with input images.
        /// </summary>
        private readonly Jp2File[] _jp2s;

        /// <summary>
        /// File paths of input images
        /// </summary>
        private readonly string[] _paths;

        private QueryContext[] _queryContext;


        /// <summary>
        /// Output file stream
        /// </summary>
        private FileStream _dest;

        public Program(MergeOptions opts)
        {

            string[] paths = File.ReadAllLines(opts.InputPathsAsTextFile);
            if(!paths.Any())
            {
                Console.Error.Write(opts.GetUsage());
                return;
            }

            string outputPath = paths.First();
            _paths = paths.Skip(1).ToArray();
            if(!string.IsNullOrEmpty(opts.MergedOutputPath))
            {
                _paths = paths.ToArray();
                outputPath = opts.MergedOutputPath;
            }

            _dest = new FileStream(outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                1 << 20 /* 1MB */);

            _imageCount = new Size(opts.TilesX, opts.TilesY);
            if(_imageCount.IsEmpty)
            {
                Console.Error.Write(opts.GetUsage());
                return;
            }
            int inputCount = _imageCount.Width * _imageCount.Height;
            if(inputCount != _paths.Length)
            {
                Console.Error.Write(opts.GetUsage());
                return;
            }

            _files = new FileStream[inputCount];
            _jp2s = new Jp2File[inputCount];

            _firstCodestream = OpenJp2(0).OpenCodestream();
        }

        private static void Main(string[] args)
        {
            MergeOptions opts = new MergeOptions();
            bool isParsed = CommandLine.Parser.Default.ParseArguments(args, opts);
            if(!isParsed)
            {
                return;
            }

            Program executor = new Program(opts);
            executor.Run();
        }

        private static Jp2File Open(
            int idx,
            string[] paths,
            Jp2File[] jp2Files)
        {
            if (jp2Files[idx] == null)
            {
                Stream stream = new FileStream(
                    paths[idx],
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1 << 14);
                jp2Files[idx] = Jp2File.Open(stream);
            }
            return jp2Files[idx];
        }

        private static void WarnIfClipped(JP2Codestream src, CodMarker cod)
        {
            bool tileSizePow2 = true;
            bool isClippedCodeblock = false;
            int diffTileCblk;
            tileSizePow2 &= BitHacks.IsPowerOf2((uint)src.TileSize.Width);
            tileSizePow2 &= BitHacks.IsPowerOf2((uint)src.TileSize.Height);
            diffTileCblk = BitHacks.LogFloor2((uint)src.TileSize.Width);
            diffTileCblk -= cod.CodeblockWidth;
            isClippedCodeblock |= diffTileCblk < cod.DecompositionLevels;
            diffTileCblk = BitHacks.LogFloor2((uint)src.TileSize.Height);
            diffTileCblk -= cod.CodeblockHeight;
            isClippedCodeblock |= diffTileCblk < cod.DecompositionLevels;
            if (isClippedCodeblock && !tileSizePow2)
            {
                var sb = new StringBuilder();
                Console.WriteLine(sb
                    .AppendLine(
                        "Codeblock in one of the subbands is clipped by the ")
                    .AppendLine(
                        "actual subband dimensions, to perform merging well ")
                    .AppendLine(
                        "input origin must be aligned at the same position ")
                    .AppendLine(
                        "as their output coordinates")
                    .ToString());
            }
        }

        private void CopyTile(ushort tIdx, Jp2File dstJp2)
        {
            JP2Codestream src = OpenJp2(tIdx).OpenCodestream();
            JP2Codestream dst = dstJp2.OpenCodestream();
            ThrowIfDifferentInputs(_firstCodestream, src);
            for (int r = 0; r <= src.DecompositionLevels; r++)
            {
                CopyTileResLevel(dst, src, tIdx, r);
            }
        }

        /// <summary>
        /// Expands each packet interval in the source tile
        /// to a list of tilepart packet intervals. 
        /// Performs the supplied function over the tile-part
        /// packet interval, with an optional computational
        /// result.
        /// </summary>
        /// <typeparam name="T">
        /// The optional aggregate result of perform
        /// </typeparam>
        /// <param name="intervals">The packet intervals</param>
        /// <param name="srcTile">The source tile</param>
        /// <param name="perform">
        /// The function to perform on each tile-part packet interval
        /// </param>
        private void EnumerateIntervals<T>(
            IEnumerable<PacketInterval> intervals,
            JP2Tile srcTile,
            Func<JP2TilePart, int, int, T, T> perform)
        {
            var scanner = new SegmentScanner(srcTile.GetPacketCounts());
            T aggregate = default(T);
            foreach (var ival in intervals)
            {
                // use inclusive end packet index
                SegmentLocation segStart = scanner.Find(ival.PacketStart);
                SegmentLocation segEnd = scanner.Find(ival.PacketEnd - 1);
                int tpStart = segStart.SegmentIdx;
                int tpEnd = segEnd.SegmentIdx;
                // an interval of packets in the tile may span across multiple tile-parts.
                for (int tpIdx = tpStart; tpIdx <= tpEnd; tpIdx++)
                {
                    JP2TilePart tp = srcTile.OpenTilePart(tpIdx);
                    int pckStart = tpIdx == tpStart
                        ? segStart.InSegmentIdx : 0;
                    int pckEnd = tpIdx == tpEnd
                        ? (segEnd.InSegmentIdx + 1) : tp.Packets;
                    int pckCount = pckEnd - pckStart;
                    aggregate = perform(tp, pckStart, pckCount, aggregate);
                }
            }
        }

        private void CopyTileResLevel(JP2Codestream dst, JP2Codestream src, ushort tIdx, int r)
        {
            int decomps = src.DecompositionLevels;
            var dstTilePart = dst.CreateTilePart(tIdx, r == decomps);

            // get all packets for this resolution level and source tile
            // currently support only a single tile in source image for every
            // destination tile.
            QueryContext queryContext = new QueryContext(src, 0);
            PreciseQuery preciseQuery = PreciseQuery.Create(queryContext);
            var intervals = preciseQuery
                .Resolution(r)
                .Execute();
            JP2Tile srcTile = src.Tiles[0];
            // add packet lengths
            Func<JP2TilePart, int, int, int, int> addPacketLengths =
                (tpSrc, pckStart, pckCount, voidParam) =>
                {
                    var lengths = tpSrc.GetPacketLengths(pckStart, pckCount);
                    dstTilePart.AddPacketLengths(lengths);
                    return voidParam;
                };
            EnumerateIntervals<int>(intervals, srcTile, addPacketLengths);

            // bulk transfer packet data
            dstTilePart.Flush();
            byte[] buffer = new byte[1 << 16];
            Func<JP2TilePart, int, int, uint, uint> bulkTransfer =
                (tpSrc, pckStart, pckCount, dstOffset) =>
                {
                    uint dataCount = JP2TilePart.BulkTransferData(
                        dstTilePart,
                        dstOffset,
                        tpSrc,
                        pckStart,
                        pckCount,
                        buffer);
                    return dstOffset + dataCount;
                };
            EnumerateIntervals<uint>(
                intervals,
                srcTile,
                bulkTransfer);
            dstTilePart.Close();
            srcTile.Close();
        }

        private void Dispose()
        {
            foreach (var fs in _files)
            {
                fs.Dispose();
            }
            _dest.Dispose();
        }

        private SizMarker GetDestSiz()
        {
            Size dstRefSize = new Size();
            JP2Codestream src = _firstCodestream;
            dstRefSize.Width = _imageCount.Width * src.ImageSize.Width;
            dstRefSize.Width += src.ImageOffset.X;
            dstRefSize.Height = _imageCount.Height * src.ImageSize.Height;
            dstRefSize.Height += src.ImageOffset.Y;
            SizMarker siz = new SizMarker(
                dstRefSize,
                _firstCodestream.ImageOffset,
                _firstCodestream.TileSize,
                _firstCodestream.TileOffset,
                _firstCodestream.Components,
                _firstCodestream.Precisions,
                _firstCodestream.SubSamplingX,
                _firstCodestream.SubSamplingY);
            return siz;
        }

        private bool HasBorders(JP2Codestream cs)
        {
            return
                cs.ImageSize.Width % cs.TileSize.Width == 0 &&
                cs.ImageSize.Height % cs.TileSize.Height == 0;
        }

        private Jp2File OpenJp2(int idx)
        {
            if (_jp2s[idx] == null)
            {
                _jp2s[idx] = Jp2File.Open(OpenStream(idx));
            }
            return _jp2s[idx];
        }

        private FileStream OpenStream(int idx)
        {
            if (_files[idx] == null)
            {
                _files[idx] = new FileStream(_paths[idx],
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1 << 14 /* 16KB */);
            }
            return _files[idx];
        }

        private void Run()
        {
            ThrowIfHasInnerTiles(_firstCodestream);
            ThrowIfTooManyTiles();

            SizMarker siz = GetDestSiz();
            // Lets generate a tile-part for each tile-resolution in the source images
            int tiles = _imageCount.Width * _imageCount.Height;
            int expectedTileParts = tiles * (_firstCodestream.DecompositionLevels + 1);
            CodMarker cod = _firstCodestream.Markers[MarkerType.COD] as CodMarker;
            QcdMarker qcd = _firstCodestream.Markers[MarkerType.QCD] as QcdMarker;
            WarnIfClipped(_firstCodestream, cod);
            ComMarker com = new ComMarker("https://github.com/plroit/Skyreach");
            var markers = new List<MarkerSegment>()
            {
                siz, cod, qcd, com
            };
            Jp2File dst = Jp2File.Create(
                markers, 
                expectedTileParts, 
                false, 
                _dest);            
            for (ushort tIdx = 0; tIdx < tiles; tIdx++)
            {
                CopyTile(tIdx, dst);
            }
            dst.Flush();            
        }

        private void ThrowIfDifferentInputs(
            JP2Codestream src, JP2Codestream lhs)
        {
            bool rc = true;
            rc = rc && src.ImageSize == lhs.ImageSize;
            rc = rc && src.TileSize == lhs.TileSize;
            rc = rc && src.Components == lhs.Components;
            rc = rc && src.DecompositionLevels == lhs.DecompositionLevels;
            rc = rc && src.QualityLayers == lhs.QualityLayers;
            rc = rc && src.SubSamplingX.SequenceEqual(lhs.SubSamplingX);
            rc = rc && src.SubSamplingY.SequenceEqual(lhs.SubSamplingY);
            rc = rc && src.Progression == lhs.Progression;
            rc = rc && src.Precisions.SequenceEqual(lhs.Precisions);
            // I should also check precinct partition and codeblock size
            // equality. Generally speaking, COD and QCD markers should be identical.
            if (!rc)
            {
                throw new ArgumentException(String.Concat(
                    "One of the images provided did not match the first ",
                    "image in one of the encoding parameters. COD and QCD ",
                    "markers in every image should be identical"));
            }
        }

        private void ThrowIfHasInnerTiles(JP2Codestream src)
        {
            if (src.ImageSize != src.TileSize)
            {
                throw new NotImplementedException(
                    "Inner tiling in source images");
            }
        }

        private void ThrowIfTooManyTiles()
        {
            int totalTiles = _imageCount.Width * _imageCount.Height;
            if (totalTiles > JP2Codestream.MAX_TILES)
            {
                throw new ArgumentOutOfRangeException("too many tiles");
            }
        }
    }
}