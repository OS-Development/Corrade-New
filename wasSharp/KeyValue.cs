///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace wasSharp
{
    public class KeyValue
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <returns>true if the key was found in data</returns>
        public static Func<string, string, string> Get =
            ((Expression<Func<string, string, string>>) ((key, data) => data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => o[0].Equals(key))
                .Select(o => o[1])
                .FirstOrDefault())).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns a key-value data string with a key set to a given value.
        /// </summary>
        /// <returns>
        ///     a key-value data string or the empty string if either key or
        ///     value are empty
        /// </returns>
        public static Func<string, string, string, string> Set =
            ((Expression<Func<string, string, string, string>>)
                ((key, value, data) => string.Join("&", string.Join("&", data.Split('&')
                    .AsParallel()
                    .Select(o => o.Split('='))
                    .Where(o => o.Length.Equals(2))
                    .Where(o => !o[0].Equals(key))
                    .Select(o => string.Join("=", o[0], o[1]))), string.Join("=", key, value)))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Deletes a key-value pair from a string referenced by a key.
        /// </summary>
        /// <returns>a key-value pair string</returns>
        public static Func<string, string, string> Delete =
            ((Expression<Func<string, string, string>>) ((key, data) => string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => !o[0].Equals(key))
                .Select(o => string.Join("=", o[0], o[1]))
                .ToArray()))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decodes key-value pair data to a dictionary.
        /// </summary>
        /// <returns>a dictionary containing the keys and values</returns>
        public static Func<string, Dictionary<string, string>> Decode =
            ((Expression<Func<string, Dictionary<string, string>>>) (data => data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Select(o => new
                {
                    k = o[0],
                    v = o[1]
                })
                .GroupBy(o => o.k)
                .ToDictionary(o => o.Key, p => p.First().v))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Serialises a dictionary to key-value data.
        /// </summary>
        /// <returns>a key-value data encoded string</returns>
        public static Func<Dictionary<string, string>, string> Encode =
            ((Expression<Func<Dictionary<string, string>, string>>)
                (data => string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value))))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Escapes a dictionary's keys and values for sending as POST data.
        /// </summary>
        public static Func<Dictionary<string, string>, Func<string, string>, Dictionary<string, string>> Escape =
            ((Expression<Func<Dictionary<string, string>, Func<string, string>, Dictionary<string, string>>>)
                ((data, func) => data.AsParallel().ToDictionary(o => func(o.Key), p => func(p.Value)))).Compile();
    }
}