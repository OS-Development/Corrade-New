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