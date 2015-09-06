///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> setgroup = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                UUID groupUUID;
                if (!UUID.TryParse(rule.Option, out groupUUID))
                {
                    return;
                }
                IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                if (
                    !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                        ref currentGroups))
                    return;
                UUID currentGroup =
                    currentGroups.ToList().FirstOrDefault(o => o.Equals(groupUUID));
                if (!currentGroup.Equals(UUID.Zero))
                {
                    Client.Groups.ActivateGroup(groupUUID);
                }
            };
        }
    }
}