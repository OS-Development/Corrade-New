///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
        /// <typeparam name="T">the data type</typeparam>
        /// <param name="mask">the mask to check the flag for</param>
        /// <param name="flag">the flag to check</param>
        /// <returns>true in case the flag is set</returns>
        public static bool IsMaskFlagSet<T>(T mask, T flag) where T : struct
        {
            return ((ulong) (object) mask & (ulong) (object) flag) != 0;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sets a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">the data type</typeparam>
        /// <param name="mask">the mask to set the flag on</param>
        /// <param name="flag">the flag to set</param>
        public static void SetMaskFlag<T>(ref T mask, T flag) where T : struct
        {
            mask = (T) (object) ((ulong) (object) mask | (ulong) (object) flag);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Unset a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">the data type</typeparam>
        /// <param name="mask">the mask to unset the flag on</param>
        /// <param name="flag">the flag to unset</param>
        public static void UnsetMaskFlag<T>(ref T mask, T flag) where T : struct
        {
            mask = (T) (object) ((ulong) (object) mask & ~(ulong) (object) flag);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Toggle a flag for a bitmask.
        /// </summary>
        /// <typeparam name="T">the data type</typeparam>
        /// <param name="mask">the mask to toggle the flag on</param>
        /// <param name="flag">the flag to toggle</param>
        public static void ToggleMaskFlag<T>(ref T mask, T flag) where T : struct
        {
            mask = (T) (object) ((ulong) (object) mask ^ (ulong) (object) flag);
        }
    }
}