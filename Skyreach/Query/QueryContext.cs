using Skyreach.Jp2.Codestream;
using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query
{
    public class QueryContext
    {
        /// <summary>
        /// The codestream to query
        /// </summary>
        public JP2Codestream Codestream { get; private set; }

        /// <summary>
        /// The image area
        /// </summary>
        public Rectangle Image { get; private set; }

        /// <summary>
        /// The index of the tile to query
        /// </summary>
        public ushort TileIdx { get; private set; }

        /// <summary>
        /// The tile area in every resolutions.
        /// Index 0 is the smallest resolution.
        /// </summary>
        public Rectangle[] Tile { get; private set; }

        /// <summary>
        /// Number of precincts that cover this tile
        /// in every resolution.
        /// Index 0 is the smallest resolution
        /// </summary>
        public Size[] Precincts { get; private set; }

        /// <summary>
        /// ppx,ppy for each resolution level.
        /// Actual precinct dimensions are [2^ppx,2^ppy]
        /// </summary>
        public IReadOnlyList<Point> Log2Partitions { get; private set; }

        public QueryContext(JP2Codestream cs, ushort tileIdx)
        {
            if(cs == null || !cs.IsOpened)
            {
                throw new ArgumentNullException("cs null or not opened");
            }

            if(tileIdx >= (cs.TileCount.Width*cs.TileCount.Height))
            {
                throw new ArgumentOutOfRangeException("tileIdx");
            }

            Codestream = cs;
            TileIdx = tileIdx;            
            Image = new Rectangle(Codestream.ImageOffset, Codestream.ImageSize);
            Tile = new Rectangle[Codestream.DecompositionLevels + 1];
            Precincts = new Size[Codestream.DecompositionLevels + 1];
            CodMarker cod = Codestream.Markers[MarkerType.COD] as CodMarker;
            Log2Partitions = cod.PrecinctPartitions;
            for (int r = 0; r <= Codestream.DecompositionLevels; r++)
            {
                Tile[r] = CalculateTileRect(TileIdx, r);
                Precincts[r] = CalculatePrecinctCount(TileIdx, r);
            }
        }

        private Size CalculatePrecinctCount(ushort tileIdx, int resolution)
        {
            Rectangle tile = Tile[resolution];
            Point partition = Log2Partitions[resolution];
            int minHorz = tile.Left >> partition.X;
            int minVert = tile.Top >> partition.Y;
            int maxHorz = BitHacks.DivShiftCeil(tile.Right, partition.X);
            int maxVert = BitHacks.DivShiftCeil(tile.Bottom, partition.Y);
            return new Size(maxHorz - minHorz, maxVert - minVert);
        }

        private Rectangle CalculateTileRect(ushort tileIdx, int res)
        {
            // TODO - fast lane computation for images
            // without tile or image offset, and 2^x tile sizes
            int resShiftFactor = Codestream.DecompositionLevels - res;
            int tileX = tileIdx % Codestream.TileCount.Width;
            int tileY = tileIdx / Codestream.TileCount.Width;
            Size sz = Codestream.TileSize;
            Point ul = new Point(
                Math.Max(tileX * sz.Width, Codestream.TileOffset.X),
                Math.Max(tileY * sz.Height, Codestream.TileOffset.Y));
            Rectangle tile = new Rectangle(ul, sz);
            // for image offset and border effects
            tile.Intersect(Image);
            // reduce to resolution
            tile.X = BitHacks.DivShiftCeil(tile.X, resShiftFactor);
            tile.Y = BitHacks.DivShiftCeil(tile.Y, resShiftFactor);
            tile.Width = BitHacks.DivShiftCeil(tile.Width, resShiftFactor);
            tile.Height = BitHacks.DivShiftCeil(tile.Height, resShiftFactor);

            return tile;
        }

    }
}
