using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query.BoundingBox
{
    // TODO - Implement this class
    public class BoundingBoxQuery
    {        
        protected readonly QueryContext _ctx;

        protected Rectangle _roi;

        protected int _maxResLevel;

        protected int _maxQualLayer;

        protected int _compIdx;

        public BoundingBoxQuery(QueryContext ctx)
        {
            _ctx = ctx;
            _maxResLevel = ctx.Codestream.DecompositionLevels;
            _maxQualLayer = ctx.Codestream.QualityLayers - 1;
            _roi = _ctx.Tile[_maxResLevel];
        }

        public IEnumerable<PacketEnumerator> Execute()
        {
            throw new NotImplementedException();
        }

        public BoundingBoxQuery Resolution(int maxResolution)
        {
            return this;
        }

        public BoundingBoxQuery Quality(int maxLayers)
        {
            if(maxLayers <= 0)
            {
                throw new ArgumentException("maxLayers");
            }
            return this;
        }

        public BoundingBoxQuery RegionOfInterest(Rectangle roi)
        {
            if(roi == null || roi.IsEmpty)
            {
                throw new ArgumentException("roi");
            }
            _roi = roi;
            return this;
        }




    }
}
