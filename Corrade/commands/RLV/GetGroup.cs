///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System;
using System.Globalization;
using System.Linq;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getgroup =
                (message, rule, senderUUID) =>
                {
                    int channel;
                    if (!int.TryParse(rule.Param, NumberStyles.Integer, Utils.EnUsCulture, out channel) || channel < 1)
                    {
                        return;
                    }
                    var groupUUID = Client.Self.ActiveGroup;
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        return;
                    var groupName = string.Empty;
                    if (
                        !Resolvers.GroupUUIDToName(Client,
                            currentGroups.AsParallel().FirstOrDefault(o => o.Equals(groupUUID)),
                            corradeConfiguration.ServicesTimeout, ref groupName))
                    {
                        return;
                    }
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Chat(groupName, channel, ChatType.Normal);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}
