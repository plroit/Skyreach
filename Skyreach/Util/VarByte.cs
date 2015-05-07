using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Util.Streams
{
    /// <summary>
    /// Extension methods to encode a value
    /// using a Variable Length byte encoding.
    /// Encode and decode BIG_ENDIAN. The last 7 bits
    /// of each byte are concatenated to form the value.
    /// If the MSB bit is on, then you should continue
    /// decoding to the next byte. MSB is off - then this is the last byte.
    /// </summary>
    public static class VarByteLen
    {
        public static int EncodeVarLen(this Stream stream, uint value)
        {
            // How many 7-bit groups can this number be stored in?
            // Could have used BitHacks MSBPOS then divide-ceiling with 7
            // but I guess it would have taken more time than just simple
            // counting :-)
            uint val = value;
            int bytes = 0;
            do
            {
                
                bytes++;
                val >>= 7;
            }
            while(val > 0);

            int shift = 7 * (bytes - 1);
            int bytesLeft = bytes;
            do
            {
                val = value >> shift;
                byte b = (byte) (val & 0x7F);
                b |= (byte)(bytesLeft > 1 ? 0x80 : 0);
                stream.WriteUInt8(b);
                shift -= 7;
                bytesLeft--;
            }
            while (bytesLeft > 0);

            return bytes;
        }

        public static uint DecodeVarLen(this Stream stream, ref int bytesLeft)
        {
            uint currPacketLength = 0;
            byte currByte = 0;
            do
            {
                currByte = stream.ReadUInt8();
                currPacketLength = (currPacketLength << 7) | (byte)(currByte & 0x7F);
                bytesLeft--;
            } while ((bytesLeft > 0) && (currByte & 0x80) == 0x80);
            return currPacketLength;
        }
    }
}
