///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace wasSharp
{
    public static class KeyValue
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <returns>true if the key was found in data</returns>
        public static string Get(string key, string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => Strings.StringEquals(o[0], key, StringComparison.Ordinal))
                .Select(o => o[1])
                .FirstOrDefault();
        }

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
        public static string Set(string key, string value, string data)
        {
            return string.Join("&", string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => !Strings.StringEquals(o[0], key, StringComparison.Ordinal))
                .Select(o => string.Join("=", o[0], o[1]))), string.Join("=", key, value));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Deletes a key-value pair from a string referenced by a key.
        /// </summary>
        /// <returns>a key-value pair string</returns>
        public static string Delete(string key, string data)
        {
            return string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => !Strings.StringEquals(o[0], key, StringComparison.Ordinal))
                .Select(o => string.Join("=", o[0], o[1]))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decodes key-value pair data to a dictionary.
        /// </summary>
        /// <returns>a dictionary containing the keys and values</returns>
        public static Dictionary<string, string> Decode(string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Select(o => new
                {
                    k = o[0],
                    v = o[1]
                })
                .GroupBy(o => o.k)
                .ToDictionary(o => o.Key, p => p.First().v);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Serialises a dictionary to key-value data.
        /// </summary>
        /// <returns>a key-value data encoded string</returns>
        public static string Encode(Dictionary<string, string> data)
        {
            return string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Escapes a dictionary's keys and values for sending as POST data.
        /// </summary>
        public static Dictionary<string, string> Escape(Dictionary<string, string> data, Func<string, string> func)
        {
            return data.AsParallel().ToDictionary(o => func(o.Key), p => func(p.Value));
        }
    }
}