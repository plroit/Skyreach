using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Util.Streams
{
    public static class BigEndianWriteExtensions
    {
        public static void WriteUInt64(this Stream stream, ulong value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }
            stream.Write(buf, 0, sizeof(ulong));
        }

        public static void WriteUInt32(this Stream stream, uint value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }
            stream.Write(buf, 0, sizeof(uint));
        }

        public static void WriteUInt16(this Stream stream, ushort value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }
            stream.Write(buf, 0, sizeof(ushort));
        }

        public static void WriteUInt8(this Stream stream, byte value)
        {
            stream.WriteByte(value);
        }
    }
}
