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
            public static Action<string, RLVRule, UUID> getgroup = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                UUID groupUUID = Client.Self.ActiveGroup;
                IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                if (
                    !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                        ref currentGroups))
                    return;
                string groupName = string.Empty;
                if (
                    !GroupUUIDToName(currentGroups.AsParallel().FirstOrDefault(o => o.Equals(groupUUID)),
                        corradeConfiguration.ServicesTimeout, ref groupName))
                {
                    return;
                }
                Client.Self.Chat(groupName, channel, ChatType.Normal);
            };
        }
    }
}