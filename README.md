#Skyreach - A JPEG2000 Transcoding Library

Skyreach is a library that is designated for structural traversal of 
JPEG2000 images and their transcoding in the compressed domain.

Skyreach can be used to extract data out of an JPEG2000 compressed 
image and create a new image based on this data, using only IO based
operations.  Applications include:

- **jp2extract** (work-in-progress): Extracts a combination of: 
	- Low resolution subset of an image.
	- Specific tiles out of a tiled image.
	- Color components, such as the luminance component from an RGB image..
	- Size reduced copy of an image (at the expense of higher visual distortion) for the same resolution level.

- **jp2merge**:  Combines individual image tiles that were compressed independently over a scaled-out cluster of compression nodes into a single JPEG2000 file. jp2merge can be used as the *REDUCE* step of a general *MAP-REDUCE* framework for compressing large images.

- **jp2info** (work-in-progress): a simple application that traverses the codestream and reports useful properties about the internal structure and size of the each tile, quality layer, resolution level, color component and precinct in the image.

To fully appreciate the scalability benefits of using JPEG2000 compression, please see:  
[Understanding scalability in JPEG2000](https://github.com/plroit/Skyreach/wiki/Understanding-scalability-in-JPEG2000)

## INTRODUCTION

Skyreach does not encode or decode JPEG2000 images, however it can read and parse
existing images, extract useful data out of an image and create a new one based on
the input. A Simple example would be the extraction of a low resolution image out
of a high resolution source, by simple means of copying the relevant data out of
the source image to a new one.

In order to use Skyreach effectively one must be familiar with the structure and layout of
a JP2 image. Fortunately, these topics are covered in an attached wiki article [Introduction to JPEG2000 Structure and Layout] (https://github.com/plroit/Skyreach/wiki/Introduction-to-JPEG2000-Structure-and-Layout)

Skyreach includes a query functionality that enables quering an existing
image, for example, querying for a reduced resolution subset, or 
a limited region of interest, and get a result set of inner JPEG2000 data packets 
that answer the query. The result set can be used to construct a new image, 
simply by copying it to the destination image.

Performance wise, Skyreach is designed to work efficientry over very
large data sets and keep the bottleneck only at the IO level.
Meaning that any operation using the library should be constrained by
the IO throughput of the underlying platform, and not by algorithmic
complexity or memory allocation of the library.

## Working with the API
TBD

Simple codestream traversal:
````csharp
Jp2File jp2 = Jp2File.Open(stream);
JP2Codestream cs = jp2.OpenCodestream();
Console.WriteLine("Image Size: {0}", cs.ImageSize);
Console.WriteLine("Tile Size: {0}", cs.TileSize);
Console.WriteLine("Number of tiles [X,Y]", cs.TileCount);
Console.WriteLine("Resolution Levels", cs.DecompositionLevels + 1);
Console.WriteLine("Color components: {0}", cs.Components);
foreach(JP2TilePart tp in cs.OpenChildren())
{
    Console.WriteLine("Tile index: {0}", tp.TileIndex);
    Console.WriteLine("Tilepart {0}", tp.TilePartIndex);
    Console.WriteLine("Size in bytes: {0}", tp.Length);
    Console.WriteLine("Position in stream: {0}", tp.Position);
    Console.WriteLine("Packets: {0}", tp.Packets);
    Console.WriteLine("Size of all packets {0}", tp.TotalPacketLength);    
}
````
