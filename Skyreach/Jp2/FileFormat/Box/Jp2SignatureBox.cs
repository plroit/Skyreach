using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat.Box
{
    public class Jp2SignatureBox : Jp2Box
    {
        private static byte[] JP2_SIGNATURE;

        static Jp2SignatureBox()
        {
            // '<CR><LF><0x87><LF>' 
            JP2_SIGNATURE = new byte[] { 0x0D, 0x0A, 0x87, 0x0A };
        }

        public Jp2SignatureBox()
            // The signature box has a fixed length
            // it must be the first box in the file
            : base((uint)BoxTypes.Jp2SignatureBox, 12L, 8)
        {

        }

        public override byte[] GenerateBoxContent()
        {
            // should return a copy of the array.
            return JP2_SIGNATURE.ToArray();

        }
    }
}
