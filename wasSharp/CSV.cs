///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wasSharp
{
    public class CSV
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a list of string to a comma-separated values string.
        /// </summary>
        /// <param name="l">a list of strings</param>
        /// <returns>a commma-separated list of values</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static string wasEnumerableToCSV(IEnumerable<string> l)
        {
            string[] csv = l.Select(o => o).ToArray();
            char[] escapeCharacters = {'"', ' ', ',', '\r', '\n'};
            Parallel.ForEach(csv.Select((v, i) => new {i, v}), o =>
            {
                string cell = o.v.Replace("\"", "\"\"");

                switch (cell.IndexOfAny(escapeCharacters))
                {
                    case -1:
                        csv[o.i] = cell;
                        break;
                    default:
                        csv[o.i] = "\"" + cell + "\"";
                        break;
                }
            });
            return string.Join(",", csv);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a comma-separated list of values to a list of strings.
        /// </summary>
        /// <param name="csv">a comma-separated list of values</param>
        /// <returns>a list of strings</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static IEnumerable<string> wasCSVToEnumerable(string csv)
        {
            Stack<char> s = new Stack<char>();
            StringBuilder m = new StringBuilder();
            for (int i = 0; i < csv.Length; ++i)
            {
                switch (csv[i])
                {
                    case ',':
                        if (!s.Any() || !s.Peek().Equals('"'))
                        {
                            yield return m.ToString();
                            m = new StringBuilder();
                            continue;
                        }
                        m.Append(csv[i]);
                        continue;
                    case '"':
                        if (i + 1 < csv.Length && csv[i].Equals(csv[i + 1]))
                        {
                            m.Append(csv[i]);
                            ++i;
                            continue;
                        }
                        if (!s.Any() || !s.Peek().Equals(csv[i]))
                        {
                            s.Push(csv[i]);
                            continue;
                        }
                        s.Pop();
                        continue;
                }
                m.Append(csv[i]);
            }

            yield return m.ToString();
        }
    }
}