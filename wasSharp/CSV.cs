///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace wasSharp
{
    public static class CSV
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a list of strings to a comma-separated values string.
        /// </summary>
        /// <returns>a commma-separated list of values</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static string FromEnumerable(IEnumerable<string> input)
        {
            return string.Join(",",
                input
                    .Select(o => o.Replace("\"", "\"\""))
                    .Select(o => o.IndexOfAny(new[] {'"', ' ', ',', '\r', '\n'}).Equals(-1) ? o : "\"" + o + "\""));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a dictionary of strings to a comma-separated values string.
        /// </summary>
        /// <returns>a commma-separated list of values</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static string FromDictionary<K, V>(Dictionary<K, V> input)
        {
            return string.Join(",", input.Keys.Select(o => o.ToString()).Zip(input.Values.Select(o => o.ToString()),
                (o, p) =>
                    string.Join(",",
                        o.Replace("\"", "\"\"").IndexOfAny(new[] {'"', ' ', ',', '\r', '\n'}).Equals(-1)
                            ? o
                            : "\"" + o + "\"",
                        p.Replace("\"", "\"\"").IndexOfAny(new[] {'"', ' ', ',', '\r', '\n'}).Equals(-1)
                            ? p
                            : "\"" + p + "\"")));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts successive comma-separated values to key-value pairs.
        /// </summary>
        /// <returns>key-value pairs of successive comma-separate values</returns>
        public static IEnumerable<KeyValuePair<string, string>> ToKeyValue(string input)
        {
            return ToEnumerable(input).AsParallel().Select((o, p) => new {o, p})
                .GroupBy(q => q.p/2, q => q.o)
                .Select(o => o.ToArray())
                .TakeWhile(o => o.Length%2 == 0)
                .Where(o => !string.IsNullOrEmpty(o[0]) || !string.IsNullOrEmpty(o[1]))
                .ToDictionary(o => o[0], p => p[1]).Select(o => o);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a generic key value pair to a CSV.
        /// </summary>
        /// <returns>a commma-separated list of values</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static string FromKeyValue<K, V>(KeyValuePair<K, V> input)
        {
            var key = input.Key.ToString();
            var value = input.Value.ToString();

            return string.Join(",", key
                .Replace("\"", "\"\"").IndexOfAny(new[] {'"', ' ', ',', '\r', '\n'}).Equals(-1)
                ? key
                : "\"" + key + "\"", value
                    .Replace("\"", "\"\"")
                    .IndexOfAny(new[] {'"', ' ', ',', '\r', '\n'})
                    .Equals(-1)
                    ? value
                    : "\"" + value + "\"");
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
        public static IEnumerable<string> ToEnumerable(string csv)
        {
            var s = new Stack<char>();
            var m = new StringBuilder();
            for (var i = 0; i < csv.Length; ++i)
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