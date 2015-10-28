///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;

namespace wasSharp
{
    public class Arrays
    {
        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets an array element at a given modulo index.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="index">a positive or negative index of the element</param>
        /// <param name="data">the array</param>
        /// <return>an array element</return>
        public static T wasGetElementAt<T>(T[] data, int index)
        {
            switch (index < 0)
            {
                case true:
                    return data[((index%data.Length) + data.Length)%data.Length];
                default:
                    return data[index%data.Length];
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets a sub-array from an array.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="data">the array</param>
        /// <param name="start">the start index</param>
        /// <param name="stop">the stop index (-1 denotes the end)</param>
        /// <returns>the array slice between start and stop</returns>
        public static T[] wasGetSubArray<T>(T[] data, int start, int stop)
        {
            if (stop.Equals(-1))
                stop = data.Length - 1;
            T[] result = new T[stop - start + 1];
            Array.Copy(data, start, result, 0, stop - start + 1);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Delete a sub-array and return the result.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="data">the array</param>
        /// <param name="start">the start index</param>
        /// <param name="stop">the stop index (-1 denotes the end)</param>
        /// <returns>the array without elements between start and stop</returns>
        public static T[] wasDeleteSubArray<T>(T[] data, int start, int stop)
        {
            if (stop.Equals(-1))
                stop = data.Length - 1;
            T[] result = new T[data.Length - (stop - start) - 1];
            Array.Copy(data, 0, result, 0, start);
            Array.Copy(data, stop + 1, result, start, data.Length - stop - 1);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Concatenate multiple arrays.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="arrays">multiple arrays</param>
        /// <returns>a flat array with all arrays concatenated</returns>
        public static T[] wasConcatenateArrays<T>(params T[][] arrays)
        {
            int resultLength = 0;
            foreach (T[] o in arrays)
            {
                resultLength += o.Length;
            }
            T[] result = new T[resultLength];
            int offset = 0;
            for (int x = 0; x < arrays.Length; x++)
            {
                arrays[x].CopyTo(result, offset);
                offset += arrays[x].Length;
            }
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Permutes an array in reverse a given number of times.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="input">the array</param>
        /// <param name="times">the number of times to permute</param>
        /// <returns>the array with the elements permuted</returns>
        public static T[] wasReversePermuteArrayElements<T>(T[] input, int times)
        {
            if (times.Equals(0)) return input;
            T[] slice = new T[input.Length];
            Array.Copy(input, 1, slice, 0, input.Length - 1);
            Array.Copy(input, 0, slice, input.Length - 1, 1);
            return wasReversePermuteArrayElements(slice, --times);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Permutes an array forward a given number of times.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="input">the array</param>
        /// <param name="times">the number of times to permute</param>
        /// <returns>the array with the elements permuted</returns>
        public static T[] wasForwardPermuteArrayElements<T>(T[] input, int times)
        {
            if (times.Equals(0)) return input;
            T[] slice = new T[input.Length];
            Array.Copy(input, input.Length - 1, slice, 0, 1);
            Array.Copy(input, 0, slice, 1, input.Length - 1);
            return wasForwardPermuteArrayElements(slice, --times);
        }
    }
}