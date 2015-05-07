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
    /// <summary>
    /// SizMarker contains basic information about the image and is required
    /// as the first marker (after the SOC) in the main header.
    /// It contains the size of the image and tile, the offsets
    /// in the high resolution grid,  number of color components,
    /// their sub-sampling factors and sample bit depth (precision)
    /// </summary>
    public class SizMarker : MarkerSegment
    {
        /// <summary>
        /// Initiates a SizMarker from an existing codestream.
        /// </summary>
        /// <param name="length">
        /// Length in bytes of the marker segment 
        /// Identical to the length field that is advertised in the Length
        /// field of each marker segment. The length does not include the 2 
        /// bytes of the marker.
        /// /// </param>
        /// <param name="markerBody">
        /// Body of the marker segment, without the marker type and length 
        /// fields
        /// </param>
        protected internal SizMarker(ushort length, byte[] markerBody)
            : base(MarkerType.SIZ, length, markerBody)
        {
            Parse();
        }

        /// <param name="referenceGridSize">
        /// Size of the high resolution grid, or canvas
        /// that every object in the codestream should be fit to.
        /// </param>
        /// <param name="imageOffset">
        /// Horizontal and vertical offset from the origin of the reference 
        /// grid to the left and top side of the image area.
        /// </param>
        /// <param name="tileSize">
        /// Dimensions of one reference tile with respect to the 
        /// reference grid.
        /// </param>
        /// <param name="tileOffset">
        /// Horizontal and vertical offset from the origin of the reference
        /// grid to the left and top side of the first tile.
        /// </param>
        /// <param name="numComponents">
        /// Number of color components in the image. 
        /// </param>
        /// <param name="precisionPerComponent">
        /// Precision (depth) in bits and sign of the component samples.
        /// Each byte represents precision value per component before 
        /// DC level shifting is performed.
        /// </param>
        /// <param name="SubSamplingX">
        /// Horizontal separation of a sample of i-th component 
        /// with respect to the reference grid. 
        /// </param>
        /// <param name="SubSamplingY">
        /// Vertical separation of a sample of i-th component
        /// with respect to the reference grid. 
        /// </param>
        public SizMarker(
            Size referenceGridSize, Point imageOffset,
            Size tileSize, Point tileOffset,
            ushort numComponents, byte[] precisionPerComponent,
            byte[] subSamplingX, byte[] subSamplingY)
            : base(MarkerType.SIZ)
        {
            Profile = 0;
            RefGridSize = referenceGridSize;
            ImageOffset = imageOffset;
            TileSize = tileSize;
            TileOffset = tileOffset;
            Components = numComponents;

            if(precisionPerComponent.Length != numComponents ||
                subSamplingX.Length != numComponents ||
                subSamplingY.Length != numComponents)
            {
                throw new ArgumentException(
                    "Must supply sub-sampling and precision for each" +
                    " color component");
            }

            Precisions = precisionPerComponent;
            SubSamplingX = subSamplingX;
            SubSamplingY = subSamplingY;
        }

        /// <summary>
        /// Capabilities that a decoder needs to properly decode the codestream
        /// </summary>
        public ushort Profile { get; private set; }

        /// <summary>
        /// Dimensions of the reference grid. 
        /// <para>
        /// ISO 15443-1 mandate unsigned 32 bits [0,2^32 - 1]
        /// Size and Point CLR objects use signed 32 bits [2^32,2^31 - 1]
        /// </para>
        /// </summary>
        public Size RefGridSize { get; private set; }

        /// <summary>
        /// Horizontal and vertical offset from the origin of the reference 
        /// grid to the left and top side of the image area. 
        /// 
        /// ISO 15443-1 mandate unsigned 32 bits [0,2^32 - 1]
        /// Size and Point CLR objects use signed 32 bits [2^31,2^31 - 1]
        /// </summary>
        public Point ImageOffset { get; private set; }

        /// <summary>
        /// Dimensions of one reference tile with respect to the reference grid. 
        /// 
        /// ISO 15443-1 mandate unsigned 32 bits [0,2^32 - 1]
        /// Size and Point CLR objects use signed 32 bits [-2^31,2^31 - 1]
        /// </summary>
        public Size TileSize { get; private set; }

        /// <summary>
        /// Horizontal and vertical offset from the origin of the reference grid
        /// to the left and top side of the first tile. 
        /// </summary>
        public Point TileOffset { get; private set; }

        /// <summary>
        /// Number of color components in the image. 
        /// </summary>
        public ushort Components { get; private set; }

        /// <summary>
        /// Precision (depth) in bits and sign of the component samples.
        /// Each byte represents precision value per component before 
        /// DC level shifting is performed.
        /// </summary>
        public byte[] Precisions { get; private set; }

        /// <summary>
        /// Horizontal separation of a sample of i-th component 
        /// with respect to the reference grid. 
        /// </summary>
        public byte[] SubSamplingX { get; private set; }

        /// <summary>
        /// Vertical separation of a sample of i-th component
        /// with respect to the reference grid. 
        /// </summary>
        public byte[] SubSamplingY { get; private set; }

        protected internal override void Parse()
        {
            MemoryStream mem = new MemoryStream(_markerBody);

            if(_markerLength < 38)
            {
                throw new ArgumentException(
                    "SIZ marker length must be at least 38 bytes");
            }
            
            Profile = mem.ReadUInt16();
            if(Profile > 2)
            {
                throw new ArgumentException(
                    "unsupported codestream capabilities");
            }

            uint refGridWidth = mem.ReadUInt32();
            uint refGridHeight = mem.ReadUInt32();

            ThrowIfSizeOverflows(refGridWidth, "Reference Grid Width");
            ThrowIfSizeOverflows(refGridHeight, "Reference Grid Height");

            if (refGridWidth == 0 || refGridHeight == 0)
            {
                throw new ArgumentException(
                    "Reference grid width and height must be above zero");
            }

            RefGridSize = new Size((int)refGridWidth, (int)refGridHeight);

            uint imageOffsetX = mem.ReadUInt32();
            uint imageOffsetY = mem.ReadUInt32();
            if (imageOffsetX == Int32.MaxValue || imageOffsetY == Int32.MaxValue)
            {
                throw new ArgumentException(
                    "Image offset must be below the maximal value for the reference grid");
            }

            ThrowIfSizeOverflows(imageOffsetX, "image offset X");
            ThrowIfSizeOverflows(imageOffsetY, "image offset Y");

            ImageOffset = new Point((int) imageOffsetX, (int) imageOffsetY);

            uint tileWidth = mem.ReadUInt32();
            uint tileHeight = mem.ReadUInt32();
            if (tileWidth == 0 || tileHeight == 0)
            {
                throw new ArgumentException(
                    "Reference tile width and height must be above zero");
            }

            ThrowIfSizeOverflows(tileWidth, "Tile Width");
            ThrowIfSizeOverflows(tileHeight, "Tile Height");

            TileSize = new Size((int)tileWidth, (int)tileHeight);
            
            uint tileOffsetX = mem.ReadUInt32();
            uint tileOffsetY = mem.ReadUInt32();
            if (tileOffsetX == Int32.MaxValue || tileOffsetY == Int32.MaxValue)
            {
                throw new ArgumentException(
                    "Tile offset must be below the maximal value for the tile size");
            }

            TileOffset = new Point((int)tileOffsetX, (int)tileOffsetY);

            ThrowIfSizeOverflows(tileOffsetX, "Tile Offset X");
            ThrowIfSizeOverflows(tileOffsetY, "Tile Offset Y");

            Components = mem.ReadUInt16();
            if(Components == 0 || Components > (1 << 14))
            {
                throw new ArgumentOutOfRangeException(
                    "Component count out of valid range");
            }

            if(_markerLength != (38 + (Components*3)))
            {
                throw new ArgumentOutOfRangeException(
                    "Component count and length of SIZ marker do not agree");
            }

            Precisions = new byte[Components];
            SubSamplingX = new byte[Components];
            SubSamplingY = new byte[Components];
            for(int c = 0; c < Components; c++)
            {
                Precisions[c] = mem.ReadUInt8();
                SubSamplingX[c] = mem.ReadUInt8();
                SubSamplingY[c] = mem.ReadUInt8();

                if(SubSamplingX[c] == 0 || SubSamplingY[c] == 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "Sub-sampling must be strictly positive");
                }
            }


        }

        public override byte[] GenerateMarkerBody()
        {
            MemoryStream mem = new MemoryStream();
            // start writing the markerBody directly to memory stream
            mem.WriteUInt16(Profile);
            mem.WriteUInt32((uint)RefGridSize.Width);
            mem.WriteUInt32((uint)RefGridSize.Height);
            mem.WriteUInt32((uint)ImageOffset.X);
            mem.WriteUInt32((uint)ImageOffset.Y);
            mem.WriteUInt32((uint)TileSize.Width);
            mem.WriteUInt32((uint)TileSize.Height);
            mem.WriteUInt32((uint)TileOffset.X);
            mem.WriteUInt32((uint)TileOffset.Y);

            mem.WriteUInt16(Components);
            for (int c = 0; c < Components; c++)
            {
                mem.WriteUInt8(Precisions[c]);
                mem.WriteUInt8(SubSamplingX[c]);
                mem.WriteUInt8(SubSamplingY[c]);
            }
            return mem.ToArray();
        }

        private void ThrowIfSizeOverflows(uint size, string paramName)
        {
            if(size > Int32.MaxValue)
            {
                throw new NotSupportedException(
                    "ISO-15443-1 defines [" + paramName + "] as 32 bit unsigned integer" +
                    " CLR implements Point and Size with 32 bit signed integers.");
            }
        }
    }
}
