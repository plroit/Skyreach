using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Jp2.Codestream;
using Skyreach.Jp2.FileFormat.Box;
using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat
{
    /// <summary>
    /// Represents a JPEG2000 file,
    /// A concatenated list of JPEG2000 boxes.
    /// Must start with the JPEG2000 signature box
    /// and must contain a JPEG2000 codestream box
    /// </summary>
    public class Jp2File
    {
        /// <summary>
        /// True iff the file is a raw codestream.
        /// File is a JPEG2000 codestream, not wrapped within
        /// JPEG2000 boxes
        /// </summary>
        protected readonly bool _isRaw;

        /// <summary>
        /// The underlying stream, must be support seeking
        /// </summary>
        private readonly Stream _stream;

        /// <summary>
        /// The contiguous sequence of boxes that represent this 
        /// JPEG2000 file.
        /// </summary>
        protected IEnumerable<Jp2Box> _boxes;

        /// <summary>
        /// The JPEG2000 codestream. The actual data that 
        /// represents an image encoded with the JPEG2000 compression
        /// </summary>
        private JP2Codestream Codestream { get; set; }

        internal long GetCodestreamOffset()
        {
            long offset = 0;
            // find codestream box, add lengths till you find it
            foreach(var box in _boxes)
            {
                if(box.Type == BoxTypes.CodestreamBox)
                {
                    return offset + box.DataOffset;
                }
                if(box.Length > long.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        "Codestream box is beyond 2^63 bytes");
                }
                offset += (long) box.Length;
            }
            throw new InvalidOperationException(
                "no codestream box found");
        }

        /// <summary>
        /// Private constructor, open JP2File objects using the static 
        /// factory methods Open or Create
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="isRaw"></param>
        protected Jp2File(Stream stream, IEnumerable<Jp2Box> boxes)
        {
            _stream = stream;
            _boxes = boxes;
            _isRaw = false;
            var csBoxes = 
                boxes.Where(box => box.Type == BoxTypes.CodestreamBox);

            if(!csBoxes.Any())
            {
                throw new InvalidOperationException(
                    "Must create JP2File from a codestream box");
            }
            var csBox = csBoxes.First() as ContiguousCodestreamBox;
            long csOffset = GetCodestreamOffset();
            csBox.Codestream.Bind(_stream, csOffset);
            Codestream = csBox.Codestream;
        }

        protected Jp2File(Stream stream)
        {
            _stream = stream;
            ushort maybeMarker = stream.ReadUInt16();
            _isRaw = maybeMarker == (ushort)MarkerType.SOC;
            stream.Seek(-2, SeekOrigin.Current); // reset
        }

        /// <summary>
        /// Opens and creates a JPEG2000 image from the first codestream in the file
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Jp2File Open(Stream stream)
        {
            ThrowIfInvalidJPEG2000(stream);     
            return (new Jp2File(stream)).Open();
        }

        /// <summary>
        /// Parses the JPEG2000 file by traversing the boxes from the 
        /// underlying IO stream. 
        /// </summary>
        /// <returns> A reference to itself for convenience</returns>
        protected Jp2File Open()
        {
            if (_isRaw)
            {
                Codestream = new JP2Codestream(_stream, 0, _stream.Length);
                return this;
            }

            _boxes = Jp2Box.TraverseBoxes(_stream, _stream.Length).ToList();

            long boxPosition = GetCodestreamOffset();
            if(boxPosition < 0)
            {
                throw new ArgumentException(
                    "File does not contain JPEG2000 codestream box");
            }

            var ccBox = _boxes.First(box => box.Type == BoxTypes.CodestreamBox);
            Codestream = new JP2Codestream(
                _stream, 
                boxPosition, 
                (long) ccBox.ContentLength);
            return this;
        }

        /// <summary>
        /// Creates from scratch a JPEG2000 conforming file
        /// with a JPEG2000 codestream skeleton. 
        /// 
        /// </summary>
        /// <param name="siz"></param>
        /// <param name="cod"></param>
        /// <param name="qcd"></param>
        /// <param name="reservedTileparts"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Jp2File Create(
            IEnumerable<MarkerSegment> markers,
            int reservedTileparts,
            bool hasPropertyRights,
            Stream stream)
        {

            var sizs = markers.Where(mrk => mrk.Type == MarkerType.SIZ);
            var cods = markers.Where(mrk => mrk.Type == MarkerType.SIZ);
            var qcds = markers.Where(mrk => mrk.Type == MarkerType.QCD);
            if(!sizs.Any() || !cods.Any() || !qcds.Any())
            {
                throw new ArgumentException(
                    "Must supply SIZ, COD and QCD markers");
            }
            SizMarker siz = sizs.First() as SizMarker;
            CodMarker cod = cods.First() as CodMarker;
            QcdMarker qcd = qcds.First() as QcdMarker;

            ColorSpace colorspace = GetColorspace(siz);
            Jp2Box signBox = new Jp2SignatureBox();
            Jp2Box ftypBox = new FileTypeBox();
            Jp2Box jp2hBox = new Jp2Box((uint)BoxTypes.JP2HeaderBox);
            ImageHeaderBox ihdrBox = new ImageHeaderBox(siz, hasPropertyRights);
            Jp2Box colrBox = new ColorspaceSpecificationBox(colorspace);
            var codestream = new JP2Codestream(markers, reservedTileparts);
            var csBox = new ContiguousCodestreamBox(codestream);
            if(ihdrBox.BitsPerComponent == ImageHeaderBox.USE_BPC_BOX)
            {
                throw new NotSupportedException(
                    "Create image with bit per component specification box");
            }

            jp2hBox.Add(new List<Jp2Box> { ihdrBox, colrBox });
            Jp2File jp2File = new Jp2File(stream, new List<Jp2Box> { 
                signBox, ftypBox, jp2hBox, csBox});
            return jp2File;
        }

        /// <summary>
        /// Choose between sRGB and Grey-scale. 
        /// Implement something smarter when needed
        /// </summary>
        /// <param name="imageProperties"></param>
        /// <returns></returns>
        private static ColorSpace GetColorspace(SizMarker imageProperties)
        {
            switch(imageProperties.Components)
            {
                case 1: return ColorSpace.Greyscale;
                case 3: return ColorSpace.sRGB;
                default: throw new NotSupportedException("color-space");
            }
        }

        /// <summary>
        /// Parses and returns the first codestream from the JPEG2000 file
        /// </summary>
        /// <returns>the opened codestream</returns>
        public JP2Codestream OpenCodestream() 
        {
            return Codestream.Open() as JP2Codestream;

        }

        /// <summary>
        /// Check for either FF4F Start-Of-Codestream marker at the 
        /// beginning of the stream, or the JP2 Signature Box 'jP  '
        /// </summary>
        /// <param name="stream"></param>
        private static void ThrowIfInvalidJPEG2000(Stream stream)
        {
            ushort val = stream.ReadUInt16();
            bool isCodestream = val == (ushort)MarkerType.SOC;
            stream.Seek(-2, SeekOrigin.Current); // reset
            bool isValid = isCodestream;
            if (!isValid)
            {
                bool isJp2 = true;
                uint[] header = new uint[3];
                int hIdx = 0;
                header[hIdx++] = stream.ReadUInt32();
                header[hIdx++] = stream.ReadUInt32();
                header[hIdx++] = stream.ReadUInt32();

                isJp2 = isJp2 && header[0] == 0x000C; // sign box length
                isJp2 = isJp2 && header[1] == (uint)BoxTypes.Jp2SignatureBox;
                isJp2 = isJp2 && header[2] == 0x0D0A870A; // <CR><LF><0x87><LF>
                stream.Seek(-12, SeekOrigin.Current); // reset
                isValid = isJp2;
            }
            // must be either a raw codestream or a jpeg2000 file format
            if (!isValid)
            {
                throw new ArgumentException(
                    "invalid jpeg2000 file or codestream");
            }
        }

        /// <summary>
        /// Writes the Headers of the JPEG2000 File 
        /// to the underlying stream. The headers of the JPEG2000 codestream
        /// are not written.
        /// </summary>
        public void Flush()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            foreach(var box in _boxes)
            {
                if(box.Type != BoxTypes.CodestreamBox)
                {
                    box.WriteBox(_stream);
                }
                else
                {
                    // the contiguous codestream box shall be the last
                    // box in the file, and its length will be zero
                    box.WriteBoxHeaders(_stream);
                    break;
                }
            }

            Codestream.Flush();
        }
    }
}
