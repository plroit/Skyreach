using Skyreach.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream
{
    public class JP2Packet : CodestreamElement
    {

        public JP2Packet(JP2TilePart parent, long offset, long length)
            : base(parent, offset, length)
        {

        }
    }
}
