using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream
{
    public abstract class CodestreamNode : CodestreamElement
    {

        public const long OFFSET_NOT_SET = -1;

        /// <summary>
        /// true iff the codestream node was opened and
        /// parsed from the underlying IO stream,
        /// or if the codestream node had been recently 
        /// created from scratch and was not yet closed.
        /// </summary>
        public bool IsOpened { get; protected set; }

        /// <summary>
        /// true if this codestream node has a 
        /// backup in the underlying IO stream
        /// and can be safely closed and re-opened.
        /// True for nodes that have been parsed
        /// from an underlying IO stream, or for nodes
        /// that have been created from scratch but already
        /// flushed to the underlying IO stream.
        /// </summary>
        public bool IsFlushed { get; protected set; }

        protected internal CodestreamNode(
            CodestreamNode parent, 
            long offset, 
            long length)
            : base(parent, offset, length)
        {
            // created from an underlying stream
            IsFlushed = true;
        }

        protected internal CodestreamNode(CodestreamNode parent)
            : base(parent, OFFSET_NOT_SET, -1)
        {
            // created from scratch
            IsOpened = true;
        }

        public bool IsRoot
        {
            get
            {
                return Parent == null;
            }
        }


        /// <summary>
        /// The underlying IO stream that this node is based upon.
        /// The root codestream node should keep the
        /// real reference to this object.
        /// 
        /// All descendants should use the root node stream
        /// 
        /// May be null if root codestream is created but not yet written
        /// to any underlying stream
        /// </summary>
        internal protected virtual Stream UnderlyingIO
        {
            get
            {
                if (!IsRoot)
                {
                    return Parent.UnderlyingIO;
                }
                throw new NotSupportedException(
                    "root node does not have an opened IO stream");
            }
            protected set
            {
                // override property in JP2Codestream, this way only
                // a real codestream will have allocated memory for
                // a reference to a stream.
                // since there are millions (sometimes more) packets 
                // (CodestreamNodes) in a codestream, this is useful.
                throw new NotSupportedException(
                    "Cannot reset underlying IO stream  in non-root node");
            }
        }

        /// <summary>
        /// Offset in bytes from the beginning of this node
        /// to the first byte of the child node.
        /// 
        /// ex. offset of the first tile-part from the codestream
        /// or offset of the first packet from the tile-part
        /// </summary>
        public abstract long FirstChildOffset { get; }

        /// <summary>
        /// Reads the marker segments from the underlying IO that correspond
        /// to the headers of this CodestreamNode. Parses every marker segment 
        /// into meaningful properties.
        /// 
        /// Establishes basic Offset and Length properties of its child nodes,
        /// using any indexing elements that appear in the marker segments,
        /// such as PLT or TLM segments.
        /// 
        /// Each CodestreamNode must be opened to access its internal 
        /// properties and the basic positioning properties
        /// of its direct children.
        /// </summary>
        /// <returns>
        /// a reference to itself for fluency and convenience
        /// </returns>
        internal abstract CodestreamNode Open();
        /// <summary>
        /// Returns an iterator that iterates over
        /// the children of this CodestreamNode and opens them.
        /// Accesses the UnderlyingIO to read and parse properties 
        /// of the child nodes.
        /// </summary>
        /// <returns>
        /// An enumerable over this node's children
        /// </returns>
        public abstract IEnumerable<CodestreamElement> OpenChildren();

        /// <summary>
        /// Flushes this node headers to the underlying IO stream,
        /// performing necessary finalizing steps, such as child index
        /// generation and total length calculation
        /// </summary>
        public abstract void Flush();

    }
}
