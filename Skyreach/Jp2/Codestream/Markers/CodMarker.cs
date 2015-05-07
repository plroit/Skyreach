using Skyreach.Util;
using Skyreach.Util.Streams;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Jp2.Codestream.Markers
{
    public class CodMarker : MarkerSegment
    {

        public const int MAX_CBLK_SIZE = 1 << 12; // 4096 samples in codeblock
        protected internal CodMarker(ushort markerLength, byte[] markerBody)
            : base(MarkerType.COD, markerLength, markerBody)
        {
            Parse();
        }

        public CodMarker(
            bool usePrecincts,
            bool useEph,
            bool useSop, 
            Size codeblockSize, 
            Size[] precinctSizes, 
            ProgressionOrder progression,
            ushort qualityLayers,
            bool useMultiComponentTransform,
            WaveletTransform waveletFilter,
            byte decompositionLevels,
            CodeblockStyle cblkStyle)
            : base(MarkerType.COD)
        {
            _scod = CodingStyle.None;
            _scod |= 
                usePrecincts ? CodingStyle.UsePrecincts : CodingStyle.None;
            _scod |=
                useSop ? CodingStyle.UseSopMarker : CodingStyle.None;
            _scod |=
                useEph ? CodingStyle.UseEphMarker: CodingStyle.None;

            Progression = progression;
            QualityLayers = qualityLayers;
            DecompositionLevels = decompositionLevels;
            UseMultipleComponentTransform = useMultiComponentTransform;
            CBlkStyle = cblkStyle;
            WaveletFilter = waveletFilter;

            // width > max || height > max
            if((new List<int>{ 
                codeblockSize.Width, 
                codeblockSize.Height, 
                MAX_CBLK_SIZE }).Max() != MAX_CBLK_SIZE)
            {
                throw new ArgumentOutOfRangeException(
                    "Codeblock dimensions must be up to " 
                    + MAX_CBLK_SIZE + " samples on each side");
            }

            bool isCodeblockPow2 =
                BitHacks.IsPowerOf2((uint) codeblockSize.Width) &&
                BitHacks.IsPowerOf2((uint) codeblockSize.Height);
            if (!isCodeblockPow2)
            {
                throw new ArgumentException(
                    "Codeblock size must be power of 2");
            }

            // codeblock size range is [4,64], and is a power of 2.
            // to save space and fill it inside a byte, 
            // codestream specifies them using the 2-exponent in the range
            // [0, 16]. The additional 2 is implicit.
            _cblkExpnX = (byte)(BitHacks.LogFloor2((uint)codeblockSize.Width));
            _cblkExpnX -= 2;

            _cblkExpnY = (byte)(BitHacks.LogFloor2((uint)codeblockSize.Height));
            _cblkExpnY -= 2;

            if(UsePrecincts)
            {
                bool valid = precinctSizes.Any();
                valid &= precinctSizes.Length <= (decompositionLevels + 1);
                valid &= precinctSizes
                    .All(prc => BitHacks.IsPowerOf2((uint)prc.Width));
                valid &= precinctSizes
                    .All(prc => BitHacks.IsPowerOf2((uint)prc.Height));
                if(!valid)
                {
                    throw new ArgumentException(
                        "precincts unspecified or not power of two");
                }

                _ppx = new byte[decompositionLevels + 1];
                _ppx = new byte[decompositionLevels + 1];

                int idx = 0;
                foreach(var prc in precinctSizes)
                {
                    _ppx[idx] = (byte)BitHacks.LogFloor2((uint)prc.Width);
                    _ppy[idx] = (byte)BitHacks.LogFloor2((uint)prc.Height);
                    idx++;
                }

                for(; idx <= decompositionLevels; idx++)
                {
                    // there is at least one element in ppx/ppy
                    // because we checked that precSizes.Any()
                    _ppx[idx] = _ppx[idx - 1];
                    _ppy[idx] = _ppy[idx - 1];
                }
            }
            else
            {
                // maximal precincts: size 2^15
                _ppx = new byte[] {0xF};
                _ppy = new byte[] {0xF};
            }
        }

        /// <summary>
        /// Scod, as appears in the codestream
        /// </summary>
        private CodingStyle _scod;

        /// <summary>
        /// Codeblock Width Exponent, as appears in the codestream
        /// (without addition of 2)
        /// </summary>
        private byte _cblkExpnX;

        /// <summary>
        /// Codeblock Height Exponent, as appears in the codestream
        /// (without addition of 2)
        /// </summary>
        private byte _cblkExpnY;

        /// <summary>
        /// Precinct Width Exponent for each resolution level
        /// </summary>
        private byte[] _ppx;

        /// <summary>
        /// Precinct Height Exponent for each resolution level
        /// </summary>
        private byte[] _ppy;

        /// <summary>
        /// True when user specified precincts are used. 
        /// When not specified by user, precincts have PPx = 15 and PPy = 15 
        /// </summary>
        public bool UsePrecincts 
        { 
            get 
            { 
                return (_scod & CodingStyle.UsePrecincts) != 0; 
            } 
        }

        public bool UseSopMarker 
        { 
            get 
            { 
                return (_scod & CodingStyle.UseSopMarker) != 0; 
            } 
        }

        public bool UseEphMarker 
        { 
            get 
            { 
                return (_scod & CodingStyle.UseEphMarker) != 0; 
            } 
        }

        public ProgressionOrder Progression { get; private set; }

        public ushort QualityLayers { get; private set; }

        public byte DecompositionLevels { get; private set; }

        public CodeblockStyle CBlkStyle { get; private set; }

        /// <summary>
        /// When specified, component transformation is used on components 
        /// 0, 1, 2 for coding efficiency (see G.2). Irreversible 
        /// component transformation used with the 9-7 irreversible filter. 
        /// Reversible component transformation used with the 5-3 reversible 
        /// filter 
        /// </summary>
        public bool UseMultipleComponentTransform { get; private set; }

        public WaveletTransform WaveletFilter { get; private set; }

        public ushort CodeblockWidth 
        { 
            get 
            { 
                return (ushort) (1 << (_cblkExpnX + 2));
            } 
        }

        public ushort CodeblockHeight
        {
            get
            {
                return (ushort)(1 << (_cblkExpnY + 2));
            }
        }

        /// <summary>
        /// ppx,ppy. Actual partition size is [2^ppx,2^ppy]
        /// </summary>
        public IReadOnlyList<Point> PrecinctPartitions;

        protected internal override void Parse()
        {
            if(_markerLength < 12)
            {
                throw new ArgumentOutOfRangeException(
                    "COD marker must be at least 12 bytes long");
            }

            Stream mem = new MemoryStream(_markerBody);
            byte scod = mem.ReadUInt8();
            // http://msdn.microsoft.com/en-us/library/system.enum.isdefined(v=vs.110).aspx
            // If enumType is an enumeration that is defined by using the 
            // FlagsAttribute attribute, the method returns false if multiple
            // bit fields in value are set but value does not correspond to a 
            // composite enumeration value
            if(!Enum.IsDefined(typeof(CodingStyle), scod))
            {
                throw new ArgumentException(
                    "CodingStyle is undefined");
            }
            _scod = (CodingStyle) scod;

            byte progOrder = mem.ReadUInt8();
            if(!Enum.IsDefined(typeof(ProgressionOrder), progOrder))
            {
                throw new ArgumentOutOfRangeException(
                    "Progression order must be 4 or less");
            }
            Progression = (ProgressionOrder)progOrder;

            QualityLayers = mem.ReadUInt16();
            if (QualityLayers == 0)
            {
                throw new ArgumentOutOfRangeException(
                    "LayerCount must be strictly positive");
            }

            byte useMct = mem.ReadUInt8();
            if (useMct > 1)
            {
                throw new ArgumentOutOfRangeException("MCT must be 0 or 1");
            }
            UseMultipleComponentTransform = useMct == 1;

            DecompositionLevels = mem.ReadUInt8();
            if (DecompositionLevels > 32)
            {
                throw new ArgumentOutOfRangeException(
                    "DecompositionLevels must be 32 or less");
            }

            _cblkExpnX = mem.ReadUInt8();

            _cblkExpnY = mem.ReadUInt8();

            if((_cblkExpnX > (1 << 4)) ||
               (_cblkExpnY > (1 << 4)) || 
               (_cblkExpnX + _cblkExpnY) > 8)
            {
                throw new ArgumentOutOfRangeException(
                    "codeblock width and height must be at most 1K samples "
                     + " the area of the codeblock should be at most 4K samples");
            }

            byte cblkStyle = mem.ReadUInt8();
            if(!Enum.IsDefined(typeof(CodeblockStyle), cblkStyle))
            {
                throw new ArgumentOutOfRangeException(
                    "coding block style must be 32 or less");
            }

            CBlkStyle = (CodeblockStyle)cblkStyle;

            byte waveletFilter = mem.ReadUInt8();
            if (!Enum.IsDefined(typeof(WaveletTransform), waveletFilter))
            {
                throw new ArgumentException(
                    "WaveletTransform undefined");
            }

            WaveletFilter = (WaveletTransform)waveletFilter;


            if(UsePrecincts)
            {
                
                int expectedCodSize = 12 + DecompositionLevels + 1;
                if(_markerLength != expectedCodSize) {
                    throw new ArgumentOutOfRangeException(
                        "size mismatch in COD marker segment for precinct specs");
                }
            
            }

            _ppx = new byte[DecompositionLevels + 1];
            _ppy = new byte[DecompositionLevels + 1];
            for (int r = 0; r <= DecompositionLevels; r++)
            {
                if (UsePrecincts)
                {
                    byte val = mem.ReadUInt8();
                    _ppx[r] = (byte)(val & 0xF);
                    _ppy[r] = (byte)((val >> 4) & 0xF);
                }
                else
                {
                    // Table A.13 – Coding style parameter values for the
                    // Scod parameter:
                    // Entropy coder, precincts with PPx = 15 and PPy = 15 
                    _ppx[r] = _ppy[r] = 0xF;
                }
            }

            PrecinctPartitions = _ppx
                .Zip(_ppy, (ppx, ppy) => new Point(ppx, ppy))
                .ToList().AsReadOnly();

        }

        public override byte[] GenerateMarkerBody()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteUInt8((byte)_scod);
            mem.WriteUInt8((byte)Progression);
            mem.WriteUInt16((ushort)QualityLayers);
            mem.WriteUInt8((byte)(UseMultipleComponentTransform ? 1 : 0));
            mem.WriteUInt8((byte)DecompositionLevels);
            mem.WriteUInt8((byte)_cblkExpnX);
            mem.WriteUInt8((byte)_cblkExpnY);
            mem.WriteUInt8((byte)CBlkStyle);
            mem.WriteUInt8((byte)WaveletFilter);
            for (int p = 0; UsePrecincts && p < _ppx.Length; p++ )
            {
                byte precSize = (byte)((_ppy[p] << 4) | _ppx[p]);
                mem.WriteUInt8(precSize);
            }
                return mem.ToArray();
        }

        [Flags]
        public enum CodingStyle : byte
        {
            None = 0,
            UsePrecincts = 1,
            UseSopMarker = 2,
            UseEphMarker = 4
        }

        [Flags]
        public enum CodeblockStyle : byte
        {
            None = 0,
            SelectiveArithmeticCodingBypass = 1,
            ResetContextProbabilitiesOnCodingPassBoundaries = 2,
            TerminateOnEachCodingPass = 4,
            VerticalCausalContext = 8,
            PredictableTermination = 16,
            SegmentationSymbols = 32
        }

        public enum WaveletTransform : byte
        {
            Irreversible_9_7 = 0,
            Reversible_5_3 = 1
        }
    }
}
