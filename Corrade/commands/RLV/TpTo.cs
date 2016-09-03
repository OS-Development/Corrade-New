///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> tpto = (message, rule, senderUUID) =>
            {
                var coordinates = rule.Option.Split('/');
                if (!coordinates.Length.Equals(3))
                {
                    return;
                }
                float globalX;
                if (!float.TryParse(coordinates[0], out globalX))
                {
                    return;
                }
                float globalY;
                if (!float.TryParse(coordinates[1], out globalY))
                {
                    return;
                }
                float altitude;
                if (!float.TryParse(coordinates[2], out altitude))
                {
                    return;
                }
                float localX, localY;
                var handle = OpenMetaverse.Helpers.GlobalPosToRegionHandle(globalX, globalY, out localX, out localY);
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.RequestTeleport(handle, new Vector3(localX, localY, altitude));
                }
            };
        }
    }
}