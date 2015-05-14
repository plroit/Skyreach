using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query
{
    public class PacketInterval
    {
        public ushort TileIndex { get; private set; }

        public int PacketStart { get; private set; }

        public int PacketEnd { get; private set; }

        public PacketInterval(ushort tileIdx, int packetStart, int packetEnd)
        {
            TileIndex = tileIdx;
            PacketStart = packetStart;
            PacketEnd = packetEnd;
        }

        public PacketInterval(ushort tileIdx, int packetStart)
            : this(tileIdx, packetStart, 1)
        {

        }

        public void Extend(int extendBy)
        {
            PacketEnd += extendBy;
        }

        public int Count { get { return PacketEnd - PacketStart; } }

        public static IEnumerable<PacketInterval> Flatten(
            List<PacketInterval> intervals)
        {
            if (!intervals.Skip(1).Any())
            {
                // no more than a single interval, 
                // everything is flattened
                return intervals;
            }
            var flattened = new List<PacketInterval>();
            PacketInterval curr = intervals.First();
            flattened.Add(curr);
            foreach (var interval in intervals.Skip(1))
            {
                bool isExtension = interval.PacketStart == curr.PacketEnd;
                if (isExtension)
                {
                    curr.Extend(interval.Count);
                }
                else
                {
                    curr = interval;
                    flattened.Add(curr);
                }
            }
            return flattened;
        }

        public List<PacketInterval> ToList()
        {
            return new List<PacketInterval>() { this };
        }
    }
}
