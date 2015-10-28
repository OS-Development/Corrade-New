///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getcommand =
                (corradeCommandParameters, result) =>
                {
                    string name =
                        wasInput(KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                    }
                    IsCorradeCommandAttribute isCommandAttribute =
                        Reflection.wasGetAttributeFromEnumValue<IsCorradeCommandAttribute>(
                            Reflection.wasGetEnumValueFromName<ScriptKeys>(name));
                    if (isCommandAttribute == null || isCommandAttribute.IsCorradeCorradeCommand.Equals(false))
                    {
                        throw new ScriptException(ScriptError.COMMAND_NOT_FOUND);
                    }
                    CommandPermissionMaskAttribute commandPermissionMaskAttribute =
                        Reflection.wasGetAttributeFromEnumValue<CommandPermissionMaskAttribute>(
                            Reflection.wasGetEnumValueFromName<ScriptKeys>(name));
                    if (commandPermissionMaskAttribute == null)
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (!corradeCommandParameters.Group.Equals(default(Configuration.Group)))
                    {
                        case false:
                            throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    switch (
                        Reflection.wasGetEnumValueFromName<Entity>(
                            wasInput(
                                KeyValue.wasKeyValueGet(
                                    wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Entity.SYNTAX:
                            switch (
                                Reflection.wasGetEnumValueFromName<Type>(
                                    wasInput(
                                        KeyValue.wasKeyValueGet(
                                            wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)).ToLowerInvariant()))
                            {
                                case Type.INPUT:
                                    CommandInputSyntaxAttribute commandInputSyntaxAttribute = Reflection
                                        .wasGetAttributeFromEnumValue
                                        <CommandInputSyntaxAttribute>(
                                            Reflection.wasGetEnumValueFromName<ScriptKeys>(name));
                                    if (commandInputSyntaxAttribute != null &&
                                        !string.IsNullOrEmpty(commandInputSyntaxAttribute.Syntax))
                                    {
                                        result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                                            commandInputSyntaxAttribute.Syntax);
                                    }
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_SYNTAX_TYPE);
                            }
                            break;
                        case Entity.PERMISSION:
                            HashSet<string> data = new HashSet<string>();
                            object LockObject = new object();
                            Parallel.ForEach(Reflection.wasGetEnumNames<Configuration.Permissions>(), o =>
                            {
                                Configuration.Permissions permission =
                                    Reflection.wasGetEnumValueFromName<Configuration.Permissions>(o);
                                if ((commandPermissionMaskAttribute.PermissionMask & (uint) permission).Equals(0))
                                    return;
                                lock (LockObject)
                                {
                                    data.Add(o);
                                }
                            });
                            if (data.Any())
                            {
                                result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.wasEnumerableToCSV(data));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}