///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using OpenMetaverse;

namespace wasOpenMetaverse
{
    public class Helpers
    {
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

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Tries to build an UUID out of the data string.
        /// </summary>
        /// <param name="data">a string</param>
        /// <returns>an UUID or the supplied string in case data could not be resolved</returns>
        public static object StringOrUUID(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            UUID @UUID;
            if (!UUID.TryParse(data, out @UUID))
            {
                return data;
            }
            return @UUID;
        }

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
    }
}