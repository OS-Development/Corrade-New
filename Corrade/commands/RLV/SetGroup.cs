///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System;
using System.Linq;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> setgroup =
                (message, rule, senderUUID) =>
                {
                    if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE))
                    {
                        return;
                    }
                    UUID groupUUID;
                    if (!UUID.TryParse(rule.Option, out groupUUID))
                    {
                        return;
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        return;
                    var currentGroup =
                        currentGroups.AsParallel().FirstOrDefault(o => o.Equals(groupUUID));
                    if (!currentGroup.Equals(UUID.Zero))
                    {
                        Client.Groups.ActivateGroup(groupUUID);
                    }
                };
        }
    }
}
