using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query.Precise
{
    internal class RpclPreciseQuery : PreciseQuery
    {
        internal RpclPreciseQuery(QueryContext ctx)
            : base(ctx)
        {

        }

        protected override IEnumerable<PacketInterval> NonEmptyExecute()
        {

            if (IsFastLaneResolutionQuery)
            {
                return PickAllPacketsInRes().ToList();
            }
            else if (IsFastLaneResolutionPositionQuery)
            {
                throw new NotImplementedException();
            }
            else
            {
                return base.NonEmptyExecute();
            }
        }

        /// <summary>
        /// true iff query is simple get all packets in resolution.
        /// RPCL progression order makes it very easy to get
        /// an interval of packets that share the same resolution.
        /// </summary>
        private bool IsFastLaneResolutionQuery
        {
            get
            {
                return _qp.HasResLevel &&
                    !(_qp.HasRegion || _qp.HasQualLayer || _qp.HasComp);
            }
        }

        private bool IsFastLaneResolutionPositionQuery
        {
            get
            {
                return _qp.HasResLevel && _qp.HasResLevel &&
                    !(_qp.HasQualLayer || _qp.HasComp);
            }
        }

        private PacketInterval PickAllPacketsInRes()
        {
            int resStart = Enumerable
                .Range(0, _qp.ResLevel)
                .Select(PacketsInResolution)
                .Sum();
            int resCount = PacketsInResolution(_qp.ResLevel);
            int resEnd = resStart + resCount;
            return new PacketInterval(_ctx.TileIdx, resStart, resEnd);
        }

        private int PacketsInResolution(int res)
        {
            Size precincts = _ctx.Precincts[res];
            int packetsInRes = precincts.Width * precincts.Height;
            packetsInRes *= _ctx.Codestream.QualityLayers;
            packetsInRes *= _ctx.Codestream.Components;
            return packetsInRes;
        }
    }
}
