using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class QcdMarker : MarkerSegment
    {
        protected internal QcdMarker(
            ushort markerLength, 
            byte[] markerBody)
            : base(MarkerType.QCD, markerLength, markerBody)
        {
            Parse();
        }

        public QcdMarker()
            : base(MarkerType.QCD)
        {

        }

        protected override void Parse()
        {
            // TODO - parsing of QCD
            base.Parse();
        }
    }
}
