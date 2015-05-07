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
    public abstract class QueryPlanner
    {
        protected readonly JP2Codestream _cs;

        /// <summary>
        /// ppx,ppy for each resolution level.
        /// Actual precinct dimensions are [2^ppx,2^ppy]
        /// </summary>
        protected readonly IReadOnlyList<Point> _log2Partitions;

        protected readonly Size[][] _tileResToPrecinctCount;

        protected readonly Rectangle[][] _tileResToTileRect;

        protected Rectangle _image;

        protected int _resolution;

        protected int _maxLayer;

        protected Point _components;

        protected int _tileIdx { get; set; }

        protected ushort _tileCount;

        public QueryPlanner(JP2Codestream codestream)
        {
            _cs = codestream;
            _image = new Rectangle(_cs.ImageOffset, _cs.ImageSize);
            _tileCount = (ushort)(_cs.TileCount.Width * _cs.TileCount.Height);
            _tileResToPrecinctCount = new Size[_tileCount][];
            _tileResToTileRect = new Rectangle[_tileCount][];
            CodMarker cod = _cs.Markers[MarkerType.COD] as CodMarker;
            _log2Partitions = cod.PrecinctPartitions;
            Reset();
        }

        public QueryPlanner Reset()
        {
            _tileIdx = -1;
            _components = Point.Empty;
            _maxLayer = _cs.QualityLayers;
            _resolution = 0; // R0
            return this;
        }

        public QueryPlanner Resolution(int res)
        {
            _resolution = res;
            return this;
        }

        public QueryPlanner Quality(int maxQualityLayer) 
        {
            _maxLayer = maxQualityLayer;
            return this;
        }

        public QueryPlanner Tile(int tileIdx)
        {
            _tileIdx = tileIdx;
            return this;
        }

        public QueryPlanner Components(int startComponent, int countComponents)
        {
            _components = new Point(startComponent, countComponents);
            return this;
        }

        public abstract IEnumerable<PacketInterval> Execute();

        protected int Resolutions 
        {
            get { return _cs.DecompositionLevels + 1; } 
        }

        protected Size GetPrecinctCount(ushort tileIdx, int resolution)
        {
            if (_tileResToPrecinctCount[tileIdx] == null)
            {
                _tileResToPrecinctCount[tileIdx] = new Size[Resolutions];
            }
            if (_tileResToPrecinctCount[tileIdx][resolution].IsEmpty)
            {
                _tileResToPrecinctCount[tileIdx][resolution] =
                    CalculatePrecinctCount(tileIdx, resolution);
            }
            return _tileResToPrecinctCount[tileIdx][resolution];
        }

        protected Rectangle GetTileRect(ushort tileIdx, int resolution)
        {
            if (_tileResToTileRect[tileIdx] == null)
            {
                _tileResToTileRect[tileIdx] = new Rectangle[Resolutions];
            }
            if (_tileResToTileRect[tileIdx][resolution].IsEmpty)
            {
                _tileResToTileRect[tileIdx][resolution] =
                    CalculateTileRect(tileIdx, resolution);
            }
            return _tileResToTileRect[tileIdx][resolution];
        }

        private Size CalculatePrecinctCount(ushort tileIdx, int resolution)
        {
            Rectangle tile = GetTileRect(tileIdx, resolution);
            Point partition = _log2Partitions[resolution];
            int minHorz = tile.Left >> partition.X;
            int minVert = tile.Top >> partition.Y;

            int maxHorz = BitHacks.DivShiftCeil(tile.Right, partition.X);
            int maxVert = BitHacks.DivShiftCeil(tile.Bottom, partition.Y);
            
            return new Size(maxHorz - minHorz, maxVert - minVert);
        }

        private Rectangle CalculateTileRect(int tileIdx, int resolution)
        {
            // TODO - fast lane computation for images
            // without tile or image offset, and 2^x tile sizes
            int r = resolution;
 	        int tileX = tileIdx % _cs.TileCount.Width;
            int tileY = tileIdx / _cs.TileCount.Width;
            Size sz = _cs.TileSize;
            Point ul = new Point(
                Math.Max(tileX*sz.Width, _cs.TileOffset.X),
                Math.Max(tileY*sz.Height, _cs.TileOffset.Y));
            Rectangle tile = new Rectangle(ul, sz);
            tile.Intersect(_image);

            tile.X = BitHacks.DivShiftCeil(tile.X, r);
            tile.Y = BitHacks.DivShiftCeil(tile.Y, r);
            tile.Width = BitHacks.DivShiftCeil(tile.Width, r);
            tile.Height = BitHacks.DivShiftCeil(tile.Height, r);

            return tile;
        }

        private static Rectangle GetPartitions(Rectangle rect, Point log2Partition)
        {
            // The rectangle is partitioned over the 
            // grid with partition of size [2^x, 2^y].           
            Point ul = rect.Location; // upper-left, included
            Point br = rect.Location + rect.Size; // bottom-right, excluded
            // map corner points in the rectangle to the indices
            // of the partitions they are located at.
            ul.X = ul.X >> log2Partition.X;
            ul.Y = ul.Y >> log2Partition.Y;
            // bottom-right of the returned bounds is excluded.
            // map it using Math.Ceiling()
            br.X = BitHacks.DivShiftCeil(br.X, log2Partition.X);
            br.Y = BitHacks.DivShiftCeil(br.Y, log2Partition.Y);
            Rectangle bounds = Rectangle.FromLTRB(ul.X, ul.Y, br.X, br.Y);
            return bounds;
        }


        public static QueryPlanner Create(JP2Codestream cs)
        {
            switch(cs.Progression) 
            {
                case Jp2.ProgressionOrder.RPCL:
                    return new RPCLQueryPlanner(cs);
                default://TODO
                    throw new NotImplementedException("progression");
            }
        }
    }
}