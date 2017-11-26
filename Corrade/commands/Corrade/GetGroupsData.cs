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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getgroupsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var data = new List<string>();
                    var LockObject = new object();
                    CSV.ToEnumerable(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message))).AsParallel().ForAll(o =>
                    {
                        UUID groupUUID;
                        if (!UUID.TryParse(o, out groupUUID) &&
                            !Resolvers.GroupNameToUUID(Client, o, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                new DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                            return;
                        var dataGroup = new Group();
                        if (
                            !Services.RequestGroup(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                ref dataGroup))
                            throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                        var groupData = dataGroup.GetStructuredData(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message)));
                        lock (LockObject)
                        {
                            data.AddRange(groupData);
                        }
                    });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}