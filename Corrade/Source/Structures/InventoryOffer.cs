///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Threading;
using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     A structure for group notices.
    /// </summary>
    public class InventoryOffer
    {
        [Reflection.NameAttribute("args")] public InventoryObjectOfferedEventArgs Args;
        [Reflection.NameAttribute("name")] public string Name;
        [Reflection.NameAttribute("event")] public ManualResetEvent Event;
    }
}