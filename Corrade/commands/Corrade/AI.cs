///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AIMLbot;
using CorradeConfiguration;
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
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.wasGetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.PROCESS:
                            string request =
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(request))
                            {
                                throw new ScriptException(ScriptError.NO_MESSAGE_PROVIDED);
                            }
                            if (AIMLBot.isAcceptingUserInput)
                            {
                                lock (AIMLBotLock)
                                {
                                    result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                                        AIMLBot.Chat(new Request(request, AIMLBotUser, AIMLBot)).Output);
                                }
                            }
                            break;
                        case Action.ENABLE:
                            lock (AIMLBotLock)
                            {
                                switch (!AIMLBotBrainCompiled)
                                {
                                    case true:
                                        new Thread(
                                            () =>
                                            {
                                                lock (AIMLBotLock)
                                                {
                                                    LoadChatBotFiles.Invoke();
                                                    AIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                                                }
                                            })
                                        {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
                                        break;
                                    default:
                                        AIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                                        AIMLBot.isAcceptingUserInput = true;
                                        break;
                                }
                            }
                            break;
                        case Action.DISABLE:
                            lock (AIMLBotLock)
                            {
                                AIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                                AIMLBot.isAcceptingUserInput = false;
                            }
                            break;
                        case Action.REBUILD:
                            lock (AIMLBotLock)
                            {
                                AIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                                string AIMLBotBrain =
                                    wasPathCombine(
                                        Directory.GetCurrentDirectory(), AIML_BOT_CONSTANTS.DIRECTORY,
                                        AIML_BOT_CONSTANTS.BRAIN.DIRECTORY, AIML_BOT_CONSTANTS.BRAIN_FILE);
                                if (File.Exists(AIMLBotBrain))
                                {
                                    try
                                    {
                                        File.Delete(AIMLBotBrain);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_REMOVE_BRAIN_FILE);
                                    }
                                }
                                LoadChatBotFiles.Invoke();
                                AIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}