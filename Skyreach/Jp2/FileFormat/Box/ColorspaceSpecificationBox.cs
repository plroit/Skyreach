using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat.Box
{
    public class ColorspaceSpecificationBox : Jp2Box
    {
        public ColorspaceSpecificationBox(ColorSpace colorspace)
            : base((uint) BoxTypes.ColorSpecBox, 15L, 8)
        {
            Method = MethodOfSpecification.EnumeratedColorSpace;
            Precedense = 0;
            Approximation = 0;
            Colorspace = colorspace;
        }

        /// <summary>
        /// Method of color-space specification.
        /// </summary>
        public MethodOfSpecification Method { get; protected set; }

        /// <summary>
        /// This field is reserved for ISO use
        /// </summary>
        public byte Precedense { get; protected set; }

        /// <summary>
        /// his field specifies the extent to which this color specification
        /// method approximates the "correct" definition of the color-space.
        /// </summary>
        public byte Approximation { get; protected set; }

        public ColorSpace Colorspace { get; protected set; }

        public override byte[] GenerateBoxContent()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteUInt8((byte)Method);
            mem.WriteUInt8(Precedense);
            mem.WriteUInt8(Approximation);
            mem.WriteUInt32((uint)Colorspace);
            return mem.ToArray();
        }
    }

    /// <summary>
    /// Method of color-space specification.
    /// </summary>
    public enum MethodOfSpecification : byte
    {
        EnumeratedColorSpace = 1,
        RestrictedIccProfile = 2
    }
}
