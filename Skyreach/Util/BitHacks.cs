using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skyreach.Util
{
    public static class BitHacks
    {
        /// <summary>
        /// Fills a 32 bit unsigned integer with ones
        /// from the MSB to the LSB
        /// ex, binary & decimal: 
        ///     FillOnes(101b) == 111b, FillOnes(5) == 7 (0xF)
        ///     FillOnes(1000b) == 1111b, FillOnes(8) == 15 (0xFF)
        ///     FillOnes(0b) == 0b - trivial zero
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static uint FillOnes(uint n)
        {
            n |= (n >> 1);
            n |= (n >> 2);
            n |= (n >> 4);
            n |= (n >> 8);
            n |= (n >> 16);
            return n;
        }

        /// <summary>
        /// Checks if a number is a power of 2.
        /// ex.    
        ///     IsPowerOf2(16) == true
        ///     IsPowerOf2(0) == false
        ///     IsPowerOf2(15) == false
        /// </summary>
        /// <param name="n"></param>
        /// <returns>true iff n is a power of 2</returns>
        public static bool IsPowerOf2(uint n)
        {
            return (n != 0) && (n & (n - 1)) == 0;
        }

        /// <summary>
        /// ex.
        ///     PopCount(10001b) == 2
        ///     PopCount(0b) == 0
        ///     PopCount(1111b) == 4
        /// </summary>
        /// <seealso cref="http://stackoverflow.com/questions/109023/how-to-count-the-number-of-set-bits-in-a-32-bit-integer"/>
        /// <param name="n"></param>
        /// <returns>
        ///     The number of bits set in a given integer
        /// </returns>
        public static int PopCount(uint n)
        {
            // comment from StackOverflow:
            // It's write-only code. Just put a comment that you are not 
            // meant to understand or maintain this code, just worship the
            // gods that revealed it to mankind. I am not one of them, 
            // just a prophet. :) –  Matt Howells       
            n -= ((n >> 1) & 0x55555555);
            n = (((n >> 2) & 0x33333333) + (n & 0x33333333));
            n = (((n >> 4) + n) & 0x0f0f0f0f);
            n += (n >> 8);
            n += (n >> 16);
            return (int)(n & 0x0000003f);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="n"></param>
        /// <returns>
        /// The log2 of the given number,
        /// rounded down to the nearest integer.
        /// </returns>
        public static int LogFloor2(uint n)
        {
            return PopCount(FillOnes(n)) - 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static int MSB(uint n)
        {
            return LogFloor2(n) + 1;
        }


        /// <summary>
        /// Divide two integral quantities and return their ceiled result
        /// </summary>
        /// <param name="nom"></param>
        /// <param name="denom"></param>
        /// <returns></returns>
        public static int DivCeil(int nom, int denom)
        {
            // fast integer division with ceiling() to the closest integer
            return ((nom - 1) / denom) + 1;
        }

        /// <summary>
        /// Divide two integral quantities and return their ceiled result
        /// Use bit shifting for denominators which are powers of two.
        /// </summary>
        /// <param name="nom"></param>
        /// <param name="log2Denom"></param>
        /// <returns></returns>
        public static int DivShiftCeil(int nom, int log2Denom)
        {
            // fast integer division with ceiling() to the closest integer
            return ((nom - 1) >> log2Denom) + 1;
        }
    }
}
