using Skyreach.Jp2.Codestream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Skyreach.Jp2.FileFormat.Box
{
    public class ContiguousCodestreamBox : Jp2Box
    {
        public JP2Codestream Codestream { get; protected set; }

        public ContiguousCodestreamBox(JP2Codestream codestream) 
            : base((uint) BoxTypes.CodestreamBox)
        {
            Codestream = codestream;
            Length = 0; // explicitly do not specify length
            // If advertised length sis zero, then it is assumed
            // that this is the last box in the file
            // and its length is all of the remaining bytes
        }
    }
}
