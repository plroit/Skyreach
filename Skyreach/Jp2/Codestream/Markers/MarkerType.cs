using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream.Markers
{
    public enum MarkerType : ushort
    {
        // Delimiting

        /// <summary> Start Of Codestream </summary>
        SOC = 0xFF4F,

        /// <summary> Start Of Tile-part </summary>
        SOT = 0xFF90,

        /// <summary> Start Of Data </summary>
        SOD = 0xFF93,

        /// <summary> End Of Codestream </summary>
        EOC = 0xFFD9,

        // Fixed information

        /// <summary> Image and tile size </summary>
        SIZ = 0xFF51,

        // Functional

        /// <summary> Coding style, Default </summary>
        COD = 0xFF52,

        /// <summary> Coding style, Component </summary>
        COC = 0xFF53,

        /// <summary> Region of interest </summary>
        RGN = 0xFF53,

        /// <summary> Quantization, Default </summary>
        QCD = 0xFF5C,

        /// <summary> Quantization, Component </summary>
        QCC = 0xFF5D,

        /// <summary> Progression Order Change </summary>
        POC = 0xFF5F,

        // Pointer

        /// <summary> Tile-part Length, Main header </summary>
        TLM = 0xFF55,

        /// <summary> Packet Length, Main header </summary>
        PLM = 0xFF57, 

        /// <summary> Packet Length, Tile-part header </summary>
        PLT = 0xFF58,
 
        /// <summary> Packed Packet headers, Main header </summary>
        PPM = 0xFF60,

        /// <summary> Packed Packet headers, Tile-part header </summary>
        PPT = 0xFF61,

        // In bit-stream

        /// <summary> Start of packet </summary>
        SOP = 0xFF91,

        /// <summary> End of packet header </summary>
        EPH = 0xFF92,

        // Informational

        /// <summary> Component registration </summary>
        CRG = 0xFF63,

        /// <summary> Comment </summary>
        COM = 0xFF64,
    }
}
