///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> mapfriend =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Friendship))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                    if (friend == null)
                    {
                        throw new ScriptException(ScriptError.FRIEND_NOT_FOUND);
                    }
                    if (!friend.CanSeeThemOnMap)
                    {
                        throw new ScriptException(ScriptError.FRIEND_DOES_NOT_ALLOW_MAPPING);
                    }
                    ulong regionHandle = 0;
                    Vector3 position = Vector3.Zero;
                    ManualResetEvent FriendFoundEvent = new ManualResetEvent(false);
                    bool offline = false;
                    EventHandler<FriendFoundReplyEventArgs> FriendFoundEventHandler = (sender, args) =>
                    {
                        if (args.RegionHandle.Equals(0))
                        {
                            offline = true;
                            FriendFoundEvent.Set();
                            return;
                        }
                        regionHandle = args.RegionHandle;
                        position = args.Location;
                        FriendFoundEvent.Set();
                    };
                    lock (Locks.ClientInstanceFriendsLock)
                    {
                        Client.Friends.FriendFoundReply += FriendFoundEventHandler;
                        Client.Friends.MapFriend(agentUUID);
                        if (!FriendFoundEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Friends.FriendFoundReply -= FriendFoundEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_MAPPING_FRIEND);
                        }
                        Client.Friends.FriendFoundReply -= FriendFoundEventHandler;
                    }
                    if (offline)
                    {
                        throw new ScriptException(ScriptError.FRIEND_OFFLINE);
                    }
                    UUID parcelUUID = Client.Parcels.RequestRemoteParcelID(position, regionHandle, UUID.Zero);
                    ManualResetEvent ParcelInfoEvent = new ManualResetEvent(false);
                    string regionName = string.Empty;
                    EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                    {
                        regionName = args.Parcel.SimName;
                        ParcelInfoEvent.Set();
                    };
                    lock (Locks.ClientInstanceParcelsLock)
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
                    result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                        CSV.FromEnumerable(new[] {regionName, position.ToString()}));
                };
        }
    }
}