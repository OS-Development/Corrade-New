///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getcommand =
                (corradeCommandParameters, result) =>
                {
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    var scriptKey = Reflection.GetEnumValueFromName<Command.ScriptKeys>(name);
                    if (scriptKey.Equals(default(Command.ScriptKeys)))
                        throw new Command.ScriptException(Enumerations.ScriptError.COMMAND_NOT_FOUND);
                    var commandPermissionMaskAttribute =
                        Reflection.GetAttributeFromEnumValue<Command.CommandPermissionMaskAttribute>(scriptKey);
                    if (commandPermissionMaskAttribute == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    if (corradeCommandParameters.Group == null ||
                        corradeCommandParameters.Group.Equals(default(Configuration.Group)))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);

                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.SYNTAX:
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Type>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message))))
                            {
                                case Enumerations.Type.INPUT:
                                    var commandInputSyntaxAttribute = Reflection
                                        .GetAttributeFromEnumValue
                                        <Command.CommandInputSyntaxAttribute>(
                                            Reflection.GetEnumValueFromName<Command.ScriptKeys>(name));
                                    if (!string.IsNullOrEmpty(commandInputSyntaxAttribute?.Syntax))
                                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                            commandInputSyntaxAttribute.Syntax);
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_SYNTAX_TYPE);
                            }
                            break;

                        case Enumerations.Entity.PERMISSION:
                            var data = new HashSet<string>();
                            var LockObject = new object();
                            Reflection.GetEnumNames<Configuration.Permissions>()
                                .AsParallel()
                                .Where(
                                    o =>
                                        commandPermissionMaskAttribute.PermissionMask.IsMaskFlagSet(
                                            Reflection.GetEnumValueFromName<Configuration.Permissions>(o)))
                                .ForAll(o =>
                                {
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                });
                            if (data.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}