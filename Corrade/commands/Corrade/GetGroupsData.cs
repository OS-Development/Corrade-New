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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getgroupsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> data = new List<string>();
                    object LockObject = new object();
                    CSV.ToEnumerable(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                            corradeCommandParameters.Message))).AsParallel().ForAll(o =>
                            {
                                UUID groupUUID;
                                if (!UUID.TryParse(o, out groupUUID) &&
                                    !Resolvers.GroupNameToUUID(Client, o, corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                    return;
                                Group dataGroup = new Group();
                                if (
                                    !Services.RequestGroup(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                        ref dataGroup))
                                {
                                    throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                                }
                                IEnumerable<string> groupData = GetStructuredData(dataGroup,
                                    wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)));
                                lock (LockObject)
                                {
                                    data.AddRange(groupData);
                                }
                            });
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}