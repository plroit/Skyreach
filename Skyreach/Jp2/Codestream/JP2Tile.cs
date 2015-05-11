using Skyreach.Query;
using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream
{
    public class JP2Tile
    {
        protected bool _isSealed; 

        protected List<JP2TilePart> _tileParts;

        private const int PACKET_COUNT_NOT_SET = -1;

        /// <summary>
        /// Stores number of packets for each tile-part
        /// in the tile. The items in this list
        /// are initialized when the tile-part is first being opened.
        /// </summary>
        protected List<int> _packetCounts;

        public const int MAX_TILEPARTS = 255;

        public JP2Tile(JP2Codestream parent, ushort tileIdx)
        {
            _tileParts = new List<JP2TilePart>();
            _packetCounts = new List<int>();
            TileIndex = tileIdx;
        }

        internal void Add(JP2TilePart tilePart, bool isLast)
        {
            if(_isSealed)
            {
                throw new InvalidOperationException(
                    "this tile has been sealed, tileIdx: " + TileIndex);
            }
            if(_tileParts.Count() > MAX_TILEPARTS)
            {
                throw new InvalidOperationException(
                    "Tile-parts limit per tile has been reached");
            }

            if(isLast)
            {
                tilePart.TilePartCount = (byte)(_tileParts.Count + 1);
            }
            _tileParts.Add(tilePart);
            _packetCounts.Add(PACKET_COUNT_NOT_SET);
        }

        public JP2TilePart OpenTilePart(int tpIdx)
        {
            if (tpIdx >= TilePartCount)
            {
                throw new IndexOutOfRangeException(
                    "tile part index: " + tpIdx);
            }
            var tp = _tileParts[tpIdx].Open() as JP2TilePart;
            return tp;
        }

        public ushort TileIndex { get; protected set; }
        
        public byte TilePartCount { get { return (byte) _tileParts.Count(); } }

        /// <summary>
        /// Retrieves an enumeration of the count of packets
        /// in each tile-part that belongs to this tile.
        /// This method opens each tile-part it enumerates over
        /// and inspects the number of tile-packets that are signaled
        /// in the PLT or by other means. 
        /// BEWARE, For tiles with a very large number of packets, calling
        /// this method inflates the memory usage of the tile.
        /// Be sure to close the tile after it is no longer used.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> GetPacketCounts()
        {
            for(int t = 0; t < TilePartCount; t++)
            {
                if (_packetCounts[t] == PACKET_COUNT_NOT_SET)
                {
                    var tp = OpenTilePart(t);
                    _packetCounts[t] = tp.Packets;
                }
                yield return _packetCounts[t];
            }
        }

        /// <summary>
        /// Closes all the tile-parts which are associated
        /// with this tile. GC may collect references 
        /// to tile-parts contents in future cycles.
        /// </summary>
        public void Close()
        {
            foreach(var tp in _tileParts)
            {
                tp.Close();
            }
        }
    }
}
