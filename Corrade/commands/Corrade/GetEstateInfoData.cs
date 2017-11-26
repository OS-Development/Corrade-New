///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
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
                getestateinfodata =
                    (corradeCommandParameters, result) =>
                    {
                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        var EstateUpdateInfoReplyEvent = new ManualResetEventSlim(false);
                        EstateUpdateInfoReplyEventArgs estateInfo = null;
                        EventHandler<EstateUpdateInfoReplyEventArgs> EstateUpdateInfoReplyHandler = (sender, args) =>
                        {
                            estateInfo = args;
                            EstateUpdateInfoReplyEvent.Set();
                        };
                        Locks.ClientInstanceEstateLock.EnterWriteLock();
                        Client.Estate.EstateUpdateInfoReply += EstateUpdateInfoReplyHandler;
                        Client.Estate.RequestInfo();
                        if (!EstateUpdateInfoReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Estate.EstateUpdateInfoReply -= EstateUpdateInfoReplyHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_INFO);
                        }
                        Client.Estate.EstateUpdateInfoReply -= EstateUpdateInfoReplyHandler;
                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                        var data =
                            estateInfo.GetStructuredData(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message))).ToList();
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}