///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace wasSharp
{
    public static class IO
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Combine multiple paths.
        /// </summary>
        /// <param name="paths">an array of paths</param>
        /// <returns>a combined path</returns>
        public static string PathCombine(params string[] paths)
        {
            return paths.Aggregate((x, y) => Path.Combine(x, y));
        }

        /// <summary>
        ///     Strip characters that are incompatible with file names.
        /// </summary>
        /// <param name="fileName">the name of the file</param>
        /// <returns>a clean string</returns>
        private static string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars()
                .Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Splits a path using a separator and an escape character.
        /// </summary>
        /// <param name="path">the path to split</param>
        /// <param name="separator">the separator character</param>
        /// <param name="escape">the escape character</param>
        /// <returns>path parts</returns>
        public static IEnumerable<string> PathSplit(this string path, char separator, char? escape)
        {
            Stack<char> s = new Stack<char>();
            StringBuilder p = new StringBuilder();
            foreach (char c in path)
            {
                if (c == escape)
                {
                    s.Push(c);
                    continue;
                }
                if (c == separator)
                {
                    if (s.Count.Equals(0) || !s.Peek().Equals(escape))
                    {
                        yield return p.ToString();
                        p = new StringBuilder();
                        continue;
                    }
                    s.Pop();
                    while (!s.Count.Equals(0))
                    {
                        p.Append(s.Pop());
                    }
                    p.Append(c);
                    continue;
                }
                p.Append(c);
            }
            while (!s.Count.Equals(0))
            {
                p.Append(s.Pop());
            }
            yield return p.ToString();
        }
    }
}