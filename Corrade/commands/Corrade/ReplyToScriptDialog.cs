///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> replytoscriptdialog =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int channel;
                    if (
                        !int.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.CHANNEL)),
                                    message)),
                            out channel))
                    {
                        throw new ScriptException(ScriptError.NO_CHANNEL_SPECIFIED);
                    }
                    int index;
                    if (
                        !int.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.INDEX)), message)),
                            out index))
                    {
                        throw new ScriptException(ScriptError.NO_BUTTON_INDEX_SPECIFIED);
                    }
                    string label =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.BUTTON)),
                            message));
                    if (string.IsNullOrEmpty(label))
                    {
                        throw new ScriptException(ScriptError.NO_BUTTON_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message)),
                            out itemUUID))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    lock (ScriptDialogLock)
                    {
                        ScriptDialog scriptDialog =
                            ScriptDialogs.FirstOrDefault(
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
                    Client.Self.ReplyToScriptDialog(channel, index, label, itemUUID);
                };
        }
    }
}