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
    public class KeyValue
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>true if the key was found in data</returns>
        public static string wasKeyValueGet(string key, string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
                })
                .Where(o => o.k.Equals(key))
                .Select(o => o.v)
                .FirstOrDefault();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns a key-value data string with a key set to a given value.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="value">the value to set the key to</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>
        ///     a key-value data string or the empty string if either key or
        ///     value are empty
        /// </returns>
        public static string wasKeyValueSet(string key, string value, string data)
        {
            HashSet<string> output = new HashSet<string>(data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = !o.First().Equals(key) ? o.Last() : value
                }).Select(o => string.Join("=", o.k, o.v)));
            string append = string.Join("=", key, value);
            if (!output.Contains(append))
            {
                output.Add(append);
            }
            return string.Join("&", output.ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Deletes a key-value pair from a string referenced by a key.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>a key-value pair string</returns>
        public static string wasKeyValueDelete(string key, string data)
        {
            return string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
                })
                .Where(o => !o.k.Equals(key))
                .Select(o => string.Join("=", o.k, o.v))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decodes key-value pair data to a dictionary.
        /// </summary>
        /// <param name="data">the key-value pair data</param>
        /// <returns>a dictionary containing the keys and values</returns>
        public static Dictionary<string, string> wasKeyValueDecode(string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
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
        /// <param name="data">a dictionary</param>
        /// <returns>a key-value data encoded string</returns>
        public static string wasKeyValueEncode(Dictionary<string, string> data)
        {
            return string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>Escapes a dictionary's keys and values for sending as POST data.</summary>
        /// <param name="data">A dictionary containing keys and values to be escaped</param>
        /// <param name="func">The function to use to escape the keys and values in the dictionary.</param>
        public static Dictionary<string, string> wasKeyValueEscape(Dictionary<string, string> data,
            Func<string, string> func)
        {
            return data.AsParallel().ToDictionary(o => func(o.Key), p => func(p.Value));
        }
    }
}