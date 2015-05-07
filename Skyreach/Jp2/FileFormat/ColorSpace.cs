using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.FileFormat
{
    public enum ColorSpace : uint
    {
        /// <summary>
        /// sRGB as defined by IEC 61966-2-1 
        /// </summary>
        sRGB = 16,

        /// <summary>
        /// Grey-scale where image luminance is related to code values
        /// using the sRGB non-linearity 
        /// </summary>
        Greyscale = 17,

        /// <summary>
        /// sYCC as defined by IEC 61966-2-1 Amd. 1 
        /// </summary>
        sYCC = 18
    }
}
