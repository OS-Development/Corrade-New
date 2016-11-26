///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Corrade.Constants;
using CorradeConfiguration;
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
                removeconfigurationgroup = (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    var target = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));

                    var groupName = string.Empty;
                    var groupUUID = UUID.Zero;
                    switch (UUID.TryParse(target, out groupUUID))
                    {
                        case true:
                            if (!Resolvers.GroupUUIDToName(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                ref groupName))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;
                        default:
                            if (!Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref groupUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;
                    }

                    if (corradeConfiguration.Groups.RemoveWhere(
                        o => Strings.StringEquals(groupName, o.Name) && groupUUID.Equals(o.UUID)).Equals(0))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_CONFIGURED);

                    lock (ConfigurationFileLock)
                    {
                        corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                    }
                };
        }
    }
}