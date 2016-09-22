///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

namespace wasSharp
{
    public static class BitTwiddling
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Swaps two integers passed by reference using XOR.
        /// </summary>
        /// <param name="q">first integer to swap</param>
        /// <param name="p">second integer to swap</param>
        public static void XORSwap(ref int q, ref int p)
        {
            q ^= p;
            p ^= q;
            q ^= p;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Checks whether a flag is set for a bitmask.
        /// </summary>
        /// <typeparam name="T">a data type</typeparam>
        /// <typeparam name="U">a data type</typeparam>
        /// <param name="mask">the mask to check the flag for</param>
        /// <param name="flag">the flag to check</param>
        /// <returns>true in case the flag is set</returns>
        public static bool IsMaskFlagSet<T, U>(this T mask, U flag) where T : struct where U : struct
        {
            return unchecked(mask as dynamic & flag as dynamic) != 0;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sets a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">a data type</typeparam>
        /// <typeparam name="U">a data type</typeparam>
        /// <param name="mask">the mask to set the flag on</param>
        /// <param name="flag">the flag to set</param>
        public static void SetMaskFlag<T, U>(ref T mask, U flag) where T : struct where U : struct
        {
            mask = unchecked(mask as dynamic | flag as dynamic);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Create a bitmask from multiple flags.
        /// </summary>
        /// <typeparam name="T">a data type</typeparam>
        /// <param name="flag">the flags to set</param>
        /// <returns>a bitmask</returns>
        public static T CreateMask<T>(this IEnumerable<T> flag) where T : struct
        {
            return flag.Aggregate((o, p) => unchecked(o as dynamic | p as dynamic));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Unset a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">a data type</typeparam>
        /// <typeparam name="U">a data type</typeparam>
        /// <param name="mask">the mask to unset the flag on</param>
        /// <param name="flag">the flag to unset</param>
        public static void UnsetMaskFlag<T, U>(ref T mask, U flag)
        {
            mask = unchecked(mask as dynamic & ~(flag as dynamic));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Toggle a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">a data type</typeparam>
        /// <typeparam name="U">a data type</typeparam>
        /// <param name="mask">the mask to toggle the flag on</param>
        /// <param name="flag">the flag to toggle</param>
        public static void ToggleMaskFlag<T, U>(ref T mask, U flag)
        {
            mask = unchecked(mask as dynamic ^ flag as dynamic);
        }

        /// <summary>
        ///     Computes the previous power of two.
        /// </summary>
        /// <param name="x">the integer</param>
        /// <returns>the previous power of two</returns>
        /// <remarks>Adapted from Hacker's Delight ISBN-10:0201914654</remarks>
        public static T PreviousPowerOfTwo<T>(this T x)
        {
            var y = x as dynamic;
            unchecked
            {
                y = y | (y >> 1);
                y = y | (y >> 2);
                y = y | (y >> 4);
                y = y | (y >> 8);
                y = y | (y >> 16);
                return y - (y >> 1);
            }
        }

        /// <summary>
        ///     Determines if a number is a power of two.
        /// </summary>
        /// <typeparam name="T">the number type</typeparam>
        /// <param name="x">the number</param>
        /// <returns>true of the number is a power of two</returns>
        public static bool IsPowerOfTwo<T>(this T x)
        {
            var y = x as dynamic;
            return (y != 0) && ((y & (y - 1)) == 0);
        }
    }
}