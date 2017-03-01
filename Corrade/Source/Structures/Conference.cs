///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     A structure for conferences.
    /// </summary>
    public struct Conference
    {
        [Reflection.NameAttribute("name")]
        public string Name;

        [Reflection.NameAttribute("session")]
        public UUID Session;

        [Reflection.NameAttribute("restored")]
        public bool Restored;
    }
}
