///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OpenMetaverse;

namespace Corrade.Structures
{
    /// <summary>
    ///     An element from the callback queue waiting to be dispatched.
    /// </summary>
    public struct CallbackQueueElement
    {
        public IEnumerable<KeyValuePair<string, string>> message;
        public string URL;
        public UUID GroupUUID;
    }
}
