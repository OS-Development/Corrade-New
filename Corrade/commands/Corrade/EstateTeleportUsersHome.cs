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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                estateteleportusershome =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                        }
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
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
                                    switch (!fullName.Any() ||
                                            !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                                ref agentUUID))
                                    {
                                        case true: // the name could not be resolved to an UUID so add it to the return
                                            data.Add(o);
                                            break;

                                        default: // the name could be resolved so send them home
                                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                                            Client.Estate.TeleportHomeUser(agentUUID);
                                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                                            break;
                                    }
                                    break;

                                default:
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.TeleportHomeUser(agentUUID);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;
                            }
                        });
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}