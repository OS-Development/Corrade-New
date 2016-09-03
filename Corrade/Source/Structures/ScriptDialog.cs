///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     A structure for script dialogs.
    /// </summary>
    public struct ScriptDialog
    {
        [Reflection.NameAttribute("agent")] public Agent Agent;
        [Reflection.NameAttribute("button")] public List<string> Button;
        [Reflection.NameAttribute("channel")] public int Channel;
        [Reflection.NameAttribute("item")] public UUID Item;
        [Reflection.NameAttribute("message")] public string Message;
        [Reflection.NameAttribute("name")] public string Name;
    }
}