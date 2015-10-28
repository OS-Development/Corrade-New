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
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> estateteleportusershome =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    string avatars =
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.AVATARS)),
                                corradeCommandParameters.Message));
                    // if no avatars were specified, teleport all users home
                    if (string.IsNullOrEmpty(avatars))
                    {
                        Client.Estate.TeleportHomeAllUsers();
                        return;
                    }
                    HashSet<string> data = new HashSet<string>();
                    Parallel.ForEach(CSV.wasCSVToEnumerable(avatars).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o =>
                        {
                            UUID agentUUID;
                            switch (!UUID.TryParse(o, out agentUUID))
                            {
                                case true:
                                    List<string> fullName = new List<string>(GetAvatarNames(o));
                                    switch (
                                        !AgentNameToUUID(fullName.First(), fullName.Last(),
                                            corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout, ref agentUUID))
                                    {
                                        case true: // the name could not be resolved to an UUID so add it to the return
                                            data.Add(o);
                                            break;
                                        default: // the name could be resolved so send them home
                                            Client.Estate.TeleportHomeUser(agentUUID);
                                            break;
                                    }
                                    break;
                                default:
                                    Client.Estate.TeleportHomeUser(agentUUID);
                                    break;
                            }
                        });
                    if (data.Any())
                    {
                        result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                            CSV.wasEnumerableToCSV(data));
                    }
                };
        }
    }
}