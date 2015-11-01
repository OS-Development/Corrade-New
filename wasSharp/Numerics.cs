///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

namespace wasSharp
{
    public class Numerics
    {
        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Given a value in a source value range and a target range, map
        ///     the value from the source range into the target range.
        /// </summary>
        /// <param name="value">the value to map</param>
        /// <param name="xMin">the lower bound of the source range</param>
        /// <param name="xMax">the upper bound of the source range</param>
        /// <param name="yMin">the lower bound of the target range</param>
        /// <param name="yMax">the upper bound of the target range</param>
        public static double MapValueToRange(double value, double xMin, double xMax, double yMin, double yMax)
        {
            return yMin + (
                (
                    yMax - yMin
                    )
                *
                (
                    value - xMin
                    )
                /
                (
                    xMax - xMin
                    )
                );
        }
    }
}