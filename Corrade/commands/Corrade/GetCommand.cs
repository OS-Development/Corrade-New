///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getcommand =
                (commandGroup, message, result) =>
                {
                    string name =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                            message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                    }
                    IsCorradeCommandAttribute isCommandAttribute =
                        wasGetAttributeFromEnumValue<IsCorradeCommandAttribute>(
                            wasGetEnumValueFromDescription<ScriptKeys>(name));
                    if (isCommandAttribute == null || isCommandAttribute.IsCorradeCorradeCommand.Equals(false))
                    {
                        throw new ScriptException(ScriptError.COMMAND_NOT_FOUND);
                    }
                    CommandPermissionMaskAttribute commandPermissionMaskAttribute =
                        wasGetAttributeFromEnumValue<CommandPermissionMaskAttribute>(
                            wasGetEnumValueFromDescription<ScriptKeys>(name));
                    if (commandPermissionMaskAttribute == null)
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (!commandGroup.Equals(default(Group)))
                    {
                        case false:
                            throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    switch (
                        wasGetEnumValueFromDescription<Entity>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                                    message)).ToLowerInvariant()))
                    {
                        case Entity.SYNTAX:
                            switch (
                                wasGetEnumValueFromDescription<Type>(
                                    wasInput(
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                            message)).ToLowerInvariant()))
                            {
                                case Type.INPUT:
                                    CommandInputSyntaxAttribute commandInputSyntaxAttribute = wasGetAttributeFromEnumValue
                                        <CommandInputSyntaxAttribute>(
                                            wasGetEnumValueFromDescription<ScriptKeys>(name));
                                    if (commandInputSyntaxAttribute != null &&
                                        !string.IsNullOrEmpty(commandInputSyntaxAttribute.Syntax))
                                    {
                                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
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
                            Parallel.ForEach(wasGetEnumDescriptions<Permissions>(), o =>
                            {
                                Permissions permission = wasGetEnumValueFromDescription<Permissions>(o);
                                if ((commandPermissionMaskAttribute.PermissionMask & (uint) permission).Equals(0))
                                    return;
                                lock (LockObject)
                                {
                                    data.Add(o);
                                }
                            });
                            if (data.Any())
                            {
                                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), wasEnumerableToCSV(data));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}