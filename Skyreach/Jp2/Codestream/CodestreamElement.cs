using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream
{
    /// <summary>
    /// Codestream contiguous element. Elements appear in a tree
    /// where the root node is the Codestream node.
    /// Elements have contiguous range in a JPEG2000 file starting at
    /// an offset from their parents.
    /// </summary>
    public abstract class CodestreamElement
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent">
        ///     The parent element or null if its the JP2Codestream
        ///     The parent of a tile part is the codestream it belongs to
        ///     the parent of packet is the tile-part that it belongs to
        /// </param>
        /// <param name="offset">
        ///     The offset in bytes of this element from
        ///     its parent element
        /// </param>
        /// <param name="length"></param>
        public CodestreamElement(CodestreamNode parent, long offset, long length)
        {
            this._offset = offset;
            this.Parent = parent;
            this.Length = length;
        }

        public CodestreamNode Parent { get; private set; }

        /// <summary>
        /// The absolute position in bytes of this element from the beginning 
        /// of the JP2File
        /// </summary>
        public long Position 
        { 
            get
            {
                return _offset + (Parent != null ? Parent.Position : 0);
            }
        }

        /// <summary>
        /// The length in bytes of this element
        /// from the first byte to the first byte of the 
        /// sibling element.
        /// </summary>
        public long Length { get; protected set; }

        /// <summary>
        /// The offset in bytes of this CodestreamNode from the first byte
        /// of its parent
        /// </summary>
        protected long _offset;

    }
}
