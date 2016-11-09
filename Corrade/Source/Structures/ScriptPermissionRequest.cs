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
    ///     A structure for script permission requests.
    /// </summary>
    public class ScriptPermissionRequest
    {
        [Reflection.NameAttribute("agent")] public Agent Agent;
        [Reflection.NameAttribute("item")] public UUID Item;
        [Reflection.NameAttribute("name")] public string Name;
        [Reflection.NameAttribute("permissions")] public ScriptPermission Permissions;
        [Reflection.NameAttribute("region")] public string Region;
        [Reflection.NameAttribute("task")] public UUID Task;
    }
}