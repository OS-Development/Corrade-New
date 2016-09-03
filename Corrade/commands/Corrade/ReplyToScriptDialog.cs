///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Structures;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> replytoscriptdialog =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int channel;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CHANNEL)),
                                    corradeCommandParameters.Message)),
                            out channel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CHANNEL_SPECIFIED);
                    }
                    var label =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.BUTTON)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(label))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_BUTTON_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)),
                            out itemUUID))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    }

                    ScriptDialog scriptDialog;
                    lock (ScriptDialogLock)
                    {
                        scriptDialog =
                            ScriptDialogs.AsParallel().FirstOrDefault(
                                o => o.Item.Equals(itemUUID) && o.Channel.Equals(channel));
                    }
                    if (scriptDialog.Equals(default(ScriptDialog)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_MATCHING_DIALOG_FOUND);

                    int index;
                    if (
                        !int.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INDEX)),
                                corradeCommandParameters.Message)),
                            out index))
                    {
                        index = scriptDialog.Button.IndexOf(label);
                        if (index.Equals(-1))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_MATCHING_DIALOG_FOUND);
                    }

                    ScriptDialogs.Remove(scriptDialog);

                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Enumerations.Action.IGNORE:
                            break;
                        default:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.ReplyToScriptDialog(channel, index, label, itemUUID);
                            }
                            break;
                    }
                };
        }
    }
}