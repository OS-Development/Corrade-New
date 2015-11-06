///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Linq;

namespace wasSharp
{
    public class IO
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
            return paths.Any()
                ? paths.Length < 2
                    ? paths[0]
                    : Path.Combine(Path.Combine(paths[0], paths[1]), PathCombine(paths.Skip(2).ToArray()))
                : string.Empty;
        }
    }
}