///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using wasSharp;

namespace Corrade.Threading
{
    public class Enumerations
    {
        /// <summary>
        ///     The type of threads managed by Corrade.
        /// </summary>
        public enum ThreadType : uint
        {
            [Reflection.NameAttribute("command")]
            COMMAND = 1,

            [Reflection.NameAttribute("rlv")]
            RLV = 2,

            [Reflection.NameAttribute("notification")]
            NOTIFICATION = 3,

            [Reflection.NameAttribute("im")]
            INSTANT_MESSAGE = 4,

            [Reflection.NameAttribute("log")]
            LOG = 5,

            [Reflection.NameAttribute("post")]
            POST = 6,

            [Reflection.NameAttribute("preload")]
            PRELOAD = 7,

            [Reflection.NameAttribute("horde")]
            HORDE = 8,

            [Reflection.NameAttribute("softban")]
            SOFTBAN = 9,

            [Reflection.NameAttribute("auxiliary")]
            AUXILIARY = 10
        }
    }
}
