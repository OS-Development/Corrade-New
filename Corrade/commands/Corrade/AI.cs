using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AIMLbot;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> ai = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Talk))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                        .ToLowerInvariant()))
                {
                    case Action.PROCESS:
                        string request =
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE)),
                                    message));
                        if (string.IsNullOrEmpty(request))
                        {
                            throw new ScriptException(ScriptError.NO_MESSAGE_PROVIDED);
                        }
                        if (AIMLBot.isAcceptingUserInput)
                        {
                            lock (AIMLBotLock)
                            {
                                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
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