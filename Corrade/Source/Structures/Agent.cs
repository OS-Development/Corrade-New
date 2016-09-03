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
    ///     Agent Structure.
    /// </summary>
    public struct Agent
    {
        [Reflection.NameAttribute("firstname")] public string FirstName;
        [Reflection.NameAttribute("lastname")] public string LastName;
        [Reflection.NameAttribute("uuid")] public UUID UUID;
    }
}