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
    ///     A structure for group notices.
    /// </summary>
    public struct GroupNotice
    {
        [Reflection.NameAttribute("agent")]
        public Agent Agent;

        [Reflection.NameAttribute("asset")]
        public AssetType Asset;

        [Reflection.NameAttribute("attachment")]
        public bool Attachment;

        [Reflection.NameAttribute("folder")]
        public UUID Folder;

        [Reflection.NameAttribute("group")]
        public Group Group;

        [Reflection.NameAttribute("message")]
        public string Message;

        [Reflection.NameAttribute("session")]
        public UUID Session;

        [Reflection.NameAttribute("subject")]
        public string Subject;
    }
}
