///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;

namespace wasOpenMetaverse
{
    public static class Helpers
    {
        public static readonly HashSet<UUID> LindenAnimations = new HashSet<UUID>(typeof(Animations).GetFields(
            BindingFlags.Public |
            BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));

        public static readonly Regex AvatarFullNameRegex = new Regex(@"^(?<first>.*?)([\s\.]|$)(?<last>.*?)$",
            RegexOptions.Compiled);

#if !__MonoCS__
        private static readonly Func<string, IEnumerable<string>> directGetAvatarNames =
            ((Expression<Func<string, IEnumerable<string>>>) (o => !string.IsNullOrEmpty(o)
                ? AvatarFullNameRegex.Matches(o)
                    .Cast<Match>()
                    .ToDictionary(p => new[]
                    {
                        p.Groups["first"].Value,
                        p.Groups["last"].Value
                    })
                    .SelectMany(
                        p =>
                            new[]
                            {
                                p.Key[0].Trim(),
                                !string.IsNullOrEmpty(p.Key[1])
                                    ? p.Key[1].Trim()
                                    : Constants.AVATARS.LASTNAME_PLACEHOLDER
                            })
                : null)).Compile();
#endif

        /// <summary>
        ///     Gets the first name and last name from an avatar name.
        /// </summary>
        /// <returns>the firstname and the lastname or Resident</returns>
        public static IEnumerable<string> GetAvatarNames(string fullName)
        {
#if !__MonoCS__
            return directGetAvatarNames(fullName);
#else
            return !String.IsNullOrEmpty(fullName)
               ? AvatarFullNameRegex.Matches(fullName)
                   .Cast<Match>()
                   .ToDictionary(p => new[]
                   {
                        p.Groups["first"].Value,
                        p.Groups["last"].Value
                   })
                   .SelectMany(
                       p =>
                           new[]
                           {
                                p.Key[0].Trim(),
                                !String.IsNullOrEmpty(p.Key[1])
                                    ? p.Key[1].Trim()
                                    : Constants.AVATARS.LASTNAME_PLACEHOLDER
                           })
               : null;
#endif
        }

        /// <summary>
        ///     Used to determine whether the current grid is Second Life.
        /// </summary>
        /// <returns>true if the connected grid is Second Life</returns>
        public static bool IsSecondLife(GridClient Client)
        {
            return Client.Network.CurrentSim.SimVersion.Contains(Constants.GRID.SECOND_LIFE);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether a vector falls within a parcel.
        /// </summary>
        /// <param name="position">a 3D vector</param>
        /// <param name="parcel">a parcel</param>
        /// <returns>true if the vector falls within the parcel bounds</returns>
        public static bool IsVectorInParcel(Vector3 position, Parcel parcel)
        {
            return position.X >= parcel.AABBMin.X && position.X <= parcel.AABBMax.X &&
                   position.Y >= parcel.AABBMin.Y && position.Y <= parcel.AABBMax.Y;
        }

        /// <summary>
        ///     Returns a global position from a simulator and a local position.
        /// </summary>
        /// <param name="simulator">a simulator</param>
        /// <param name="position">a region-local position</param>
        /// <returns>a global position</returns>
        /// <remarks>From Radegast</remarks>
        public static Vector3d GlobalPosition(Simulator simulator, Vector3 position)
        {
            uint globalX, globalY;
            Utils.LongToUInts(simulator.Handle, out globalX, out globalY);

            return new Vector3d(
                globalX + (double) position.X,
                globalY + (double) position.Y,
                position.Z);
        }

        /// <summary>
        ///     Returns a global position of a primitive and a local position.
        /// </summary>
        /// <param name="simulator">a simulator</param>
        /// <param name="primitive">a primitive</param>
        /// <returns>a global position</returns>
        /// <remarks>From Radegast</remarks>
        public static Vector3d GlobalPosition(Simulator simulator, Primitive primitive)
        {
            return GlobalPosition(simulator, primitive.Position);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     A simple start location reader for an SLURL formatted location.
        /// </summary>
        /// <copyright>Copyright (c) 2009-2014, Radegast Development Team with changes by Wizardry and Steamworks</copyright>
        public class GridLocation
        {
            private readonly string location;

            public GridLocation(string location)
            {
                switch (string.IsNullOrEmpty(location))
                {
                    case true:
                        this.location = "last";
                        break;
                    default:
                        this.location = location.Trim();
                        break;
                }
            }


            public bool isValid
                => !string.IsNullOrEmpty(Sim) && X >= 0 && X <= 255 && Y >= 0 && Y <= 255 && Z >= 0 && Z <= 255;

            public bool isCustom => location.Contains("/");

            public string Sim => GetSim(location);

            public int X => GetX(location);

            public int Y => GetY(location);

            public int Z => GetZ(location);

            private static string GetSim(string location)
            {
                if (!location.Contains("/"))
                    return location;

                var locSplit = location.Split('/');
                return locSplit[0];
            }

            private static int GetX(string location)
            {
                if (!location.Contains("/"))
                    return 128;

                var locSplit = location.Split('/');

                int returnResult;
                var stringToInt = int.TryParse(locSplit[1], NumberStyles.Integer, Utils.EnUsCulture, out returnResult);

                return stringToInt ? returnResult : 128;
            }

            private static int GetY(string location)
            {
                if (!location.Contains("/"))
                    return 128;

                var locSplit = location.Split('/');

                if (locSplit.Length > 2)
                {
                    int returnResult;
                    var stringToInt = int.TryParse(locSplit[2], NumberStyles.Integer, Utils.EnUsCulture,
                        out returnResult);

                    if (stringToInt)
                        return returnResult;
                }

                return 128;
            }

            private static int GetZ(string location)
            {
                if (!location.Contains("/"))
                    return 0;

                var locSplit = location.Split('/');

                if (locSplit.Length > 3)
                {
                    int returnResult;
                    var stringToInt = int.TryParse(locSplit[3], NumberStyles.Integer, Utils.EnUsCulture,
                        out returnResult);

                    if (stringToInt)
                        return returnResult;
                }

                return 0;
            }
        }
    }
}