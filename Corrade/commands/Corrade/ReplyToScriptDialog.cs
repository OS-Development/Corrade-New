///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytoscriptdialog =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int channel;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.CHANNEL)),
                                    corradeCommandParameters.Message)),
                            out channel))
                    {
                        throw new ScriptException(ScriptError.NO_CHANNEL_SPECIFIED);
                    }
                    int index;
                    if (
                        !int.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.INDEX)),
                                corradeCommandParameters.Message)),
                            out index))
                    {
                        throw new ScriptException(ScriptError.NO_BUTTON_INDEX_SPECIFIED);
                    }
                    var label =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.BUTTON)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(label))
                    {
                        throw new ScriptException(ScriptError.NO_BUTTON_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)),
                            out itemUUID))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    lock (ScriptDialogLock)
                    {
                        var scriptDialog =
                            ScriptDialogs.AsParallel().FirstOrDefault(
                                o =>
                                    o.Item.Equals(itemUUID) && o.Channel.Equals(channel) &&
                                    !o.Button.IndexOf(label).Equals(-1));
                        switch (!scriptDialog.Equals(default(ScriptDialog)))
                        {
                            case true:
                                ScriptDialogs.Remove(scriptDialog);
                                break;
                            default:
                                throw new ScriptException(ScriptError.NO_MATCHING_DIALOG_FOUND);
                        }
                    }
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.IGNORE:
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