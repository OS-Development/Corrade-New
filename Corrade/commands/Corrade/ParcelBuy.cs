///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> parcelbuy =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            corradeCommandParameters.Message));
                    Simulator simulator =
                        Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (!GetParcelAtPosition(simulator, position, ref parcel))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    bool forGroup;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FORGROUP)),
                                    corradeCommandParameters.Message)),
                            out forGroup))
                    {
                        if (
                            !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                                GroupPowers.LandDeed,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                        {
                            throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        }
                        forGroup = true;
                    }
                    bool removeContribution;
                    if (!bool.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REMOVECONTRIBUTION)),
                                corradeCommandParameters.Message)),
                        out removeContribution))
                    {
                        removeContribution = true;
                    }
                    ManualResetEvent ParcelInfoEvent = new ManualResetEvent(false);
                    UUID parcelUUID = UUID.Zero;
                    EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                    {
                        parcelUUID = args.Parcel.ID;
                        ParcelInfoEvent.Set();
                    };
                    lock (ClientInstanceParcelsLock)
                    {
                        Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                        Client.Parcels.RequestParcelInfo(parcelUUID);
                        if (!ParcelInfoEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                    }
                    bool forSale = false;
                    int handledEvents = 0;
                    int counter = 1;
                    ManualResetEvent DirLandReplyEvent = new ManualResetEvent(false);
                    EventHandler<DirLandReplyEventArgs> DirLandReplyEventArgs =
                        (sender, args) =>
                        {
                            handledEvents += args.DirParcels.Count;
                            Parallel.ForEach(args.DirParcels, o =>
                            {
                                if (o.ID.Equals(parcelUUID))
                                {
                                    forSale = o.ForSale;
                                    DirLandReplyEvent.Set();
                                }
                            });
                            if (((handledEvents - counter)%
                                 LINDEN_CONSTANTS.DIRECTORY.LAND.SEARCH_RESULTS_COUNT).Equals(0))
                            {
                                ++counter;
                                Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                    DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue,
                                    handledEvents);
                            }
                            DirLandReplyEvent.Set();
                        };
                    lock (ClientInstanceDirectoryLock)
                    {
                        Client.Directory.DirLandReply += DirLandReplyEventArgs;
                        Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                            DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue, handledEvents);
                        if (!DirLandReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                    }
                    if (!forSale)
                    {
                        throw new ScriptException(ScriptError.PARCEL_NOT_FOR_SALE);
                    }
                    if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    if (Client.Self.Balance < parcel.SalePrice)
                    {
                        throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                    }
                    if (!parcel.SalePrice.Equals(0) &&
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Client.Parcels.Buy(simulator, parcel.LocalID, forGroup, corradeCommandParameters.Group.UUID,
                        removeContribution, parcel.Area, parcel.SalePrice);
                };
        }
    }
}