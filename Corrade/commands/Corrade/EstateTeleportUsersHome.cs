///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                estateteleportusershome =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        lock (Locks.ClientInstanceNetworkLock)
                        {
                            if (!Client.Network.CurrentSim.IsEstateManager)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                            }
                        }
                        var avatars =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                    corradeCommandParameters.Message));
                        // if no avatars were specified, teleport all users home
                        if (string.IsNullOrEmpty(avatars))
                        {
                            Client.Estate.TeleportHomeAllUsers();
                            return;
                        }
                        var data = new HashSet<string>();
                        CSV.ToEnumerable(avatars).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            UUID agentUUID;
                            switch (!UUID.TryParse(o, out agentUUID))
                            {
                                case true:
                                    var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                    switch (fullName == null ||
                                            !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                                ref agentUUID))
                                    {
                                        case true: // the name could not be resolved to an UUID so add it to the return
                                            data.Add(o);
                                            break;
                                        default: // the name could be resolved so send them home
                                            lock (Locks.ClientInstanceEstateLock)
                                            {
                                                Client.Estate.TeleportHomeUser(agentUUID);
                                            }
                                            break;
                                    }
                                    break;
                                default:
                                    lock (Locks.ClientInstanceEstateLock)
                                    {
                                        Client.Estate.TeleportHomeUser(agentUUID);
                                    }
                                    break;
                            }
                        });
                        if (data.Any())
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                        }
                    };
        }
    }
}