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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> ai =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    if (!corradeConfiguration.EnableSIML)
                        throw new Command.ScriptException(Enumerations.ScriptError.SIML_NOT_ENABLED);

                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.PROCESS:
                            var message =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(message))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_MESSAGE_PROVIDED);
                            string reply;
                            lock (SIMLBotLock)
                            {
                                reply = SynBot.Chat(message).BotMessage;
                            }
                            if (!string.IsNullOrEmpty(reply))
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), reply);
                            break;

                        case Enumerations.Action.REBUILD:
                            lock (SIMLBotLock)
                            {
                                SIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                                var SIMLPackage = Path.Combine(
                                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                                    SIML_BOT_CONSTANTS.PACKAGE_FILE);
                                if (File.Exists(SIMLPackage))
                                    try
                                    {
                                        File.Delete(SIMLPackage);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_REMOVE_SIML_PACKAGE_FILE);
                                    }
                                LoadChatBotFiles.Invoke();
                                SIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}