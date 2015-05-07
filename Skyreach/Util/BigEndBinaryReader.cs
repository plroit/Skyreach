using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Util.Streams
{
    /// <summary>
    /// Naive BigEndian Reader
    /// Uses Array.Reverese and BitConverter.
    /// MarkerSegments are not expected to be 
    /// very large, so parsing should not
    /// be a bottleneck 
    /// </summary>
    public static class BigEndBinaryReader
    {
        public static ulong ReadUInt64(this Stream stream)
        {
            byte[] buf64 = new byte[sizeof(ulong)];
            stream.Read(buf64, 0, sizeof(ulong));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf64);
            }
            return BitConverter.ToUInt64(buf64, 0);
        }

        public static uint ReadUInt32(this Stream stream)
        {
            byte[] buf32 = new byte[sizeof(uint)];
            stream.Read(buf32, 0, sizeof(uint));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf32);
            }
            return BitConverter.ToUInt32(buf32, 0);
        }

        public static ushort ReadUInt16(this Stream stream)
        {
            byte[] buf16 = new byte[sizeof(ushort)];
            stream.Read(buf16, 0, sizeof(ushort));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf16);
            }
            return BitConverter.ToUInt16(buf16, 0);
        }

        public static byte ReadUInt8(this Stream stream)
        {
            return (byte) stream.ReadByte();
        }
    }
}
