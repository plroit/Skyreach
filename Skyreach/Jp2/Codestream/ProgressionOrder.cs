using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2
{
    public enum ProgressionOrder : byte
    {
        /// <summary>
        /// Layer-resolution level-component-position progression 
        /// </summary>
        LRCP = 0,

        /// <summary>
        /// Resolution level-layer-component-position progression 
        /// </summary>
        RLCP = 1,

        /// <summary>
        /// Resolution level-position-component-layer progression 
        /// </summary>
        RPCL = 2,

        /// <summary>
        /// Position-component-resolution level-layer progression
        /// </summary>
        PCRL = 3,

        /// <summary>
        /// Component-position-resolution level-layer progression 
        /// </summary>
        CPRL = 4
    }
}
