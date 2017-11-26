///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> primitivebuy =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindPrimitive(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;

                        default:
                            if (
                                !Services.FindPrimitive(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;
                    }
                    if (primitive.Properties.SaleType.Equals(SaleType.Not))
                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOR_SALE);
                    if (!primitive.Properties.SalePrice.Equals(0) &&
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    if (primitive.Properties.SalePrice > Client.Self.Balance)
                        throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                    UUID folderUUID;
                    var folder =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(folder) || !UUID.TryParse(folder, out folderUUID))
                    {
                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                        folderUUID = Client.Inventory.Store.RootFolder.UUID;
                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                    }
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel()
                        .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    if (simulator == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    Locks.ClientInstanceObjectsLock.EnterWriteLock();
                    Client.Objects.BuyObject(simulator,
                        primitive.LocalID, primitive.Properties.SaleType,
                        primitive.Properties.SalePrice,
                        corradeCommandParameters.Group.UUID, folderUUID);
                    Locks.ClientInstanceObjectsLock.ExitWriteLock();
                };
        }
    }
}