///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> createlandmark =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);

                    var CreateLandmarkEvent = new ManualResetEventSlim(false);
                    var succeeded = false;
                    InventoryItem inventoryItem = null;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestCreateItem(
                        Client.Inventory.FindFolderForType(AssetType.Landmark),
                        name,
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                            corradeCommandParameters.Message),
                        AssetType.Landmark,
                        UUID.Random(),
                        InventoryType.Landmark,
                        PermissionMask.All,
                        (success, item) =>
                        {
                            succeeded = success;
                            inventoryItem = item;
                            CreateLandmarkEvent.Set();
                        });
                    if (!CreateLandmarkEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);

                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            inventoryItem.UUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                            inventoryItem.AssetUUID.ToString()
                        }));
                };
        }
    }
}