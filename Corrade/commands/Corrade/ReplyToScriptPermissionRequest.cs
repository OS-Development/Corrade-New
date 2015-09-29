///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytoscriptpermissionrequest =
                (corradeCommandParameters, result) =>
                {
                    UUID itemUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)),
                            out itemUUID))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID taskUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TASK)),
                                corradeCommandParameters.Message)),
                            out taskUUID))
                    {
                        throw new ScriptException(ScriptError.NO_TASK_SPECIFIED);
                    }
                    bool succeeded = true;
                    int permissionMask = 0;
                    Parallel.ForEach(wasCSVToEnumerable(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o =>
                            Parallel.ForEach(
                                typeof (ScriptPermission).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                q =>
                                {
                                    ScriptPermission permission = (ScriptPermission) q.GetValue(null);
                                    switch (permission)
                                    {
                                        case ScriptPermission.Debit:
                                            if (HasCorradePermission(corradeCommandParameters.Group.Name,
                                                (int) Permissions.Economy))
                                            {
                                                succeeded = false;
                                                return;
                                            }
                                            break;
                                        case ScriptPermission.Teleport:
                                            if (!HasCorradePermission(corradeCommandParameters.Group.Name,
                                                (int) Permissions.Movement))
                                            {
                                                succeeded = false;
                                                return;
                                            }
                                            break;
                                        case ScriptPermission.ChangeJoints:
                                        case ScriptPermission.ChangeLinks:
                                            if (!HasCorradePermission(corradeCommandParameters.Group.Name,
                                                (int) Permissions.Interact))
                                            {
                                                succeeded = false;
                                                return;
                                            }
                                            break;
                                        case ScriptPermission.TriggerAnimation:
                                        case ScriptPermission.TrackCamera:
                                        case ScriptPermission.TakeControls:
                                        case ScriptPermission.RemapControls:
                                        case ScriptPermission.ControlCamera:
                                        case ScriptPermission.Attach:
                                            if (!HasCorradePermission(corradeCommandParameters.Group.Name,
                                                (int) Permissions.Grooming))
                                            {
                                                succeeded = false;
                                                return;
                                            }
                                            break;
                                        case ScriptPermission.ReleaseOwnership:
                                        case ScriptPermission.ChangePermissions:
                                            if (!HasCorradePermission(corradeCommandParameters.Group.Name,
                                                (int) Permissions.Inventory))
                                            {
                                                succeeded = false;
                                                return;
                                            }
                                            break;
                                        case ScriptPermission.None:
                                            return;
                                        default: // ignore any unimplemented permissions
                                            succeeded = false;
                                            return;
                                    }
                                    permissionMask |= (int) permission;
                                }));
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region = wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            corradeCommandParameters.Message));
                    Simulator simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o => o.Name.Equals(region, StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Client.Self.ScriptQuestionReply(simulator, itemUUID, taskUUID,
                        (ScriptPermission) permissionMask);
                };
        }
    }
}