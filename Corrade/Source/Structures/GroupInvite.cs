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
    ///     A structure for group invites.
    /// </summary>
    public struct GroupInvite
    {
        [Reflection.NameAttribute("agent")] public Agent Agent;
        [Reflection.NameAttribute("fee")] public int Fee;
        [Reflection.NameAttribute("group")] public string Group;
        [Reflection.NameAttribute("session")] public UUID Session;
    }
}