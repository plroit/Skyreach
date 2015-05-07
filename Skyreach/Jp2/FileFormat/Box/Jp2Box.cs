using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat.Box
{
    /// <summary>
    /// A JPEG2000 Box, from ISO 15443-1, Annex I JP2 File Format Syntax:
    /// The building-block of the JP2 file format
    /// The binary structure of a file is a contiguous sequence of boxes.
    /// All information contained within the JP2 file is encapsulated in boxes. 
    /// Some of those boxes are independent, and some of those boxes contain 
    /// other boxes. 
    /// </summary>
    public class Jp2Box
    {

        // The empty box is a box with only type and length fields
        protected const int MIN_BOX_LENGTH = 8;

        protected const int USE_EXTENDED_LENGTH = 1;

        /// <summary>
        /// The 4 byte type code of the box, BoxType is an open enumeration
        /// and may be extended beyond part-1 of the standard.
        /// Explicitly do not use the BoxTypes enumeration
        /// 
        /// Specific implementation of Jp2Box may replace this value
        /// with the BoxTypes Enumeration
        /// </summary>
        public virtual uint TypeCode { get; protected set; }

        /// <summary>
        /// The BoxTypes enumeration value that is specified in
        /// ISO15443-1. For common scenarios, UnDefined if not present
        /// </summary>
        public virtual BoxTypes Type { get; protected set; }

        /// <summary>
        /// Returns the length in bytes of the content of this box
        /// </summary>
        public long ContentLength
        {
            get
            {
                return Length - DataOffset;
            }
        }

        /// <summary>
        /// A collection of child boxes that are contained within this box.
        /// </summary>
        public IReadOnlyCollection<Jp2Box> Boxes 
        { 
            get 
            { 
                return _boxes.AsReadOnly(); 
            } 
        }

        /// <summary>
        /// The absolute position of this box in the underlying IO stream
        /// </summary>
        //public long Position { get; protected set; }

        /// <summary>
        /// The offset of the first content byte in this box
        /// </summary>
        public uint DataOffset { get; protected set; }

        /// <summary>
        /// <para>
        /// The length in bytes of this box including the type and 
        /// length fields. Same as advertised in the underlying IO stream
        /// </para>
        /// ISO 15443-1 defines Length to be unsigned 64 bits,
        /// but CLR supports positioning only on 63 bit ranges
        /// </summary>
        public long Length { get; protected set; }

        private List<Jp2Box> _boxes;

        protected Jp2Box(uint code, long length, uint dataOffset)
            : this(code)
        {
            Length = length;
            DataOffset = dataOffset;
        }

        protected Jp2Box(
            uint code, 
            long length, 
            uint dataOffset, 
            IEnumerable<Jp2Box> boxes)
            : this(code, length, dataOffset)
        {
            _boxes.AddRange(boxes);
        }

        public Jp2Box(uint code)
        {
            TypeCode = code;
            Type = Translate(code);
            Length = MIN_BOX_LENGTH;
            DataOffset = MIN_BOX_LENGTH;
            _boxes = new List<Jp2Box>();
        }

        /// <summary>
        /// Generates the body of the JPEG2000 box.
        /// Should not be used for the Contiguous Codestream box
        /// The codestream should be incrementally generated.
        /// </summary>
        /// <returns></returns>
        public virtual byte[] GenerateBoxContent()
        {
            return new byte[0];
        }

        /// <summary>
        /// <para>
        /// Parses a JPEG2000 box from the underlying IO stream.
        /// </para>
        /// <para>
        /// Stream must be positioned at the start of the box
        /// </para>
        /// <para>
        /// Advances the stream to the first byte of the box content
        /// </para>
        /// </summary>
        /// <param name="stream">the underlying IO stream</param>
        /// <param name="boxLimit">
        /// number of bytes that are left to be read in the box container
        /// </param>
        /// <returns></returns>
        public static Jp2Box Open(Stream stream, long boxLimit)
        {
            if(boxLimit < MIN_BOX_LENGTH)
            {
                throw new ArgumentException(
                    "Not enough data left to parse JPEG2000 box");
            }

            long length = stream.ReadUInt32();

            if(length == 0)
            {
                // ISO 15443-1, Annex I section 4 Box Definition
                //  If the value of this field is 0, then the length of the 
                // box was not known when the LBox field was written. In this 
                // case, this box contains all bytes up to the end of the file.
                length = boxLimit;
            }
            else if(length > USE_EXTENDED_LENGTH && length < MIN_BOX_LENGTH)
            {
                // these values are supposedly reserved for ISO use..
                throw new ArgumentException(
                    "JPEG2000 box length cannot be determined");
            }

            uint ucode = stream.ReadUInt32();
            uint dataOffset = 8;

            if(length == USE_EXTENDED_LENGTH)
            {
                if(boxLimit < (MIN_BOX_LENGTH + 8))
                {
                    throw new ArgumentException(
                        "not enough data to read extended length field");
                }
                ulong ulLength = stream.ReadUInt64();
                if (ulLength > long.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        "box is larger than 2^63 bytes");
                }

                length = (long)ulLength;
                dataOffset += 8;
            }
            if(length > boxLimit)
            {
                throw new ArgumentException(
                    "Box length is larger than the limit on box container");
            }

            if(Jp2Box.IsSuperbox(ucode))
            {
                var boxes = TraverseBoxes(stream, length - dataOffset);
                return new Jp2Box(ucode, length, dataOffset, boxes);
            }
            else
            {
                // advance the stream position beyond the content of this box.
                // TraverseBoxes recursive call relies on Open call to 
                // advance the stream position.
                stream.Seek(length - dataOffset, SeekOrigin.Current);
                return new Jp2Box(ucode, length, dataOffset);
            }
        }

        private static BoxTypes Translate(uint code)
        {
            return Enum.IsDefined(typeof(BoxTypes), code) ?
                (BoxTypes)code : BoxTypes.UnDefined;
        }


        /// <summary>
        /// Returns true iff the current box type is defined as a superbox
        /// </summary>
        /// <param name="boxType"></param>
        /// <returns></returns>
        public static bool IsSuperbox(uint boxType)
        {
            switch (boxType)
            {
                case (uint)BoxTypes.JP2HeaderBox:
                case (uint)BoxTypes.ResolutionBox:
                case (uint)BoxTypes.UuidInfoBox:
                    return true;
                default:
                    return false;
            }
        }

        public void Add(IEnumerable<Jp2Box> childBoxes)
        {
            long sumLengths = childBoxes.Aggregate(
                0L,
                (tempSum, box) => box.Length + tempSum);
            Length += sumLengths;
            _boxes.AddRange(childBoxes);
        }

        public void WriteBoxHeaders(Stream underlyingStream)
        {
            byte[] content = GenerateBoxContent();
            uint len = Length <= uint.MaxValue ? (uint) Length : 1;
            underlyingStream.WriteUInt32(len);
            underlyingStream.WriteUInt32(TypeCode);
            if(len == 1)
            {
                underlyingStream.WriteUInt64((ulong)Length);
            }
        }

        /// <summary>
        /// Writes entire box to the underlying IO stream.
        /// If the box is a superbox, it writes its children as well
        /// <para>
        /// Advances the stream position to the first byte after the content 
        /// of this box.
        /// </para>
        /// </summary>
        /// <param name="underlyingStream"></param>
        public void WriteBox(Stream underlyingStream)
        {
            // Stream.Write takes only Int32 as count parameter
            // if box is more than 4GB long, you should have written it 
            // incrementally anyway
            if (Length > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    @"box content is more than 2^31 bytes, 
                    generate incrementally");
            }
            
            WriteBoxHeaders(underlyingStream);

            if(_boxes.Any())
            {
                // superbox, call recursively to all children
                // to write themselves to stream
                foreach(var box in _boxes)
                {
                    box.WriteBox(underlyingStream);
                }
            }
            else
            {
                // its a leaf box. call generate content
                byte[] content = GenerateBoxContent();
                if(Length > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        "box is larger than 2^31 bytes. use buffers");
                }
                underlyingStream.Write(content, 0, (int)ContentLength);
            }
        }

        /// <summary>
        /// <para>
        /// Traverses a contiguous sequence of JPEG2000 boxes.
        /// Visits recursively the contiguous sequence of boxes of 
        /// every superbox.
        /// </para>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="boxLimit"></param>
        /// <returns>
        /// An ordered collection representing the sequence 
        /// of boxes that appear in the underlying IO stream
        /// </returns>
        public static IEnumerable<Jp2Box> TraverseBoxes(Stream stream, long boxLimit)
        {
            List<Jp2Box> boxes = new List<Jp2Box>();
            long bytesLeft = boxLimit;
            while (bytesLeft > 0)
            {
                Jp2Box box = Jp2Box.Open(stream, bytesLeft);
                bytesLeft -= box.Length;
                boxes.Add(box);
            }

            return boxes;
        }
    }

}
