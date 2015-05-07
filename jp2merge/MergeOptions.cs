using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace jp2merge
{
    internal class MergeOptions
    {
        [Option('f', "fromfile", Required=true, 
            HelpText="Path to a text file that contains a list of jpeg2000 file paths separated by a new line.")]
        public string InputPathsAsTextFile { get; set; }

        [Option('o', "out", HelpText=
            "Output JPEG2000 file path, if absent, first argument in \'fromfile\' is assumed to be the output")]
        public string MergedOutputPath { get; set; }

        [Option('x', Required=true,
            HelpText="Number of horizontal tiles")]
        public int TilesX { get; set; }

        [Option('y', Required=true,
        HelpText="Number of vertical tiles")]
        public int TilesY { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText {
                Heading = new HeadingInfo("jp2merge", Program.JP2MERGE_VERSION.ToString()),
                Copyright = new CopyrightInfo(Program.AUTHOR, 2015),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true,                
            };
            help.AddPreOptionsLine("Usage: jp2merge -x 10 -y 12 -f jp2Paths.txt [-o merged.jp2]");
            help.AddPreOptionsLine(String.Concat(
                "Input file paths must be specified inside the jp2Paths.txt file ",
                "in a raster order."));
            help.AddPreOptionsLine(String.Concat(
                "if output file path is unspecified, the first file in jp2Paths.txt ",
                "is assumed to be the merged output file"));
            help.AddOptions(this);
            help.AddPostOptionsLine(
                "jp2merge combines multiple JPEG2000 images into a single image.");
            help.AddPostOptionsLine(String.Concat(
                "Input images are assumed to be JPEG2000 tiles that have been ",
                "compressed individually with the same JPEG2000 encoding parameters."));
            help.AddPostOptionsLine(String.Concat(
                "Initial compression of tiles may happen across a scaled-out cluster ",
                "of compressing nodes. The output of each compression node can now be ",
                "combined and merged into a single JPEG2000 image"));
            help.AddPostOptionsLine("");
            help.AddPostOptionsLine(
                "Input images must satisfy one of the following conditions: ");
            help.AddPostOptionsLine(String.Concat(
                " * Aligned using their image and tile offset properties",
                " to their position in the merged image reference grid"));
            help.AddPostOptionsLine(String.Concat(
                "* No codeblock at any subband in any resolution level and position",
                " should have its dimensions clipped by either a subband or a precinct")); 
            return help;
        }


    }
}
