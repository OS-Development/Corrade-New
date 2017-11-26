///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> compilescript
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    bool mono;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MONO)),
                            corradeCommandParameters.Message)), out mono))
                        mono = true;
                    var CreateScriptEvent = new ManualResetEventSlim(false);
                    InventoryItem newScript = null;
                    var assetUUID = UUID.Zero;
                    var itemUUID = UUID.Zero;
                    var succeeded = false;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.TrashFolder),
                        Path.GetRandomFileName().Replace(".", string.Empty),
                        string.Empty,
                        AssetType.LSLText,
                        UUID.Random(),
                        InventoryType.LSL,
                        PermissionMask.Transfer,
                        delegate(bool completed, InventoryItem createdItem)
                        {
                            assetUUID = createdItem.AssetUUID;
                            itemUUID = createdItem.UUID;
                            succeeded = completed;
                            newScript = createdItem;
                            CreateScriptEvent.Set();
                        });
                    if (!CreateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                    var scriptMessages = new List<string>();
                    var scriptCompiled = false;
                    var UpdateScriptEvent = new ManualResetEventSlim(false);
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message)))))
                    {
                        Client.Inventory.RequestUpdateScriptAgentInventory(memoryStream.ToArray(), newScript.UUID, mono,
                            delegate(bool completed, string status, bool compiled, List<string> messages,
                                UUID itemID, UUID assetID)
                            {
                                assetUUID = assetID;
                                itemUUID = itemID;
                                succeeded = completed;
                                scriptCompiled = compiled;
                                if (messages != null && messages.Any())
                                    scriptMessages.AddRange(messages);
                                UpdateScriptEvent.Set();
                            });
                        if (!UpdateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                        }
                    }
                    // Delete script.
                    Client.Inventory.RemoveItem(itemUUID);
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                    if (scriptMessages.Any())
                        result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            CSV.FromEnumerable(scriptMessages));
                    if (!scriptCompiled)
                        throw new Command.ScriptException(Enumerations.ScriptError.SCRIPT_COMPILATION_FAILED);
                };
        }
    }
}