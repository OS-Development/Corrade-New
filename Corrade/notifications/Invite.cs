///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> invite =
                (corradeNotificationParameters, notificationData) =>
                {
                    InstantMessageEventArgs notificationGroupInviteEventArgs =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationGroupInviteEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    IEnumerable<string> name = wasOpenMetaverse.Helpers.GetAvatarNames(notificationGroupInviteEventArgs.IM.FromAgentName);
                    if (name != null)
                    {
                        List<string> fullName = new List<string>(name);
                        if (fullName.Count.Equals(2))
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                fullName.First());
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                fullName.Last());

                            UUID agentUUID = UUID.Zero;
                            if (Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                            {
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                                    agentUUID.ToString());
                            }
                        }
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.GROUP),
                        notificationGroupInviteEventArgs.IM.FromAgentID.ToString());
                    lock (GroupInviteLock)
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                            GroupInvites.AsParallel().FirstOrDefault(
                                p => p.Session.Equals(notificationGroupInviteEventArgs.IM.IMSessionID))
                                .Group);
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION),
                        notificationGroupInviteEventArgs.IM.IMSessionID.ToString());
                };
        }
    }
}