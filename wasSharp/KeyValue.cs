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
    public static class KeyValue
    {

#if !__MonoCS__
        private static readonly Func<string, string, string> directGet =
            ((Expression<Func<string, string, string>>) ((key, data) => data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => o[0].Equals(key))
                .Select(o => o[1])
                .FirstOrDefault())).Compile();

        private static readonly Func<string, string, string, string> directSet =
            ((Expression<Func<string, string, string, string>>)
                ((key, value, data) => string.Join("&", string.Join("&", data.Split('&')
                    .AsParallel()
                    .Select(o => o.Split('='))
                    .Where(o => o.Length.Equals(2))
                    .Where(o => !o[0].Equals(key))
                    .Select(o => string.Join("=", o[0], o[1]))), string.Join("=", key, value)))).Compile();

        private static readonly Func<string, string, string> directDelete =
            ((Expression<Func<string, string, string>>) ((key, data) => string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => !o[0].Equals(key))
                .Select(o => string.Join("=", o[0], o[1]))
                .ToArray()))).Compile();

        private static readonly Func<string, Dictionary<string, string>> directDecode =
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

        private static readonly Func<Dictionary<string, string>, string> directEncode =
            ((Expression<Func<Dictionary<string, string>, string>>)
                (data => string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value))))).Compile();

        private static readonly Func<Dictionary<string, string>, Func<string, string>, Dictionary<string, string>>
            directEscape =
                ((Expression<Func<Dictionary<string, string>, Func<string, string>, Dictionary<string, string>>>)
                    ((data, func) => data.AsParallel().ToDictionary(o => func(o.Key), p => func(p.Value)))).Compile();
#endif

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <returns>true if the key was found in data</returns>
        public static string Get(string key, string data)
        {
#if !__MonoCS__
            return directGet(key, data);
#else
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => o[0].Equals(key))
                .Select(o => o[1])
                .FirstOrDefault();
#endif
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
#if !__MonoCS__
            return directSet(key, value, data);
#else
            return string.Join("&", string.Join("&", data.Split('&')
                    .AsParallel()
                    .Select(o => o.Split('='))
                    .Where(o => o.Length.Equals(2))
                    .Where(o => !o[0].Equals(key))
                    .Select(o => string.Join("=", o[0], o[1]))), string.Join("=", key, value));
#endif
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
#if !__MonoCS__
            return directDelete(key, data);
#else
            return string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('='))
                .Where(o => o.Length.Equals(2))
                .Where(o => !o[0].Equals(key))
                .Select(o => string.Join("=", o[0], o[1]))
                .ToArray());
#endif
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
#if !__MonoCS__
            return directDecode(data);
#else
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
#endif
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
#if !__MonoCS__
            return directEncode(data);
#else
            return string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value)));
#endif
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Escapes a dictionary's keys and values for sending as POST data.
        /// </summary>
        public static Dictionary<string, string> Escape(Dictionary<string, string> data, Func<string, string> func)
        {
#if !__MonoCS__
            return directEscape(data, func);
#else
            return data.AsParallel().ToDictionary(o => func(o.Key), p => func(p.Value));
#endif
        }
    }
}