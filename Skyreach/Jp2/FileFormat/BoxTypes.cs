using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat
{
    /// <summary>
    /// Represents different box types in a JPEG2000 file format
    /// </summary>
    public enum BoxTypes : uint
    {
        /// <summary>
        /// JPEG2000 Signature. The first box in a JPEG2000 file.
        /// </summary>
        Jp2SignatureBox = 0x6A502020,

        FileTypeBox = 0x66747970,
        JP2HeaderBox = 0x6A703268,
        ImageHeaderBox = 0x69686472,
        BitsPerCompBox = 0x62706363,
        ColorSpecBox = 0x636F6C72,
        PalleteBox = 0x70636C72,
        CompMapBox = 0x636D6170,
        ChannelDefBox = 0x63646566,
        ResolutionBox = 0x72657320,
        CaptureResBox = 0x72657363,
        DisplayResBox = 0x72657364,
        CodestreamBox = 0x6A703263,
        IntellectPropRightsBox = 0x6A703269,
        XmlBox = 0x786D6C20,
        UuidBox = 0x75756964,
        UuidInfoBox = 0x75696E66,
        UuidListBox = 0x75637374,
        UrlBox = 0x75726C20,

        UnDefined = 0x0
    }
}
