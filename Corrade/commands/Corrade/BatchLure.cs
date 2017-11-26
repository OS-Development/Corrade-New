///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> batchlure =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var data = new HashSet<string>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                    corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            UUID agentUUID;
                            if (!UUID.TryParse(o, out agentUUID))
                            {
                                var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                if (!fullName.Any() ||
                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                        corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                                {
                                    // Add all the unrecognized agents to the returned list.
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(o))
                                            data.Add(o);
                                    }
                                    return;
                                }
                            }
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.SendTeleportLure(agentUUID,
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message)));
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                        });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}