///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using Corrade.Constants;
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
                removeconfigurationgroup = (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

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
                    Locks.ClientInstanceConfigurationLock.EnterWriteLock();
                    if (corradeConfiguration.Groups.RemoveWhere(
                        o => string.Equals(groupName, o.Name, StringComparison.OrdinalIgnoreCase) &&
                             groupUUID.Equals(o.UUID)).Equals(0))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_CONFIGURED);
                    Locks.ClientInstanceConfigurationLock.ExitWriteLock();
                    lock (ConfigurationFileLock)
                    {
                        try
                        {
                            using (var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                            {
                                corradeConfiguration.Save(fileStream, ref corradeConfiguration);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                        }
                    }
                };
        }
    }
}