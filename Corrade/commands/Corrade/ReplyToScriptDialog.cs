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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                replytoscriptdialog =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }

                        UUID dialogUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DIALOG)),
                                    corradeCommandParameters.Message)),
                                out dialogUUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_DIALOG_SPECIFIED);
                        }

                        ScriptDialog scriptDialog;
                        switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant()))
                        {
                            case Enumerations.Action.PURGE:
                                lock (ScriptDialogsLock)
                                {
                                    ScriptDialogs.Clear();
                                }
                                break;
                            case Enumerations.Action.IGNORE:
                                lock (ScriptDialogsLock)
                                {
                                    if (!ScriptDialogs.TryGetValue(dialogUUID, out scriptDialog))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_MATCHING_DIALOG_FOUND);
                                    ScriptDialogs.Remove(dialogUUID);
                                }
                                break;
                            case Enumerations.Action.REPLY:
                                lock (ScriptDialogsLock)
                                {
                                    if (!ScriptDialogs.TryGetValue(dialogUUID, out scriptDialog))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_MATCHING_DIALOG_FOUND);
                                }
                                var label = wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.BUTTON)),
                                        corradeCommandParameters.Message));

                                var index = wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INDEX)),
                                    corradeCommandParameters.Message));
                                int labelIndex;
                                if (string.IsNullOrEmpty(index) || !int.TryParse(index, out labelIndex))
                                    labelIndex = -1;

                                if (string.IsNullOrEmpty(label) && labelIndex.Equals(-1))
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.NO_LABEL_OR_INDEX_SPECIFIED);
                                }
                                if (string.IsNullOrEmpty(label) && !labelIndex.Equals(-1))
                                {
                                    label = scriptDialog.Button.ElementAtOrDefault(labelIndex);
                                    if (string.IsNullOrEmpty(label))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.DIALOG_BUTTON_NOT_FOUND);
                                }
                                else if (!string.IsNullOrEmpty(label) && labelIndex.Equals(-1))
                                {
                                    labelIndex = scriptDialog.Button.IndexOf(label);
                                    if (labelIndex.Equals(-1))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.DIALOG_BUTTON_NOT_FOUND);
                                }
                                else
                                {
                                    if (!scriptDialog.Button.IndexOf(label).Equals(labelIndex))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.DIALOG_BUTTON_NOT_FOUND);
                                }

                                // Remove the dialog.
                                lock (ScriptDialogsLock)
                                {
                                    ScriptDialogs.Remove(dialogUUID);
                                }

                                // Reply to the dialog.
                                lock (Locks.ClientInstanceSelfLock)
                                {
                                    Client.Self.ReplyToScriptDialog(scriptDialog.Channel, labelIndex, label,
                                        scriptDialog.Item);
                                }
                                break;
                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}