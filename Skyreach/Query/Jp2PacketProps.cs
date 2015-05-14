using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Query
{
    public class Jp2PacketProps
    {
        public int PacketIdx { get; set; }
        public int ResLevel { get; set; }
        public int QualityLayer { get; set; }
        public int Component { get; set; }
        public Point Precinct { get; set; }
    }
}
