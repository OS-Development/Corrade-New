///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace wasSharp
{
    public static class Web
    {
        private static readonly Func<string, string> directURIEscapeDataString =
            ((Expression<Func<string, string>>)
                (data => string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766).ToArray().AsParallel()
                    .Select(o => Uri.EscapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - o*32766))))
                    .ToArray()))).Compile();

        private static readonly Func<string, string> directURIUnescapeDataString =
            ((Expression<Func<string, string>>)
                (data => string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766).ToArray().AsParallel()
                    .Select(
                        o => Uri.UnescapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - o*32766))))
                    .ToArray()))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC3986 URI Escapes a string</summary>
        /// <remarks>
        ///     data - a string to escape
        /// </remarks>
        /// <returns>an RFC3986 escaped string</returns>
        public static string URIEscapeDataString(string data)
        {
            return directURIEscapeDataString(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>URI unescapes an RFC3986 URI escaped string</summary>
        /// <remarks>
        ///     data - a string to unescape
        /// </remarks>
        /// <returns>the resulting string</returns>
        public static string URIUnescapeDataString(string data)
        {
            return directURIUnescapeDataString(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC1738 escaped string</returns>
        public static string URLEscapeDataString(string data)
        {
            return WebUtility.UrlEncode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Unescape a string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>an RFC1738 unescaped string</returns>
        public static string URLUnescapeDataString(string data)
        {
            return WebUtility.UrlDecode(data);
        }
    }
}