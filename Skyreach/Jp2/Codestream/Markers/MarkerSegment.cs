using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skyreach.Util.Streams;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class MarkerSegment
    {
        public const int MarkerLength = 2; // 2 bytes for marker e.g: 0xFF4F

        /// <summary>
        /// The maximum number of bytes that can be written to a marker 
        /// segment, excluding the marker type and length fields
        /// </summary>
        public const int MAX_BODY_BYTES = UInt16.MaxValue - 2;

        public MarkerType Type { get; protected set; }
        
        /// <summary>
        /// The length field as signaled in the codestream.
        /// The length in bytes of the marker segment,
        /// without the marker.
        /// When present,
        /// markerBody.Length == (_markerLength - 2)
        /// </summary>
        protected ushort _markerLength;

        /// <summary>
        /// The body of the marker segment, 
        /// without BOTH the marker and the length fields
        /// When present, 
        /// markerBody.Length == (_markerLength - 2)
        /// </summary>
        protected byte[] _markerBody;

        /// <summary>
        /// Length in bytes of the MarkerSegment, without the marker itself
        /// The same length that is advertised in the codestream.
        /// </summary>
        public int Length { get { return _markerLength; } }

        public static MarkerSegment Open(Stream stream)
        {
            MarkerType markerType = ReadMarkerType(stream);
            byte[] buffer = new byte[2];
            ushort markerLength = 0;
            byte[] markerBody = null;
            if(IsSegment(markerType))
            {
                stream.Read(buffer, 0, 2);
                markerLength = (ushort)(buffer[0] << 8 | buffer[1]);
                markerBody = new byte[markerLength - 2];
                stream.Read(markerBody, 0, markerBody.Length);
            }
            else
            {
                markerBody = new byte[0];
            }

            switch(markerType)
            {
                case MarkerType.SIZ: return new SizMarker(markerLength, markerBody);
                case MarkerType.COD: return new CodMarker(markerLength, markerBody);
                case MarkerType.QCD: return new QcdMarker(markerLength, markerBody);
                case MarkerType.SOT: return new SotMarker(markerLength, markerBody);
                case MarkerType.TLM: return new TlmMarker(markerLength, markerBody);
                case MarkerType.PLT: return new PltMarker(markerLength, markerBody);
                default: 
                    return new MarkerSegment(markerType, markerLength, markerBody);
            }
        }

        /// <summary>
        /// Tests if a marker is a marker segment
        /// From ISO 115443-1 A.4: Delimiting markers and marker segments  
        /// </summary>
        /// <param name="type"></param>
        /// <returns> True iff the marker belongs to a marker segment</returns>
        public static bool IsSegment(MarkerType type)
        {
            switch(type)
            {
                case MarkerType.SOC:
                case MarkerType.SOD:
                case MarkerType.EOC:
                    return false;
                default:
                    return true;
            }
        }

        protected MarkerSegment(
            MarkerType type, 
            ushort markerLength, 
            byte[] markerBody)
        {
            Type = type;
            _markerLength = markerLength;
            _markerBody = markerBody;
            // continue to specific class constructor and call Parse there.
            // Parsing is class specific and may need initialization of data 
            // members that belong to that class.
            // Parse(); -- keep this comment
        }

        public MarkerSegment(MarkerType type)
        {
            Type = type;
        }

        public void ForceGeneration()
        {
            _markerBody = GenerateMarkerBody();
            if (_markerBody.Length == 0)
            {
                throw new InvalidOperationException(
                    "Could not generate marker segment: " + Type);
            }
            _markerLength = (ushort)(_markerBody.Length + 2);
        }

        protected internal virtual void Parse()
        {
            return; // stub
        }

        internal static MarkerType Peek(Stream stream)
        {
            MarkerType type = ReadMarkerType(stream);
            stream.Seek(-2, SeekOrigin.Current); // reset
            return type;
        }

        private static MarkerType ReadMarkerType(Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            ushort marker = (ushort)(buffer[0] << 8 | buffer[1]);
            bool isValid = Enum.IsDefined(typeof(MarkerType), marker);
            if (!isValid)
            {
                throw new ArgumentException("valid marker value expected, instead got: " + marker);
            }
            return (MarkerType)marker;
        }

        public int WriteMarkerSegment(Stream stream)
        {            
            if(_markerBody == null || _markerLength == 0)
            {
                ForceGeneration();
            }

            stream.WriteUInt16((ushort)Type);
            stream.WriteUInt16(_markerLength);
            stream.Write(_markerBody, 0, _markerLength - 2);
            return _markerLength + 2;
        }

        public virtual byte[] GenerateMarkerBody()
        {
            if(_markerBody == null)
            {
                throw new InvalidOperationException(
                    "No body segment for generic MarkerSegment of type: "
                    + Type);
            }
            return _markerBody;
        }

        /// <summary>
        /// Write the 2-bytes representations of the marker to the stream
        /// </summary>
        /// <param name="markerType"></param>
        /// <param name="stream"></param>
        /// <returns>the number of bytes written (2 bytes) </returns>
        public static int WriteMarker(MarkerType markerType, Stream stream)
        {
            stream.WriteUInt16((ushort)markerType);
            return 2;
        }
    }
}
