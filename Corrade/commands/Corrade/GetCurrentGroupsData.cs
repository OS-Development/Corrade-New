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
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getcurrentgroupsdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Group))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var currentGroups = Enumerable.Empty<UUID>();
                        if (
                            !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                                ref currentGroups))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                        var data = new List<string>();
                        var LockObject = new object();
                        currentGroups.AsParallel().ForAll(o =>
                        {
                            var dataGroup = new Group();
                            if (!Services.RequestGroup(Client, o, corradeConfiguration.ServicesTimeout, ref dataGroup))
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