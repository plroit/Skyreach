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
        private List<uint> _packetLengths;

        public byte ZIndex { get; private set; }

        protected internal PltMarker(ushort markerLength, byte[] markerBody)
            : base(MarkerType.PLT, markerLength, markerBody)
        {
            _packetLengths = new List<uint>();
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
        public PltMarker(List<uint> packetLengths, byte zIndex, int firstLengthIdx) 
            : base(MarkerType.PLT)
        {
            _packetLengths = new List<uint>();
            MemoryStream mem = new MemoryStream();
            ZIndex = zIndex;
            int pltLength = 0;
            int encodedCount = 0;
            for (int idx = firstLengthIdx; idx < packetLengths.Count; idx++ )
            {
                _packetLengths.Add(packetLengths[idx]);
                pltLength += mem.EncodeVarLen(packetLengths[idx]);
                encodedCount++;
                if (pltLength > PLT_LENGTH_LIMIT)
                {
                    // do not delete the last packet length, it is fine!
                    break;
                }
            }
            // it is not efficient, but leave actual encoding to 
            // a call for GenerateMarkerSegment. Do not use stream
            // for markerBody

        }

        protected internal override void Parse()
        {
            if(_markerLength < 4)
            {
                throw new ArgumentOutOfRangeException(
                    "PLT marker is too short");
            }

            var mem = new MemoryStream(_markerBody);
            ZIndex = mem.ReadUInt8();
            int bytesLeft = _markerLength - 3;
            while(bytesLeft > 0)
            {
                uint currPacketLength = mem.DecodeVarLen(ref bytesLeft);
                _packetLengths.Add(currPacketLength);
            }
        }

        public override byte[] GenerateMarkerBody()
        {
            int len = 0;
            int packets = _packetLengths.Count();
            if (packets == 0)
            {
                return new byte[0];
            }
            var mem = new MemoryStream(packets);
            mem.WriteUInt8(ZIndex);
            for (int p = 0; p < packets; p++)
            {
                len += mem.EncodeVarLen(_packetLengths[p]);
            }
            if (len > UInt16.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    "PLT is more than 2^16bytes, need to create multiple PLT");
            }
            return mem.ToArray();
        }

        public IEnumerator<uint> GetEnumerator()
        {
            return _packetLengths.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int PacketCount { get { return _packetLengths.Count(); } }

    }
}
