using Skyreach.Jp2.Codestream;
using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query
{
    internal class RPCLQueryPlanner : QueryPlanner
    {

        public RPCLQueryPlanner(JP2Codestream codestream)
            : base(codestream)
        {

        }

        public override IEnumerable<PacketInterval> Execute()
        {
            List<PacketInterval> intervals = new List<PacketInterval>();
            ushort startTile = _tileIdx < 0  ? (ushort)0 : (ushort)_tileIdx;
            ushort endTile = _tileIdx < 0 ? _tileCount : (ushort)(_tileIdx + 1);
            for (ushort t = startTile; t < endTile; t++)
            {
                intervals.AddRange(Execute(t, _resolution));
            }
            return intervals;
        }

        private IEnumerable<PacketInterval> Execute(
            ushort tileIdx, 
            int resolution)
        {
            bool pickAllPacketsInResolution = 
                _components.IsEmpty && 
                _maxLayer == _cs.QualityLayers;
            if(!pickAllPacketsInResolution)
            {
                throw new NotImplementedException();
            }

            return ExecuteTileRes(tileIdx, resolution);
                
        }

        private IEnumerable<PacketInterval> ExecuteTileRes(
            ushort tileIdx, 
            int resolution)
        {
            // the fast lane, get every packet in this tile that
            // belongs to the specified resolution level
            Size partitions = GetPrecinctCount(tileIdx, resolution);
            int startPacket = FirstPacketInResolution(tileIdx, resolution);
            // packets in all of the precincts that cover the same position
            // across all components. This would not work for 
            // sub-sampled images.
            int packetsInPosition = _cs.QualityLayers * _cs.Components;
            int partitionCount = partitions.Width * partitions.Height;
            int endPacket = startPacket +
                (packetsInPosition * partitionCount);
            return new List<PacketInterval> { new PacketInterval(
                    tileIdx,
                    startPacket,
                    endPacket)};
        }

        private int FirstPacketInResolution(ushort tileIdx, int resolution)
        {
            int startPacket = 0;
            int layersMultComps = _cs.QualityLayers * _cs.Components;
            for (int r = _cs.DecompositionLevels; r > resolution; r--)
            {
                // this computation works only for non sub-sampled images
                // for sub-sampled images, there is a different number of
                // precincts per each resolution AND COMPONENT
                Size precincts = GetPrecinctCount(tileIdx, r);
                int precinctsInResolution = precincts.Width * precincts.Height;
                startPacket += layersMultComps * precinctsInResolution;
            }
            return startPacket;
        }
    }
}
