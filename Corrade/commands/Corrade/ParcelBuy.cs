///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> parcelbuy =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                    }
                    if (simulator == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    }
                    bool forGroup;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORGROUP)),
                                    corradeCommandParameters.Message)),
                            out forGroup))
                    {
                        if (
                            !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                                GroupPowers.LandDeed,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        }
                        forGroup = true;
                    }
                    bool removeContribution;
                    if (!bool.TryParse(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REMOVECONTRIBUTION)),
                                corradeCommandParameters.Message)),
                        out removeContribution))
                    {
                        removeContribution = true;
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    UUID parcelUUID;
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        parcelUUID = Client.Parcels.RequestRemoteParcelID(position, simulator.Handle,
                            UUID.Zero);
                    }
                    if (parcelUUID.Equals(UUID.Zero))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    var ParcelInfoEvent = new ManualResetEvent(false);
                    EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                    {
                        if (args.Parcel.ID.Equals(parcelUUID))
                        {
                            ParcelInfoEvent.Set();
                        }
                    };
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                        Client.Parcels.RequestParcelInfo(parcelUUID);
                        if (!ParcelInfoEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                    }
                    var forSale = false;
                    var handledEvents = 0;
                    var counter = 1;
                    var DirectorySearchResultsAlarm =
                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<DirLandReplyEventArgs> DirLandReplyEventArgs =
                        (sender, args) =>
                        {
                            DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                            handledEvents += args.DirParcels.Count;
                            args.DirParcels.AsParallel().Where(o => o.ID.Equals(parcelUUID)).ForAll(o =>
                            {
                                forSale = o.ForSale;
                                DirectorySearchResultsAlarm.Signal.Set();
                            });
                            if (handledEvents > wasOpenMetaverse.Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT &&
                                ((handledEvents - counter)%
                                 wasOpenMetaverse.Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT).Equals(0))
                            {
                                ++counter;
                                Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                    DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue,
                                    handledEvents);
                            }
                        };
                    lock (Locks.ClientInstanceDirectoryLock)
                    {
                        Client.Directory.DirLandReply += DirLandReplyEventArgs;
                        Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                            DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue, handledEvents);
                        if (
                            !DirectorySearchResultsAlarm.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                false))
                        {
                            Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                    }
                    if (!forSale && !parcel.AuthBuyerID.Equals(Client.Self.AgentID))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.PARCEL_NOT_FOR_SALE);
                    }
                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        if (Client.Self.Balance < parcel.SalePrice)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                        }
                    }
                    if (!parcel.SalePrice.Equals(0) &&
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.Buy(simulator, parcel.LocalID, forGroup, corradeCommandParameters.Group.UUID,
                            removeContribution, parcel.Area, parcel.SalePrice);
                    }
                };
        }
    }
}