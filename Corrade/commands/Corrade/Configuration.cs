///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Corrade.Constants;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> configuration =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                            corradeCommandParameters.Message))
                    );

                    switch (action)
                    {
                        case Enumerations.Action.READ:
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    using (
                                        var streamReader = new StreamReader(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                            Encoding.UTF8))
                                    {
                                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                            streamReader.ReadToEnd());
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .UNABLE_TO_LOAD_CONFIGURATION);
                            }
                            break;

                        case Enumerations.Action.WRITE:
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    using (
                                        var streamWriter = new StreamWriter(CORRADE_CONSTANTS.CONFIGURATION_FILE, false,
                                            Encoding.UTF8))
                                    {
                                        streamWriter.Write(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .UNABLE_TO_SAVE_CONFIGURATION);
                            }
                            break;

                        case Enumerations.Action.SET:
                        case Enumerations.Action.GET:
                            var path =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            var conf = new XmlDocument();
                            try
                            {
                                lock (ConfigurationFileLock)
                                {
                                    using (
                                        var streamReader = new StreamReader(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                            Encoding.UTF8))
                                    {
                                        conf.LoadXml(streamReader.ReadToEnd());
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .UNABLE_TO_LOAD_CONFIGURATION);
                            }
                            string data;
                            switch (action)
                            {
                                case Enumerations.Action.GET:
                                    try
                                    {
                                        data = conf.SelectSingleNode(path).InnerXml;
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_XML_PATH);
                                    }
                                    if (!string.IsNullOrEmpty(data))
                                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), data);
                                    break;

                                case Enumerations.Action.SET:
                                    data =
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message));
                                    if (string.IsNullOrEmpty(data))
                                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                                    try
                                    {
                                        conf.SelectSingleNode(path).InnerXml = data;
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_XML_PATH);
                                    }

                                    try
                                    {
                                        lock (ConfigurationFileLock)
                                        {
                                            using (
                                                var streamWriter = new StreamWriter(
                                                    CORRADE_CONSTANTS.CONFIGURATION_FILE, false, Encoding.UTF8))
                                            {
                                                conf.Save(streamWriter);
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.UNABLE_TO_SAVE_CONFIGURATION);
                                    }
                                    break;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}