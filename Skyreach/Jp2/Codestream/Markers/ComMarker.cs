using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skyreach.Util.Streams;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class ComMarker : MarkerSegment
    {
        public const int LATIN1_CODEPAGE = 1252;

        public const int LATIN1_REGISTRATION = 1;

        public ComMarker(string latin1Comment)
            :base(MarkerType.COM)
        {
            Comment = latin1Comment;
            Registration = LATIN1_REGISTRATION;
            _markerBody = GenerateMarkerBody();
            _markerLength = (ushort)_markerBody.Length;
            _markerLength += (ushort) MarkerSegment.MarkerLength;
        }

        public ComMarker(
            ushort markerLength, 
            byte[] markerBody)
            : base(MarkerType.COM, markerLength, markerBody)
        {
            Parse();
        }

        public String Comment { get; private set; }

        public ushort Registration { get; private set; }

        protected override void Parse()
        {
            MemoryStream mem = new MemoryStream(_markerBody);

            Registration = mem.ReadUInt16();
            if(Registration == LATIN1_REGISTRATION)
            {
                var enc = Encoding.GetEncoding(LATIN1_CODEPAGE);
                Comment = enc.GetString(_markerBody, 1, _markerLength - 3);
            }
            else
            {
                throw new NotImplementedException(string.Format
                    ("Encoding registration: {0}", Registration));
            }
        }

        protected override byte[] GenerateMarkerBody()
        {
            var mem = new MemoryStream();
            mem.WriteUInt16(Registration);
            Encoding enc = Encoding.GetEncoding(LATIN1_CODEPAGE);
            var bytes = enc.GetBytes(Comment);
            mem.Write(bytes, 0, bytes.Length);
            return mem.ToArray();
        }
    }
}
