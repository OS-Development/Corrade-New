///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace wasSharp
{
    public static class IO
    {
#if !__MonoCS__
        private static readonly Func<string[], string> directPathCombine =
            ((Expression<Func<string[], string>>) (data =>
                data.Aggregate((x, y) => Path.Combine(x, y))))
                .Compile();
#endif

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
#if !__MonoCS__
            return directPathCombine(paths);
#else
            return paths.Aggregate((x, y) => Path.Combine(x, y));
#endif
        }
    }
}