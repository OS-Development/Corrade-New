///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Net;

namespace wasSharp
{
    public class Web
    {
        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC3986 URI Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC3986 escaped string</returns>
        public static string wasURIEscapeDataString(string data)
        {
            // Uri.EscapeDataString can only handle 32766 characters at a time
            return string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766)
                .Select(o => Uri.EscapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - (o*32766)))))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>URI unescapes an RFC3986 URI escaped string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>the resulting string</returns>
        public static string wasURIUnescapeDataString(string data)
        {
            // Uri.UnescapeDataString can only handle 32766 characters at a time
            return string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766)
                .Select(o => Uri.UnescapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - (o*32766)))))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC1738 escaped string</returns>
        public static string wasURLEscapeDataString(string data)
        {
            return WebUtility.UrlEncode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Unescape a string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>an RFC1738 unescaped string</returns>
        public static string wasURLUnescapeDataString(string data)
        {
            return WebUtility.UrlDecode(data);
        }
    }
}