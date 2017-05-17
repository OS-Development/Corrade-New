///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System.Xml.Serialization;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     A structure for teleport lures.
    /// </summary>
    [Reflection.NameAttribute("teleportlure")]
    [XmlRoot(ElementName = "TeleportLure")]
    public struct TeleportLure
    {
        [Reflection.NameAttribute("agent")]
        [XmlElement(ElementName = "Agent")]
        public Agent Agent;

        [Reflection.NameAttribute("session")]
        [XmlElement(ElementName = "Session")]
        public UUID Session;
    }
}
