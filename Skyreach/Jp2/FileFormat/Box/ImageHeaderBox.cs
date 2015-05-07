using Skyreach.Jp2.Codestream.Markers;
using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat.Box
{
    public class ImageHeaderBox : Jp2Box
    {
        public const int JP2_COMPRESSION_TYPE = 7;

        /// <summary>
        /// If the components vary in bit depth and/or sign, then the value of 
        /// the BitPerComponent field shall be 255 and the JP2 Header box shall 
        /// also contain a Bits Per Component box defining the bit depth of 
        /// each component 
        /// </summary>
        public const byte USE_BPC_BOX = (byte)0xFF;

        public ImageHeaderBox(SizMarker siz, bool hasPropertyRights)
            : base((uint)BoxTypes.ImageHeaderBox, 22L, 8)
        {
            ImageWidth = (uint) (siz.RefGridSize.Width - siz.ImageOffset.X);
            ImageHeight = (uint) (siz.RefGridSize.Height - siz.ImageOffset.Y);
            Components = siz.Components;
            byte firstPrecision = siz.Precisions.First();

            bool samePrecision = siz.Precisions
                .All(prec => prec == firstPrecision); 
            BitsPerComponent = samePrecision ? firstPrecision : USE_BPC_BOX;
            UnknownColorspace = 0; // it is known!
            IntellectualPropertyRights = (byte) (hasPropertyRights ? 1 : 0);
        }

        /// <summary>
        /// Width of the image area. Xsiz – XOsiz
        /// </summary>
        public uint ImageWidth { get; protected set; }

        /// <summary>
        /// Height of the image area. Ysiz – YOsiz
        /// </summary>
        public uint ImageHeight { get; protected set; }

        /// <summary>
        /// number of components in the codestream.
        /// The value of this field shall be equal to the value of the
        /// Csiz field in the SIZ marker in the codestream. 
        /// </summary>
        public ushort Components { get; protected set; }

        /// <summary>
        /// Bit depth of the components in the codestream, minus 1, and is 
        /// stored as a 1-byte field.
        /// <para>
        /// If the bit depth and the sign are the same for all components, 
        /// then this parameter specifies that bit depth and shall be 
        /// equivalent to the values of the Ssiz fields in the SIZ marker in 
        /// the codestream (which shall all be equal). If the components vary 
        /// in bit depth and/or sign, then the value of this field shall be 
        /// 255 and the JP2 Header box shall also contain a Bits Per Component 
        /// box defining the bit depth of each component 
        /// </para>
        /// </summary>
        public byte BitsPerComponent { get; protected set; }

        /// <summary>
        /// Specifies the compression algorithm used to compress the image data
        /// </summary>
        public virtual byte CompressionType { get { return JP2_COMPRESSION_TYPE; } }

        /// <summary>
        ///  specifies if the actual color-space of the image data in the 
        ///  codestream is known. Boolean 0 or 1; 
        /// </summary>
        public byte UnknownColorspace { get; set; }

        /// <summary>
        ///  indicates whether this JP2 file contains intellectual property 
        ///  rights information. Boolean 0 or 1;
        /// </summary>
        public byte IntellectualPropertyRights { get; protected set; }

        public override byte[] GenerateBoxContent()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteUInt32(ImageHeight);
            mem.WriteUInt32(ImageWidth);
            mem.WriteUInt16(Components);
            mem.WriteUInt8(BitsPerComponent);
            mem.WriteUInt8(CompressionType);
            mem.WriteUInt8(UnknownColorspace);
            mem.WriteUInt8(IntellectualPropertyRights);
            return mem.ToArray();

        }


    }
}
