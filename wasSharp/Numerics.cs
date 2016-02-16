///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq.Expressions;

namespace wasSharp
{
    public static class Numerics
    {

#if !__MonoCS__
        private static readonly Func<double, double, double, double, double, double> directMapValueToRange =
            ((Expression<Func<double, double, double, double, double, double>>)
                ((value, xMin, xMax, yMin, yMax) => yMin + (yMax - yMin)*(value - xMin)/(xMax - xMin))).Compile();
#endif

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Given a value in a source value range and a target range, map
        ///     the value from the source range into the target range.
        /// </summary>
        /// <remarks>
        ///     value - the value to map
        ///     xMin - the lower bound of the source range
        ///     xMax - the upper bound of the source range
        ///     yMin - the lower bound of the target range
        ///     yMax - the upper bound of the target range
        /// </remarks>
        /// <returns>a value in x mapped in the range of y</returns>
        public static double MapValueToRange(double value, double xMin, double xMax, double yMin, double yMax)
        {
#if !__MonoCS__
            return directMapValueToRange(value, xMin, xMax, yMin, yMax);
#else
            return yMin + (yMax - yMin)*(value - xMin)/(xMax - xMin);
#endif
        }
    }
}