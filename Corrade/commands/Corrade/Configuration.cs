///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Xml;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> configuration =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    Action action = wasGetEnumValueFromDescription<Action>(wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                            corradeCommandParameters.Message))
                        .ToLowerInvariant());

                    switch (action)
                    {
                        case Action.READ:
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                                        corradeConfiguration.Read(CORRADE_CONSTANTS.CONFIGURATION_FILE));
                                }
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_LOAD_CONFIGURATION);
                            }
                            break;
                        case Action.WRITE:
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    corradeConfiguration.Write(CORRADE_CONSTANTS.CONFIGURATION_FILE, wasKeyValueGet(
                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                                }
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_SAVE_CONFIGURATION);
                            }
                            break;
                        case Action.SET:
                        case Action.GET:
                            string path =
                                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PATH)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                            {
                                throw new ScriptException(ScriptError.NO_PATH_PROVIDED);
                            }
                            XmlDocument conf = new XmlDocument();
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    conf.LoadXml(corradeConfiguration.Read(CORRADE_CONSTANTS.CONFIGURATION_FILE));
                                }
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_LOAD_CONFIGURATION);
                            }
                            string data;
                            switch (action)
                            {
                                case Action.GET:
                                    try
                                    {
                                        data = conf.SelectSingleNode(path).InnerXml;
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.INVALID_XML_PATH);
                                    }
                                    if (!string.IsNullOrEmpty(data))
                                    {
                                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), data);
                                    }
                                    break;
                                case Action.SET:
                                    data =
                                        wasInput(
                                            wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                                corradeCommandParameters.Message));
                                    if (string.IsNullOrEmpty(data))
                                    {
                                        throw new ScriptException(ScriptError.NO_DATA_PROVIDED);
                                    }
                                    try
                                    {
                                        conf.SelectSingleNode(path).InnerXml = data;
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.INVALID_XML_PATH);
                                    }

                                    try
                                    {
                                        lock (ConfigurationFileLock)
                                        {
                                            corradeConfiguration.Write(CORRADE_CONSTANTS.CONFIGURATION_FILE, conf);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.UNABLE_TO_SAVE_CONFIGURATION);
                                    }
                                    break;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}