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
            public static Action<Group, string, Dictionary<string, string>> parcelbuy =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    Simulator simulator =
                        Client.Network.Simulators.FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.InvariantCultureIgnoreCase));
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
                                    message)),
                            out forGroup))
                    {
                        if (
                            !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.LandDeed,
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
                                message)),
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
                        !HasCorradePermission(commandGroup.Name, (int) Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Client.Parcels.Buy(simulator, parcel.LocalID, forGroup, commandGroup.UUID,
                        removeContribution, parcel.Area, parcel.SalePrice);
                };
        }
    }
}