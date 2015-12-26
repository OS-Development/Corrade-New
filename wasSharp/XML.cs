///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace wasSharp
{
    public static class XML
    {
        private static readonly Func<string, bool> directIsSafeXML =
            ((Expression<Func<string, bool>>)
                (data =>
                    Regex.Replace(data,
                        @"(" + string.Join("|", @"&amp;", @"&lt;", @"&gt;", @"&quot;", @"&apos;") + @")",
                        @"", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                        .IndexOfAny(new[] {'&', '<', '>', '"', '\''})
                        .Equals(-1))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Unescapes a string used in XML.
        /// </summary>
        /// <param name="s">the string to unescape</param>
        /// <returns>an XML unescaped string</returns>
        public static string UnescapeXML(string s)
        {
            Queue<char> t = new Queue<char>();
            StringBuilder m = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&':
                        if (!t.Count.Equals(0))
                        {
                            m.Append(string.Join("", t.ToArray()));
                            t.Clear();
                        }
                        t.Enqueue(c);
                        break;
                    case ';':
                        if (!t.Count.Equals(0))
                        {
                            t.Enqueue(c);
                            string special = string.Join("", t.ToArray());
                            switch (special)
                            {
                                case "&apos;":
                                    m.Append('\'');
                                    break;
                                case "&quot;":
                                    m.Append('"');
                                    break;
                                case "&gt;":
                                    m.Append('>');
                                    break;
                                case "&lt;":
                                    m.Append('<');
                                    break;
                                case "&amp;":
                                    m.Append('&');
                                    break;
                                default: // Unrecognized escape sequence
                                    m.Append(special);
                                    break;
                            }
                            t.Clear();
                            break;
                        }
                        m.Append(c);
                        break;
                    default:
                        if (!t.Count.Equals(0))
                        {
                            t.Enqueue(c);
                            if (t.Count >= 6)
                            {
                                m.Append(string.Join("", t.ToArray()));
                                t.Clear();
                            }
                            break;
                        }
                        m.Append(c);
                        break;
                }
            }
            return m.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Escapes a string to be used in XML.
        /// </summary>
        /// <param name="s">the string to escape</param>
        /// <returns>an XML escaped string</returns>
        public static string EscapeXML(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            string[] result = new string[s.Length];
            Parallel.ForEach(Enumerable.Range(0, s.Length), o =>
            {
                switch (s[o])
                {
                    case '&':
                        result[o] = @"&amp;";
                        break;
                    case '<':
                        result[o] = @"&lt;";
                        break;
                    case '>':
                        result[o] = @"&gt;";
                        break;
                    case '"':
                        result[o] = @"&quot;";
                        break;
                    case '\'':
                        result[o] = @"&apos;";
                        break;
                    default:
                        result[o] = s[o].ToString();
                        break;
                }
            });
            return string.Join("", result);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether a string is safe to use in XML
        /// </summary>
        /// <param name="s">the string to check</param>
        /// <returns>true in case the string is safe</returns>
        public static bool IsSafeXML(string data)
        {
            return directIsSafeXML(data);
        }
    }
}