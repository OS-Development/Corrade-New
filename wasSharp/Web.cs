///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Text;

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
            //return HttpUtility.UrlEncode(data);
            StringBuilder result = new StringBuilder();

            char[] hex = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'};

            foreach (char c in data)
            {
                switch (c)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                    case 'g':
                    case 'h':
                    case 'i':
                    case 'j':
                    case 'k':
                    case 'l':
                    case 'm':
                    case 'n':
                    case 'o':
                    case 'p':
                    case 'q':
                    case 'r':
                    case 's':
                    case 't':
                    case 'u':
                    case 'v':
                    case 'w':
                    case 'x':
                    case 'y':
                    case 'z':
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                    case 'G':
                    case 'H':
                    case 'I':
                    case 'J':
                    case 'K':
                    case 'L':
                    case 'M':
                    case 'N':
                    case 'O':
                    case 'P':
                    case 'Q':
                    case 'R':
                    case 'S':
                    case 'T':
                    case 'U':
                    case 'V':
                    case 'W':
                    case 'X':
                    case 'Y':
                    case 'Z':
                    case '!':
                    case '\'':
                    case '(':
                    case ')':
                    case '*':
                    case '-':
                    case '.':
                    case '_':
                        result.Append(c);
                        break;
                    case ' ':
                        result.Append('+');
                        break;
                    default:
                        StringBuilder uCode = new StringBuilder();
                        foreach (var b in Encoding.UTF8.GetBytes(new[] {c}))
                        {
                            uCode.Append('%');
                            uCode.Append(hex[b >> 4]);
                            uCode.Append(hex[b & 0x0F]);
                        }
                        result.Append(uCode);
                        break;
                }
            }

            return result.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Unescape a string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>an RFC1738 unescaped string</returns>
        public static string wasURLUnescapeDataString(string data)
        {
            //return HttpUtility.UrlDecode(data);
            StringBuilder result = new StringBuilder();

            int c;

            Func<byte, int> GetInt = o =>
            {
                switch ((char) o)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        return o - '0';
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                        return o - 'a' + 10;
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                        return o - 'A' + 10;
                    default:
                        return -1;
                }
            };

            Func<string, int, int, int> GetCharString = (s, o, l) =>
            {
                int v = 0;
                int e = l + o;
                for (int i = o; i < e; ++i)
                {
                    c = GetInt((byte) s[i]);
                    if (c.Equals(-1)) return -1;
                    v = (v << 4) + c;
                }
                return v;
            };

            using (MemoryStream bytes = new MemoryStream())
            {
                for (int x = 0; x < data.Length; ++x)
                {
                    if (data[x].Equals('%') && !data[x + 1].Equals('%') && x + 2 < data.Length)
                    {
                        c = GetCharString(data, x + 1, 2);
                        switch (c)
                        {
                            case -1:
                                result.Append('%');
                                break;
                            default:
                                bytes.WriteByte((byte) c);
                                x += 2;
                                break;
                        }
                        continue;
                    }

                    if (!bytes.Length.Equals(0))
                    {
                        result.Append(Encoding.UTF8.GetChars(bytes.ToArray()));
                        bytes.SetLength(0);
                    }

                    switch (data[x].Equals('+'))
                    {
                        case true:
                            result.Append(' ');
                            break;
                        default:
                            result.Append(data[x]);
                            break;
                    }
                }

                if (!bytes.Length.Equals(0))
                {
                    result.Append(Encoding.UTF8.GetChars(bytes.ToArray()));
                    bytes.SetLength(0);
                }
            }

            return result.ToString();
        }
    }
}