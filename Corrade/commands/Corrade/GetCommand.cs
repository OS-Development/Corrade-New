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
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getcommand =
                (corradeCommandParameters, result) =>
                {
                    string name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                    }
                    IsCorradeCommandAttribute isCommandAttribute =
                        Reflection.GetAttributeFromEnumValue<IsCorradeCommandAttribute>(
                            Reflection.GetEnumValueFromName<ScriptKeys>(name));
                    if (isCommandAttribute == null || isCommandAttribute.IsCorradeCorradeCommand.Equals(false))
                    {
                        throw new ScriptException(ScriptError.COMMAND_NOT_FOUND);
                    }
                    CommandPermissionMaskAttribute commandPermissionMaskAttribute =
                        Reflection.GetAttributeFromEnumValue<CommandPermissionMaskAttribute>(
                            Reflection.GetEnumValueFromName<ScriptKeys>(name));
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
                        Reflection.GetEnumValueFromName<Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Entity.SYNTAX:
                            switch (
                                Reflection.GetEnumValueFromName<Type>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)).ToLowerInvariant()))
                            {
                                case Type.INPUT:
                                    CommandInputSyntaxAttribute commandInputSyntaxAttribute = Reflection
                                        .GetAttributeFromEnumValue
                                        <CommandInputSyntaxAttribute>(
                                            Reflection.GetEnumValueFromName<ScriptKeys>(name));
                                    if (commandInputSyntaxAttribute != null &&
                                        !string.IsNullOrEmpty(commandInputSyntaxAttribute.Syntax))
                                    {
                                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
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
                            Parallel.ForEach(Reflection.GetEnumNames<Configuration.Permissions>(), o =>
                            {
                                Configuration.Permissions permission =
                                    Reflection.GetEnumValueFromName<Configuration.Permissions>(o);
                                if ((commandPermissionMaskAttribute.PermissionMask & (uint) permission).Equals(0))
                                    return;
                                lock (LockObject)
                                {
                                    data.Add(o);
                                }
                            });
                            if (data.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}