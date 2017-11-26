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
using CorradeConfigurationSharp;
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
                        ScriptPermissionRequest scriptPermissionRequest = null;
                        var itemUUID = UUID.Zero;
                        var taskUUID = UUID.Zero;
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                        );
                        switch (action)
                        {
                            case Enumerations.Action.REPLY:
                            case Enumerations.Action.IGNORE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                            corradeCommandParameters.Message)),
                                        out itemUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                                if (
                                    !UUID.TryParse(
                                        wasInput(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TASK)),
                                            corradeCommandParameters.Message)),
                                        out taskUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_TASK_SPECIFIED);
                                lock (ScriptPermissionsRequestsLock)
                                {
                                    scriptPermissionRequest =
                                        ScriptPermissionRequests.FirstOrDefault(
                                            o => o.Task.Equals(taskUUID) && o.Item.Equals(itemUUID));
                                }
                                if (scriptPermissionRequest == null)
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.SCRIPT_PERMISSION_REQUEST_NOT_FOUND);
                                break;
                        }

                        switch (action)
                        {
                            case Enumerations.Action.REPLY:
                                var succeeded = true;
                                var permissionMask = ScriptPermission.None;
                                CSV.ToEnumerable(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(
                                                    Command.ScriptKeys.PERMISSIONS)),
                                                corradeCommandParameters.Message)))
                                    .AsParallel()
                                    .Where(o => !string.IsNullOrEmpty(o))
                                    .ForAll(
                                        o =>
                                            typeof(ScriptPermission)
                                                .GetFields(BindingFlags.Public | BindingFlags.Static)
                                                .AsParallel()
                                                .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                                .ForAll(
                                                    q =>
                                                    {
                                                        var permission = (ScriptPermission) q.GetValue(null);
                                                        switch (permission)
                                                        {
                                                            case ScriptPermission.Debit:
                                                                if (
                                                                    !HasCorradePermission(
                                                                        corradeCommandParameters.Group.UUID,
                                                                        (int) Configuration.Permissions.Economy))
                                                                {
                                                                    succeeded = false;
                                                                    return;
                                                                }
                                                                break;

                                                            case ScriptPermission.Teleport:
                                                                if (
                                                                    !HasCorradePermission(
                                                                        corradeCommandParameters.Group.UUID,
                                                                        (int) Configuration.Permissions.Movement))
                                                                {
                                                                    succeeded = false;
                                                                    return;
                                                                }
                                                                break;

                                                            case ScriptPermission.ChangeJoints:
                                                            case ScriptPermission.ChangeLinks:
                                                                if (
                                                                    !HasCorradePermission(
                                                                        corradeCommandParameters.Group.UUID,
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
                                                                if (
                                                                    !HasCorradePermission(
                                                                        corradeCommandParameters.Group.UUID,
                                                                        (int) Configuration.Permissions.Grooming))
                                                                {
                                                                    succeeded = false;
                                                                    return;
                                                                }
                                                                break;

                                                            case ScriptPermission.ReleaseOwnership:
                                                            case ScriptPermission.ChangePermissions:
                                                                if (
                                                                    !HasCorradePermission(
                                                                        corradeCommandParameters.Group.UUID,
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
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                var region = wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                        corradeCommandParameters.Message));
                                Locks.ClientInstanceNetworkLock.EnterReadLock();
                                var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                                    o => string.Equals(region, o.Name, StringComparison.OrdinalIgnoreCase));
                                Locks.ClientInstanceNetworkLock.ExitReadLock();
                                if (simulator == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                                // remove the script permission request
                                lock (ScriptPermissionsRequestsLock)
                                {
                                    ScriptPermissionRequests.Remove(scriptPermissionRequest);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.ScriptQuestionReply(simulator, itemUUID, taskUUID,
                                    permissionMask);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.PURGE:
                                lock (ScriptPermissionsRequestsLock)
                                {
                                    ScriptPermissionRequests.Clear();
                                }
                                break;

                            case Enumerations.Action.IGNORE:
                                lock (ScriptPermissionsRequestsLock)
                                {
                                    ScriptPermissionRequests.Remove(scriptPermissionRequest);
                                }
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}