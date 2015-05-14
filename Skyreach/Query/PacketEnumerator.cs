using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query
{
    public abstract class PacketEnumerator : IEnumerable<Jp2PacketProps>
    {
        protected QueryContext _ctx;

        protected PacketEnumerator(QueryContext ctx)
        {
            _ctx = ctx;
        }

        public static PacketEnumerator Create(QueryContext ctx)
        {
            switch(ctx.Codestream.Progression)
            {
                case Jp2.ProgressionOrder.RPCL:
                    return new RpclPacketEnumerator(ctx);
                case Jp2.ProgressionOrder.LRCP:
                    return new LrcpPacketEnumerator(ctx);
                default:
                    throw new NotImplementedException();
            }
        }

        public abstract IEnumerator<Jp2PacketProps> GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class RpclPacketEnumerator : PacketEnumerator
    {
        public RpclPacketEnumerator(QueryContext ctx)
            : base(ctx)
        {
        }

        public override IEnumerator<Jp2PacketProps> GetEnumerator()
        {
            int packIdx = 0;
            int decompositions = _ctx.Codestream.DecompositionLevels;
            int components = _ctx.Codestream.Components;
            int layers = _ctx.Codestream.QualityLayers;
            for (int r = 0; r <= decompositions; r++)
            {
                Size precincts = _ctx.Precincts[r];
                Point p = new Point();
                for (p.Y = 0; p.Y < precincts.Height; p.Y++)
                {
                    for (p.X = 0; p.X < precincts.Width; p.X++)
                    {
                        for (int c = 0; c < components; c++)
                        {
                            for (int q = 0; q < layers; q++)
                            {
                                yield return new Jp2PacketProps();
                                //{
                                //    // point is a struct, 
                                //    // will copy value-type
                                //    ResLevel = r,
                                //    Precinct = p,
                                //    Component = c,
                                //    QualityLayer = q,
                                //    PacketIdx = packIdx
                                //};
                                // packIdx is a closure in the iterator method
                                packIdx++;
                            }
                        }
                    }
                }
            }
        }
    }

    public class LrcpPacketEnumerator : PacketEnumerator
    {
        public LrcpPacketEnumerator(QueryContext ctx)
            : base(ctx)
        {
        }

        public override IEnumerator<Jp2PacketProps> GetEnumerator()
        {
            int packIdx = 0;
            int decompositions = _ctx.Codestream.DecompositionLevels;
            int components = _ctx.Codestream.Components;
            int layers = _ctx.Codestream.QualityLayers;
            for (int l = 0; l < layers; l++)
            {
                for (int r = 0; r <= decompositions; r++)
                {
                    for (int c = 0; c < components; c++)
                    {
                        Size precincts = _ctx.Precincts[r];
                        Point p = new Point();
                        for (p.Y = 0; p.Y < precincts.Height; p.Y++)
                        {
                            for (p.X = 0; p.X < precincts.Width; p.X++)
                            {
                                yield return new Jp2PacketProps
                                {
                                    // point is a struct, 
                                    // will copy value-type
                                    ResLevel = r,
                                    Precinct = p,
                                    Component = c,
                                    QualityLayer = l,
                                    PacketIdx = packIdx
                                };
                                // packIdx is a closure in the iterator method
                                packIdx++;
                            }
                        }
                    }
                }                
            }
        }
    }
}
