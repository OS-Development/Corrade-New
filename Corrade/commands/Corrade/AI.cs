///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using CorradeConfiguration;
using Syn.Bot;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> ai =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.PROCESS:
                            var request =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(request))
                            {
                                throw new ScriptException(ScriptError.NO_MESSAGE_PROVIDED);
                            }
                            lock (SIMLBotLock)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    SynBot.Chat(request).BotMessage);
                            }
                            break;
                        case Action.REBUILD:
                            lock (SIMLBotLock)
                            {
                                SIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                                var SIMLPackage = Path.Combine(
                                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                                    SIML_BOT_CONSTANTS.PACKAGE_FILE);
                                if (File.Exists(SIMLPackage))
                                {
                                    try
                                    {
                                        File.Delete(SIMLPackage);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_REMOVE_SIML_PACKAGE_FILE);
                                    }
                                }
                                LoadChatBotFiles.Invoke();
                                SIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}