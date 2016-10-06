///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                replytoscriptpermissionrequest =
                    (corradeCommandParameters, result) =>
                    {
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
                        UUID taskUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TASK)),
                                    corradeCommandParameters.Message)),
                                out taskUUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_TASK_SPECIFIED);
                        }
                        ScriptPermissionRequest scriptPermissionRequest;
                        lock (ScriptPermissionRequestLock)
                        {
                            scriptPermissionRequest =
                                ScriptPermissionRequests.FirstOrDefault(
                                    o => o.Task.Equals(taskUUID) && o.Item.Equals(itemUUID));
                        }
                        if (scriptPermissionRequest.Equals(default(ScriptPermissionRequest)))
                        {
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.SCRIPT_PERMISSION_REQUEST_NOT_FOUND);
                        }
                        var succeeded = true;
                        var permissionMask = ScriptPermission.None;
                        CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                    corradeCommandParameters.Message)))
                            .ToArray()
                            .AsParallel()
                            .Where(o => !string.IsNullOrEmpty(o))
                            .ForAll(
                                o =>
                                    typeof (ScriptPermission).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel()
                                        .Where(p => Strings.Equals(o, p.Name, StringComparison.Ordinal))
                                        .ForAll(
                                            q =>
                                            {
                                                var permission = (ScriptPermission) q.GetValue(null);
                                                switch (permission)
                                                {
                                                    case ScriptPermission.Debit:
                                                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                            (int) Configuration.Permissions.Economy))
                                                        {
                                                            succeeded = false;
                                                            return;
                                                        }
                                                        break;
                                                    case ScriptPermission.Teleport:
                                                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                            (int) Configuration.Permissions.Movement))
                                                        {
                                                            succeeded = false;
                                                            return;
                                                        }
                                                        break;
                                                    case ScriptPermission.ChangeJoints:
                                                    case ScriptPermission.ChangeLinks:
                                                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                            (int) Configuration.Permissions.Interact))
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
                                                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                            (int) Configuration.Permissions.Grooming))
                                                        {
                                                            succeeded = false;
                                                            return;
                                                        }
                                                        break;
                                                    case ScriptPermission.ReleaseOwnership:
                                                    case ScriptPermission.ChangePermissions:
                                                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                            (int) Configuration.Permissions.Inventory))
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
                                                BitTwiddling.SetMaskFlag(ref permissionMask, permission);
                                            }));
                        if (!succeeded)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var region = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                        Simulator simulator;
                        lock (Locks.ClientInstanceNetworkLock)
                        {
                            simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o => Strings.Equals(region, o.Name, StringComparison.OrdinalIgnoreCase));
                        }
                        if (simulator == null)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        }
                        // remove the script permission request
                        lock (ScriptPermissionRequestLock)
                        {
                            ScriptPermissionRequests.Remove(scriptPermissionRequest);
                        }
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.ScriptQuestionReply(simulator, itemUUID, taskUUID,
                                permissionMask);
                        }
                    };
        }
    }
}