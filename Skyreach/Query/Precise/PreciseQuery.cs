using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query.Precise
{
    /// <summary>
    /// PreciseQuery performs a query over the packets of a tile in a codestream 
    /// based on precise parameters.
    /// You can either specify a precise value for a parameter, 
    /// or leave unchanged to perform the query on all possible values.
    /// Examples of possible queries include:
    /// Get all packets from resolution level 2.
    /// Get all packets from quality layer 0 and component 1
    /// Get all packets that their top left precinct position is (64,128)
    /// These queries are typically useful when one wants to divide the
    /// packet sequence to tile-parts based on a common property such as
    /// a resolution level or a quality layer.   
    /// </summary>
    public class PreciseQuery
    {
        internal static readonly Point ANY_POSITION = new Point(-1, -1);

        internal const int ANY_VALUE = -1;

        protected readonly QueryContext _ctx;

        protected PreciseQueryParams _qp;

        protected PreciseQuery(QueryContext ctx)
        {
            _ctx = ctx;
            Reset();
        }

        public static PreciseQuery Create(QueryContext ctx)
        {
            switch(ctx.Codestream.Progression)
            {
                case Jp2.ProgressionOrder.RPCL:
                    return new RpclPreciseQuery(ctx);
                default:
                    return new PreciseQuery(ctx);
            }
        }

        public PreciseQuery Reset()
        {
            _qp = new PreciseQueryParams(_ctx);
            return this;
        }

        public PreciseQuery Resolution(int res)
        {
            _qp.ResLevel = res;
            return this;
        }

        public PreciseQuery Quality(int qualityLayer)
        {
            _qp.QualLayer = qualityLayer;
            return this;
        }

        public PreciseQuery Component(int comp)
        {
            _qp.Component = comp;
            return this;
        }

        public IEnumerable<PacketInterval> Execute()
        {
            if(!_qp.IsEmpty)
            {
                return NonEmptyExecute();
            }
            else
            {
                int packets = CalcPacketsInTile();
                var ival = new PacketInterval(_ctx.TileIdx, 0, packets);
                return ival.ToList();
            }    
        }

        private int CalcPacketsInTile()
        {
            int packets = 0;
            int resolutions = _ctx.Codestream.DecompositionLevels + 1;
            int layers = _ctx.Codestream.QualityLayers;
            int components = _ctx.Codestream.Components;

            // assume that there is no sub-sampling
            // and no change in number of layers, resolutions
            // or precinct partitions in codestream QCC or tile-specific
            // QCD/QCC markers.
            for (int r = 0; r < resolutions; r++)
            {
                Size precincts = _ctx.Precincts[r];
                int packetsInRes = 0;
                packetsInRes += layers * components;
                packetsInRes *= precincts.Width * precincts.Height;
                packets += packetsInRes;
            }
            return packets;
        }        

        protected virtual IEnumerable<PacketInterval> NonEmptyExecute()
        {
            // this is possibly a deferred execution enumerable
            // You should record the state of the query parameters
            // and check the match only against the recorded state.
            //
            // avoid the following mistake of user code:
            // 
            // var intervals = query.Resolution(0).Execute();
            // query.Resolution(1);
            // foreach(var ival in interval) { ... }
            var qp = _qp.Clone() as PreciseQueryParams;
            var packets = PacketEnumerator.Create(_ctx);
            int start = -1;
            int end = -1;

            foreach (var pack in packets.Where(qp.Matches))
            {
                if (start == -1)
                {
                    start = pack.PacketIdx;
                    end = start + 1;
                }
                else if (pack.PacketIdx == end)
                {
                    // the current matching packet
                    // has extended our interval
                    end++;
                }
                else
                {
                    // ok, done for now, grab preceding interval
                    // and create a new one.
                    var ival = new PacketInterval(_ctx.TileIdx, start, end);
                    start = pack.PacketIdx;
                    end = start + 1;
                    yield return ival;
                }
            }
            // now grab the last interval that could not be grabbed
            // due to the end of the enumeration.
            if (start != -1)
            {
                yield return new PacketInterval(_ctx.TileIdx, start, end);
            }
        }

        internal protected class PreciseQueryParams : ICloneable
        {
            private readonly QueryContext _ctx;

            internal protected PreciseQueryParams(QueryContext ctx)
            {
                _ctx = ctx;
                TopLeft = ANY_POSITION;
                Component = ANY_VALUE;
                QualLayer = ANY_VALUE;
                ResLevel = ANY_VALUE;
            }

            public object Clone()
            {
                PreciseQueryParams t = new PreciseQueryParams(_ctx);
                t.TopLeft = TopLeft;
                t.Component = Component;
                t.QualLayer = QualLayer;
                t.ResLevel = ResLevel;
                return t;
            }

            internal bool Matches(Jp2PacketProps pack)
            {
                // either query is for ALL_VALUES
                // or you should specifically match 
                // what was requested.
                bool isMatch = true;
                isMatch = isMatch && (!HasQualLayer || pack.QualityLayer == QualLayer);
                isMatch = isMatch && (!HasResLevel|| pack.ResLevel == ResLevel);
                isMatch = isMatch && (!HasComp|| pack.Component == Component);
                isMatch = isMatch && (!HasRegion || PositionMatches(pack, TopLeft));
                return isMatch;
            }

            internal bool PositionMatches(Jp2PacketProps pack, Point topLeft)
            {
                Point ppxy = _ctx.Log2Partitions[pack.ResLevel];                
                Point precinctPosition = new Point(
                    pack.Precinct.X << ppxy.X, pack.Precinct.Y << ppxy.Y);
                return precinctPosition == topLeft;
            }

            internal Point TopLeft { get; set; }

            internal int ResLevel { get; set; }

            internal int QualLayer { get; set; }

            internal int Component { get; set; }

            internal bool IsEmpty 
            { 
                get 
                {
                    return 
                        !HasResLevel &&
                        !HasRegion && 
                        !HasQualLayer && 
                        !HasComp;
                } 
            }

            internal bool HasResLevel { get { return ResLevel != ANY_VALUE; } }

            internal bool HasComp { get { return Component != ANY_VALUE; } }

            internal bool HasQualLayer { get { return QualLayer != ANY_VALUE; } }

            internal bool HasRegion { get { return TopLeft != ANY_POSITION; } }
        }
    }
}
