# Skyreach
Library for JPEG2000 structural traversal and compressed domain manipulation

## INTRODUCTION

Skyreach is a library that is designated for structural traversal of 
JPEG2000 files and manipulation of JPEG2000 images in the compressed domain.

Skyreach does not encode or decode JPEG2000 images, however it can read and parse
existing images, extract useful data out of an image and create a new one based on
the input. A Simple example would be the extraction of a low resolution image out
of a high resolution source, by simple means of copying the relevant data out of
the source image to a new one.

In order to use Skyreach effectively one must know the structure and layout of
a JP2 image. Fortunately, these topics are covered in a set of attached wiki articles.

Skyreach includes a query functionality that enables quering an existing
image, for example, to query for a reduced resolution subset, or 
a limited region of interest, and get a result set of inner 
JPEG2000 data packets that answer the query. The result set can be used
to construct a new image, simply by copying it to the destination image.

Performance wise, Skyreach is designed to work efficientry over very
large data sets and keep the bottleneck only at the IO level.
Meaning that any operation using the library should be constrained by
the IO throughput of the underlying platform, and not by algorithmic
complexity or memory allocation of the library.

## Working with the API
TBD

