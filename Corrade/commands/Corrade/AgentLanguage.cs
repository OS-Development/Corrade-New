///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;
using System.Globalization;
using System.Linq;
using System.IO;
using Corrade.Constants;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> agentlanguage =
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
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), Client.Self.AgentStateStatus.Language);
                            break;
                        case Enumerations.Action.SET:
                            var language = wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LANGUAGE)),
                                        corradeCommandParameters.Message));
                            if (!CultureInfo.GetCultures(CultureTypes.AllCultures).AsParallel().Where(o => !(o.CultureTypes & CultureTypes.UserCustomCulture).Equals(CultureTypes.UserCustomCulture)).Any(o => Strings.StringEquals(o.TwoLetterISOLanguageName, language, StringComparison.Ordinal)))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_LANGUAGE);
                            }
                            bool isPublic;
                            if (
                                !bool.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PUBLIC)),
                                        corradeCommandParameters.Message)),
                                    out isPublic))
                            {
                                isPublic = true;
                            }
                            lock(Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.UpdateAgentLanguage(language, isPublic);
                            }
                            lock(Locks.ClientInstanceConfigurationLock)
                            {
                                corradeConfiguration.ClientLanguage = language;
                                corradeConfiguration.AdvertiseClientLanguage = isPublic;
                            }
                            lock (ConfigurationFileLock)
                            {
                                try
                                {
                                    using (var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                                    {
                                        corradeConfiguration.Save(fileStream, ref corradeConfiguration);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                                }
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}