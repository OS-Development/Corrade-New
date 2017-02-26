///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using System.Collections.Generic;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;
using System.Threading;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> agentaccess =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int)Configuration.Permissions.Grooming))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.GET:
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), Client.Self.AgentAccess);
                            break;
                        case Enumerations.Action.SET:
                            var access = wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACCESS)),
                                        corradeCommandParameters.Message));
                            switch (access)
                            {
                                case wasOpenMetaverse.Constants.MATURITY.PARENTAL_GUIDANCE:
                                case wasOpenMetaverse.Constants.MATURITY.MATURE:
                                case wasOpenMetaverse.Constants.MATURITY.ADULT:
                                    var succeeded = true;
                                    var AgentAccessSetEvent = new ManualResetEvent(false);
                                    lock(Locks.ClientInstanceSelfLock)
                                    {
                                        Client.Self.SetAgentAccess(access, (o) =>
                                        {
                                            succeeded = o.Success;
                                            if (String.Equals(o.NewLevel, access))
                                                succeeded = false;
                                            AgentAccessSetEvent.Set();
                                        });
                                    }
                                    if (!AgentAccessSetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_SET_AGENT_ACCESS);
                                    }
                                    break;
                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_AGENT_ACCESSS);
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}