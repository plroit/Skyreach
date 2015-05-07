
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
    /// File type box for a file that conforms to file format specifications
    /// as described in ISO 15443 Part 1, Annex I: JP2 file format syntax 
    /// </summary>
    public class FileTypeBox : Jp2Box
    {

        protected List<uint> _compatibilities;

        public FileTypeBox()
            : base((uint)BoxTypes.FileTypeBox, 20L, 8)
        {
            _compatibilities = new List<uint>();
            AddCompatbility(JP2_BRAND);
        }

        /// <summary>
        /// <para>ASCII for 'jp2 '</para>
        /// Used for major version literal and compatibility list
        /// </summary>
        public const uint JP2_BRAND = 0x6A703220;

        /// <summary>
        /// Brand or Major version number for this JPEG2000 File Format
        /// defaults to: 'jp2\040' 
        /// </summary>
        public virtual uint MajorVersion
        {
            get
            {
                return JP2_BRAND;
            }
        }

        /// <summary>
        /// Minor version of this file format
        /// </summary>
        public virtual uint MinorVersion
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// Adds a compatibility list item to specification.
        /// Default CL is included when object is constructed
        /// </summary>
        /// <param name="cl"></param>
        public void AddCompatbility(uint cl)
        {
            _compatibilities.Add(cl);
        }

        public override byte[] GenerateBoxContent()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteUInt32(MajorVersion);
            mem.WriteUInt32(MinorVersion);
            foreach(uint cl in _compatibilities)
            {
                mem.WriteUInt32(cl);
            }
            return mem.ToArray();
        }
    }
}
