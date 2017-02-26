///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using System.Collections.Generic;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> mapfriend =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Friendship))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                    }
                    FriendInfo friend;
                    lock (Locks.ClientInstanceFriendsLock)
                    {
                        friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                    }
                    if (friend == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.FRIEND_NOT_FOUND);
                    }
                    if (!friend.CanSeeThemOnMap)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.FRIEND_DOES_NOT_ALLOW_MAPPING);
                    }
                    ulong regionHandle = 0;
                    var position = Vector3.Zero;
                    var FriendFoundEvent = new ManualResetEvent(false);
                    var offline = false;
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
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_MAPPING_FRIEND);
                        }
                        Client.Friends.FriendFoundReply -= FriendFoundEventHandler;
                    }
                    if (offline)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.FRIEND_OFFLINE);
                    }
                    var parcelUUID = Client.Parcels.RequestRemoteParcelID(position, regionHandle, UUID.Zero);
                    var ParcelInfoEvent = new ManualResetEvent(false);
                    var regionName = string.Empty;
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
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                    }
                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                        CSV.FromEnumerable(new[] {regionName, position.ToString()}));
                };
        }
    }
}