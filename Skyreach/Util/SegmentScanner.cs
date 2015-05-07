using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Util
{
    public class SegmentScanner
    {

        /// <summary>
        /// Enumeration of lengths on a zero based line
        /// </summary>
        private readonly IEnumerable<int> _segmentLengths;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="segmentLengths">
        /// List of segment lengths.
        /// </param>
        public SegmentScanner(IEnumerable<int> segmentLengths)
        {
            _segmentLengths = segmentLengths;
        }

        /// <summary>
        /// Finds the specified point location in the list
        /// of segments. Location is strictly non-negative.
        /// </summary>
        /// <param name="loc"></param>
        /// <returns>
        /// The segment index that this point resides in
        /// and the index of the point inside the segment.
        /// Returns null if the point location is outside of
        /// the segment index.
        /// </returns>
        public SegmentLocation Find(int loc)
        {
            if(loc < 0)
            {
                throw new ArgumentException(String.Concat(
                    "Cannot find a negative location, works only ",
                    "on zero-based locations"));
            }

            IEnumerator<int> iter = _segmentLengths.GetEnumerator();
            for (int segIdx = 0, segStart = 0; iter.MoveNext(); segIdx++ )
            {
                int segEnd = segStart + iter.Current;
                if (loc < segEnd)
                {
                    return new SegmentLocation(segIdx, loc - segStart);
                }

                // advance for next iteration
                segStart = segEnd;
            }
            return null;
        }
    }

    public class SegmentLocation
    {
        public int SegmentIdx { get; private set; }
        public int InSegmentIdx { get; private set; }

        public SegmentLocation(int segIdx, int inSegIdx)
        {
            SegmentIdx = segIdx;
            InSegmentIdx = inSegIdx;
        }
    }
}
