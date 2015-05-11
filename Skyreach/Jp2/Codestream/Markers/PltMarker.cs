using Skyreach.Util;
using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class PltMarker : MarkerSegment, IEnumerable<uint>
    {
        /// <summary>
        /// Maximal number of body bytes in PLT marker segment that this
        /// library will emit. Should actually be identical to MAX_BODY_BYTES.
        /// We offset MAX_BODY_BYTES with a small number so that the last 
        /// generated packet length that will cross this threshold will not 
        /// have to be deleted
        /// </summary>
        public const int PLT_LENGTH_LIMIT = (MarkerSegment.MAX_BODY_BYTES - 32);

        public byte ZIndex { get; private set; }

        private ushort _encodedBytes;

        private readonly MemoryStream _encodedPacketLengths;

        protected internal PltMarker(ushort markerLength, byte[] markerBody)
            : base(MarkerType.PLT, markerLength, markerBody)
        {
            _encodedBytes = (ushort) (markerLength - 3);
            _encodedPacketLengths = new MemoryStream(
                _markerBody,
                1,
                _encodedBytes);
            Parse();
        }

        /// <summary>
        /// Constructs a PLT marker from given packet lengths
        /// for convenience, the PLT marker can be constructed from
        /// arbitrary positions in the lengths list.
        /// This is done in order to generate multiple PLT markers
        /// that cover all of the tileparts packets. 
        /// For a count of all lengths that were generated, see PacketCount
        /// </summary>
        /// <param name="packetLengths"></param>
        /// <param name="zIndex"></param>
        /// <param name="firstLengthIdx"></param>
        public PltMarker(byte zIndex) 
            : base(MarkerType.PLT)
        {
            _encodedPacketLengths = new MemoryStream();
            ZIndex = zIndex;
        }

        public int Ingest(IEnumerable<uint> packetLengths)
        {
            // the plt length limit is a soft limit,
            // we can overstep it with by a single packet
            int bytesLeft = PLT_LENGTH_LIMIT - _encodedBytes;
            int count = 0;
            foreach(uint packLen in packetLengths)
            {                
                int inc = _encodedPacketLengths.EncodeVarLen(packLen);
                _encodedBytes += (ushort) inc;
                bytesLeft -= inc;
                count++;
                if(bytesLeft < 0)
                {
                    break;
                }
            }
            // 2 bytes for the length field,
            // another byte for the Zplt field
            _markerLength = (ushort) (_encodedBytes + 3);
            _isDirty = true;
            return count;
        }

        protected override void Parse()
        {
            if(_markerLength < 4)
            {
                throw new ArgumentOutOfRangeException(
                    "PLT marker is too short");
            }
            ZIndex = _markerBody[0];
        }

        protected override byte[] GenerateMarkerBody()
        {
            if (_encodedBytes == 0)
            {
                return new byte[0];
            }
            // not very memory efficient, but 5 lines!
            var mem = new MemoryStream(_encodedBytes + 1);
            mem.WriteUInt8(ZIndex);
            _encodedPacketLengths.Seek(0, SeekOrigin.Begin);
            _encodedPacketLengths.CopyTo(mem);
            return mem.ToArray();
        }


        public IEnumerator<uint> GetEnumerator()
        {
            // invoking any LINQ queries from two
            // threads simultaneously would be disastrous 
            _encodedPacketLengths.Seek(0, SeekOrigin.Begin);
            int bytesLeft = _encodedBytes;
            while (bytesLeft > 0)
            {
                uint currPacketLength =
                    _encodedPacketLengths.DecodeVarLen(ref bytesLeft);
                yield return currPacketLength;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// returns true when this PLT marker segment
        /// can not have any more packet lengths added into it
        /// </summary>
        public bool IsFull
        {
            get
            {
                return _encodedBytes >= PLT_LENGTH_LIMIT;
            }
        }
    }
}
