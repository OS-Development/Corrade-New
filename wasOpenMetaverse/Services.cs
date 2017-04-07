///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Parallel = System.Threading.Tasks.Parallel;
using OpenMetaverse.Packets;

namespace wasOpenMetaverse
{
    public static class Services
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates the current balance by requesting it from the grid.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the request in milliseconds</param>
        /// <returns>true if the balance could be retrieved</returns>
        public static bool UpdateBalance(GridClient Client, uint millisecondsTimeout)
        {
            var MoneyBalanceEvent = new ManualResetEvent(false);
            EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceEventHandler =
                (sender, args) => MoneyBalanceEvent.Set();
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.MoneyBalanceReply += MoneyBalanceEventHandler;
                Client.Self.RequestBalance();
                if (!MoneyBalanceEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
                    return false;
                }
                Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the UUIDs of all the current groups.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="mutes">an enumerable where to store mute entries</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetMutes(GridClient Client, uint millisecondsTimeout, ref IEnumerable<MuteEntry> mutes)
        {
            var MuteListUpdatedEvent = new ManualResetEvent(false);
            EventHandler<EventArgs> MuteListUpdatedEventHandler =
                (sender, args) => MuteListUpdatedEvent.Set();
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                Client.Self.RequestMuteList();
                MuteListUpdatedEvent.WaitOne((int)millisecondsTimeout, false);
                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
            }
            mutes = Client.Self.MuteList.Copy().Values;
            return true;
        }

        /// <summary>
        ///     A wrapper for retrieveing all the current groups that implements caching.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="mutes">an enumerable where to store mute entries</param>
        /// <returns>true if the current groups could be fetched</returns>
        public static bool GetMutes(GridClient Client, uint millisecondsTimeout, ref IEnumerable<MuteEntry> mutes)
        {
            bool succeeded;
            lock (Locks.ClientInstanceSelfLock)
            {
                if (Cache.MuteCache.Any())
                {
                    mutes = Cache.MuteCache.OfType<MuteEntry>();
                    return true;
                }

                succeeded = directGetMutes(Client, millisecondsTimeout, ref mutes);

                if (succeeded)
                {
                    Cache.MuteCache.UnionWith(mutes.OfType<Cache.MuteEntry>());
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the UUIDs of all the current groups.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="bans">a dictionary to store group bans</param>
        /// <returns>true if the current groups could be fetched</returns>
        public static bool GetGroupBans(GridClient Client, UUID groupUUID, uint millisecondsTimeout,
            ref Dictionary<UUID, DateTime> bans)
        {
            Dictionary<UUID, DateTime> bannedAgents = null;
            var BannedAgentsEvent = new ManualResetEvent(false);
            var succeeded = false;
            EventHandler<BannedAgentsEventArgs> BannedAgentsEventHandler = (sender, args) =>
            {
                succeeded = args.Success;
                bannedAgents = args.BannedAgents;
                BannedAgentsEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.BannedAgents += BannedAgentsEventHandler;
                Client.Groups.RequestBannedAgents(groupUUID);
                if (!BannedAgentsEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Groups.BannedAgents -= BannedAgentsEventHandler;
                    return false;
                }
                Client.Groups.BannedAgents -= BannedAgentsEventHandler;
            }
            if (!succeeded)
                return false;
            bans = bannedAgents;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the UUIDs of all the current groups.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetCurrentGroups(GridClient Client, uint millisecondsTimeout,
            ref IEnumerable<UUID> groups)
        {
            var CurrentGroupsReceivedEvent = new ManualResetEvent(false);
            Dictionary<UUID, Group> currentGroups = null;
            EventHandler<CurrentGroupsEventArgs> CurrentGroupsEventHandler = (sender, args) =>
            {
                currentGroups = args.Groups;
                CurrentGroupsReceivedEvent.Set();
            };
            Client.Groups.CurrentGroups += CurrentGroupsEventHandler;
            Client.Groups.RequestCurrentGroups();
            if (!CurrentGroupsReceivedEvent.WaitOne((int)millisecondsTimeout, false))
            {
                Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
                return false;
            }
            Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
            switch (currentGroups.Any())
            {
                case true:
                    groups = currentGroups.Keys;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        ///     A wrapper for retrieveing all the current groups that implements caching.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        public static bool GetCurrentGroups(GridClient Client, uint millisecondsTimeout, ref IEnumerable<UUID> groups)
        {
            bool succeeded;
            lock (Locks.ClientInstanceGroupsLock)
            {
                if (Cache.CurrentGroupsCache.Any())
                {
                    groups = Cache.CurrentGroupsCache;
                    return true;
                }

                succeeded = directGetCurrentGroups(Client, millisecondsTimeout, ref groups);

                if (succeeded)
                {
                    Cache.CurrentGroupsCache.UnionWith(groups);
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent has a set of powers for a group.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="agentUUID">the agent UUID</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="powers">a GroupPowers structure</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout in millisecons for each data burst</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>true if the agent has the powers</returns>
        public static bool HasGroupPowers(GridClient Client, UUID agentUUID, UUID groupUUID, GroupPowers powers,
            uint millisecondsTimeout,
            uint dataTimeout, DecayingAlarm alarm)
        {
            var avatarGroups = new List<AvatarGroup>();
            var LockObject = new object();
            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
            {
                alarm.Alarm(dataTimeout);
                lock (LockObject)
                {
                    avatarGroups.AddRange(args.Groups);
                }
            };
            lock (Locks.ClientInstanceAvatarsLock)
            {
                Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                Client.Avatars.RequestAvatarProperties(agentUUID);
                if (!alarm.Signal.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    return false;
                }
                Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
            }
            return
                avatarGroups.AsParallel()
                    .Any(o => o.GroupID.Equals(groupUUID) && !(o.GroupPowers & powers).Equals(GroupPowers.None));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Attempts to join the group chat for a given group.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="groupUUID">the UUID of the group to join the group chat for</param>
        /// <param name="millisecondsTimeout">timeout for joining the group chat</param>
        /// <returns>true if the group chat was joined</returns>
        public static bool JoinGroupChat(GridClient Client, UUID groupUUID, uint millisecondsTimeout)
        {
            var succeeded = false;
            var GroupChatJoinedEvent = new ManualResetEvent(false);
            EventHandler<GroupChatJoinedEventArgs> GroupChatJoinedEventHandler =
                (sender, args) =>
                {
                    succeeded = args.Success;
                    GroupChatJoinedEvent.Set();
                };
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.GroupChatJoined += GroupChatJoinedEventHandler;
                Client.Self.RequestJoinGroupChat(groupUUID);
                if (!GroupChatJoinedEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                    return false;
                }
                Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
            }
            return succeeded;
        }

        public static bool UpdateParcelAccessList(GridClient Client, Simulator simulator, int parcelLocalID, AccessList accessListType, List<ParcelManager.ParcelAccessEntry> accessList)
        {
            lock (Locks.ClientInstanceNetworkLock)
            {
                Client.Network.SendPacket(new ParcelAccessListUpdatePacket
                {
                    List = accessList.AsParallel().Select(o => new ParcelAccessListUpdatePacket.ListBlock
                    {
                        ID = o.AgentID,
                        Flags = (uint)o.Flags
                    }).ToArray(),
                    AgentData = new ParcelAccessListUpdatePacket.AgentDataBlock
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data = new ParcelAccessListUpdatePacket.DataBlock
                    {
                        Flags = (uint)accessListType,
                        LocalID = parcelLocalID,
                        TransactionID = UUID.Random(),
                        SequenceID = 1,
                        Sections = (int)Math.Ceiling(accessList.Count / 48f)
                    },
                    Type = PacketType.ParcelAccessListUpdate
                }, simulator);
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent referenced by an UUID is in a group
        ///     referenced by an UUID.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="groupUUID">the UUID of the groupt</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <returns>true if the agent is in the group</returns>
        public static bool AgentInGroup(GridClient Client, UUID agentUUID, UUID groupUUID, uint millisecondsTimeout)
        {
            var groupMembersReceivedEvent = new ManualResetEvent(false);
            var groupMembers = new HashSet<UUID>();
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                groupMembers.UnionWith(args.Members.Values.Select(o => o.ID));
                groupMembersReceivedEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                Client.Groups.RequestGroupMembers(groupUUID);
                if (!groupMembersReceivedEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    return false;
                }
                Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
            }
            return groupMembers.Contains(agentUUID);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches a group.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="group">a group object to store the group profile</param>
        /// <returns>true if the group was found and false otherwise</returns>
        public static bool RequestGroup(GridClient Client, UUID groupUUID, uint millisecondsTimeout, ref Group group)
        {
            var localGroup = new Group();
            var GroupProfileEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (sender, args) =>
            {
                localGroup = args.Group;
                GroupProfileEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupProfile += GroupProfileDelegate;
                Client.Groups.RequestGroupProfile(groupUUID);
                if (!GroupProfileEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Groups.GroupProfile -= GroupProfileDelegate;
                    return false;
                }
                Client.Groups.GroupProfile -= GroupProfileDelegate;
            }
            group = localGroup;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the parcel of a simulator given a position.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="simulator">the simulator containing the parcel</param>
        /// <param name="position">a position within the parcel</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="parcel">a parcel object where to store the found parcel</param>
        /// <returns>true if the parcel could be found</returns>
        public static bool GetParcelAtPosition(GridClient Client, Simulator simulator, Vector3 position,
            uint millisecondsTimeout,
            ref Parcel parcel)
        {
            var RequestAllSimParcelsEvent = new ManualResetEvent(false);
            EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedDelegate =
                (sender, args) => RequestAllSimParcelsEvent.Set();
            lock (Locks.ClientInstanceParcelsLock)
            {
                Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedDelegate;
                switch (!simulator.IsParcelMapFull())
                {
                    case true:
                        Client.Parcels.RequestAllSimParcels(simulator);
                        break;

                    default:
                        RequestAllSimParcelsEvent.Set();
                        break;
                }
                if (!RequestAllSimParcelsEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
                    return false;
                }
                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
            }
            var localParcel = simulator.Parcels.Copy().Values
                .AsParallel()
                .Where(
                    o =>
                        position.X >= o.AABBMin.X && position.X <= o.AABBMax.X &&
                        position.Y >= o.AABBMin.Y &&
                        position.Y <= o.AABBMax.Y)
                .OrderBy(o => Vector3.Distance(o.AABBMin, o.AABBMax))
                .FirstOrDefault();
            if (localParcel != null)
            {
                parcel = localParcel;
                return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the parcel info of a parcel given a parcel UUID.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="parcelUUID">the UUID of the parcel</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="parcelInfo">a parcel info object</param>
        /// <returns>true if the parcel could be found</returns>
        public static bool GetParcelInfo(GridClient Client, UUID parcelUUID, uint millisecondsTimeout,
            ref ParcelInfo parcelInfo)
        {
            var ParcelInfoEvent = new ManualResetEvent(false);
            var localParcelInfo = new ParcelInfo();
            EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
            {
                if (args.Parcel.ID.Equals(parcelUUID))
                {
                    localParcelInfo = args.Parcel;
                    ParcelInfoEvent.Set();
                }
            };
            lock (Locks.ClientInstanceParcelsLock)
            {
                Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                Client.Parcels.RequestParcelInfo(parcelUUID);
                if (!ParcelInfoEvent.WaitOne((int)millisecondsTimeout, false))
                {
                    Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                    return false;
                }
                Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
            }
            if (localParcelInfo.Equals(default(ParcelInfo)))
            {
                return false;
            }
            parcelInfo = localParcelInfo;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches all the primitives in-range.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="range">the range to extend or contract to</param>
        /// <returns>the primitives in range</returns>
        public static HashSet<Primitive> GetPrimitives(GridClient Client, float range)
        {
            lock (Locks.ClientInstanceNetworkLock)
            {
                var objectsPrimitives = Client.Network.Simulators.AsParallel()
                    .SelectMany(o => o?.ObjectsPrimitives?.Copy()?.Values)
                    .ToDictionary(o => o.LocalID, p => p);
                var objectsAvatars = Client.Network.Simulators.AsParallel()
                    .SelectMany(o => o?.ObjectsAvatars?.Copy()?.Values)
                    .ToDictionary(o => o.LocalID, p => p);
                return new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                    .Select(o => new { s = o, a = o?.ObjectsPrimitives?.Copy()?.Values })
                    .SelectMany(o => o.a.AsParallel().Where(p =>
                    {
                        // find the parent of the primitive
                        var parent = p;
                        Primitive ancestorPrimitive;
                        if (objectsPrimitives.TryGetValue(parent.ParentID, out ancestorPrimitive))
                        {
                            parent = ancestorPrimitive;
                        }
                        Avatar ancestorAvatar;
                        if (objectsAvatars.TryGetValue(parent.ParentID, out ancestorAvatar))
                        {
                            parent = ancestorAvatar;
                        }
                        return Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                            Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                    })));
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches all the objects in-range.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="range">the range to extend or contract to</param>
        /// <returns>the primitives in range</returns>
        public static HashSet<Primitive> GetObjects(GridClient Client, float range)
        {
            lock (Locks.ClientInstanceNetworkLock)
            {
                var objectsAvatars = Client.Network.Simulators.AsParallel()
                    .SelectMany(o => o?.ObjectsAvatars?.Copy()?.Values)
                    .ToDictionary(o => o.LocalID, p => p);
                return new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                    .Select(o => new { s = o, a = o.ObjectsPrimitives.Copy().Values })
                    .SelectMany(o => o.a.AsParallel().Where(p =>
                    {
                        // find the parent of the primitive
                        var parent = p;
                        Avatar ancestorAvatar;
                        if (objectsAvatars.TryGetValue(parent.ParentID, out ancestorAvatar))
                        {
                            parent = ancestorAvatar;
                        }
                        return (p.ParentID.Equals(0) || objectsAvatars.ContainsKey(p.ParentID)) &&
                               Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                                   Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                    })));
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets all the avatars in a given range.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="range">the range to search in</param>
        public static HashSet<Avatar> GetAvatars(GridClient Client, float range)
        {
            lock (Locks.ClientInstanceNetworkLock)
            {
                var objectsPrimitives = Client.Network.Simulators.AsParallel()
                    .SelectMany(o => o?.ObjectsPrimitives?.Copy()?.Values)
                    .ToDictionary(o => o.LocalID, p => p);
                var objectsAvatars = Client.Network.Simulators.AsParallel()
                    .SelectMany(o => o?.ObjectsAvatars?.Copy()?.Values)
                    .ToDictionary(o => o.LocalID, p => p);
                return new HashSet<Avatar>(Client.Network.Simulators.AsParallel()
                    .Select(o => new { s = o, a = o?.ObjectsAvatars?.Copy()?.Values })
                    .SelectMany(o => o.a.AsParallel().Where(p =>
                    {
                        // find the parent of the primitive
                        Primitive parent = p;
                        Primitive ancestorPrimitive;
                        if (objectsPrimitives.TryGetValue(parent.ParentID, out ancestorPrimitive))
                        {
                            parent = ancestorPrimitive;
                        }
                        Avatar ancestorAvatar;
                        if (objectsAvatars.TryGetValue(parent.ParentID, out ancestorAvatar))
                        {
                            parent = ancestorAvatar;
                        }
                        return Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                            Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                    })));
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates a set of primitives by scanning their properties.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="primitives">a list of primitives to update</param>
        /// <param name="dataTimeout">the timeout for receiving data from the grid</param>
        /// <returns>a list of updated primitives</returns>
        public static bool UpdatePrimitives(GridClient Client, ref HashSet<Primitive> primitives, uint dataTimeout)
        {
            var ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler =
                (sender, args) => { ObjectPropertiesEvent.Set(); };
            var localPrimitives = primitives;
            var regionHandles = new HashSet<ulong>(localPrimitives.Select(o => o.RegionHandle));
            Parallel.ForEach(regionHandles, o =>
            {
                lock (Locks.ClientInstanceObjectsLock)
                {
                    Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                    ObjectPropertiesEvent.Reset();
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        Client.Objects.SelectObjects(
                            Client.Network.Simulators.AsParallel().FirstOrDefault(p => p.Handle.Equals(o)),
                            localPrimitives.Where(p => p.RegionHandle.Equals(o))
                                .Select(p => p.LocalID)
                                .ToArray(), true);
                    }
                    ObjectPropertiesEvent.WaitOne((int)dataTimeout, false);
                    Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                }
            });
            primitives = new HashSet<Primitive>(localPrimitives.Where(o => o.Properties != null));
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates a primitive by scanning its properties.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="primitive">a primitive to scan</param>
        /// <param name="dataTimeout">the timeout for receiving data from the grid</param>
        /// <returns>a list of updated primitives</returns>
        public static bool UpdatePrimitive(GridClient Client, ref Primitive primitive, uint dataTimeout)
        {
            var ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler =
                (sender, args) => { ObjectPropertiesEvent.Set(); };
            var localPrimitive = primitive;
            var regionHandle = localPrimitive.RegionHandle;
            lock (Locks.ClientInstanceObjectsLock)
            {
                Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                ObjectPropertiesEvent.Reset();
                Client.Objects.SelectObject(
                    Client.Network.Simulators.AsParallel().FirstOrDefault(p => p.Handle.Equals(regionHandle)),
                    localPrimitive.LocalID, true);
                ObjectPropertiesEvent.WaitOne((int)dataTimeout, false);
                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
            }
            primitive = localPrimitive;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates a set of avatars by scanning their profile data.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="avatars">a list of avatars to update</param>
        /// <param name="millisecondsTimeout">the amount of time in milliseconds to timeout</param>
        /// <param name="dataTimeout">the data timeout</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>true if any avatars were updated</returns>
        public static bool UpdateAvatars(GridClient Client, ref HashSet<Avatar> avatars, uint millisecondsTimeout,
            uint dataTimeout, DecayingAlarm alarm)
        {
            var scansAvatars = new HashSet<Avatar>(avatars);
            var avatarAlarms =
                new Dictionary<UUID, DecayingAlarm>(scansAvatars.AsParallel()
                    .ToDictionary(o => o.ID, p => alarm.Clone()));
            var avatarUpdates = new Dictionary<UUID, Avatar>(scansAvatars.AsParallel()
                .ToDictionary(o => o.ID, p => p));
            var LockObject = new object();
            EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler = (sender, args) =>
            {
                lock (LockObject)
                {
                    avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    avatarUpdates[args.AvatarID].ProfileInterests = args.Interests;
                }
            };
            EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                        avatarUpdates[args.AvatarID].ProfileProperties = args.Properties;
                    }
                };
            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
            {
                lock (LockObject)
                {
                    avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    avatarUpdates[args.AvatarID].Groups.AddRange(args.Groups.Select(o => o.GroupID));
                }
            };
            EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    }
                };
            EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    }
                };
            lock (Locks.ClientInstanceAvatarsLock)
            {
                Parallel.ForEach(scansAvatars, o =>
                {
                    Client.Avatars.AvatarInterestsReply += AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply += AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply += AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply += AvatarClassifiedReplyEventHandler;
                    Client.Avatars.RequestAvatarProperties(o.ID);
                    Client.Avatars.RequestAvatarPicks(o.ID);
                    Client.Avatars.RequestAvatarClassified(o.ID);
                    DecayingAlarm avatarAlarm;
                    lock (LockObject)
                    {
                        avatarAlarm = avatarAlarms[o.ID];
                    }
                    avatarAlarm.Signal.WaitOne((int)millisecondsTimeout, false);
                    Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                });
            }
            if (
                avatarUpdates.Values.AsParallel()
                    .Any(
                        o =>
                            o != null && !o.ProfileInterests.Equals(default(Avatar.Interests)) &&
                            !o.ProfileProperties.Equals(default(Avatar.AvatarProperties))))
            {
                avatars = new HashSet<Avatar>(avatarUpdates.Values);
                return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named primitive in range (whether attachment or in-world).
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="item">the UUID of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindPrimitive(GridClient Client, UUID item, float range,
            ref Primitive primitive,
            uint dataTimeout)
        {
            var objectsPrimitives = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsPrimitives)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var objectsAvatars = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsAvatars)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var localPrimitive = Client.Network.Simulators.AsParallel()
                .Select(o => new { s = o, a = o.ObjectsPrimitives.Copy().Values })
                .SelectMany(o => o.a.AsParallel().Where(p =>
                {
                    // find the parent of the primitive
                    var parent = p;
                    Primitive ancestorPrimitive;
                    if (objectsPrimitives.TryGetValue(parent.ParentID, out ancestorPrimitive))
                    {
                        parent = ancestorPrimitive;
                    }
                    Avatar ancestorAvatar;
                    if (objectsAvatars.TryGetValue(parent.ParentID, out ancestorAvatar))
                    {
                        parent = ancestorAvatar;
                    }
                    return Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                        Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                })).FirstOrDefault(o => o.ID.Equals(item));
            if (localPrimitive == null || !UpdatePrimitive(Client, ref localPrimitive, dataTimeout))
                return false;
            primitive = localPrimitive;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named object in range (whether attachment or in-world).
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="item">the UUID of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindObject(GridClient Client, UUID item, float range,
            ref Primitive primitive,
            uint dataTimeout)
        {
            var objectsAvatars = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsAvatars)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var localPrimitive = Client.Network.Simulators.AsParallel()
                .Select(o => new { s = o, a = o.ObjectsPrimitives.Copy().Values })
                .SelectMany(o => o.a.AsParallel().Where(p =>
                {
                    // find the parent of the primitive
                    var parent = p;
                    Avatar ancestorAvatar;
                    if (objectsAvatars.TryGetValue(p.ParentID, out ancestorAvatar))
                    {
                        parent = ancestorAvatar;
                    }
                    return (p.ParentID.Equals(0) || objectsAvatars.ContainsKey(p.ParentID)) &&
                           Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                               Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                })).FirstOrDefault(o => o.ID.Equals(item));
            if (localPrimitive == null || !UpdatePrimitive(Client, ref localPrimitive, dataTimeout))
                return false;
            primitive = localPrimitive;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named primitive in range (whether attachment or in-world).
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="item">the name of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindPrimitive(GridClient Client, string item, float range,
            ref Primitive primitive,
            uint dataTimeout)
        {
            var objectsPrimitives = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsPrimitives)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var objectsAvatars = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsAvatars)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var primitives =
                new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                    .Select(o => new { s = o, a = o.ObjectsPrimitives.Copy().Values })
                    .SelectMany(o => o.a.AsParallel().Where(p =>
                    {
                        // find the parent of the primitive
                        var parent = p;
                        Primitive ancestorPrimitive;
                        if (objectsPrimitives.TryGetValue(parent.ParentID, out ancestorPrimitive))
                        {
                            parent = ancestorPrimitive;
                        }
                        Avatar ancestorAvatar;
                        if (objectsAvatars.TryGetValue(parent.ParentID, out ancestorAvatar))
                        {
                            parent = ancestorAvatar;
                        }
                        return Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                            Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                    })));
            if (!primitives.Any() || !UpdatePrimitives(Client, ref primitives, dataTimeout))
                return false;
            var localPrimitive =
                primitives.FirstOrDefault(o => string.Equals(o.Properties.Name, item, StringComparison.Ordinal));
            if (localPrimitive == null)
                return false;
            primitive = localPrimitive;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named object in range (whether attachment or in-world).
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="item">the UUID of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindObject(GridClient Client, string item, float range,
            ref Primitive primitive,
            uint dataTimeout)
        {
            var objectsAvatars = Client.Network.Simulators.AsParallel()
                .Select(o => o.ObjectsAvatars)
                .Select(o => o.Copy().Values)
                .SelectMany(o => o).ToDictionary(o => o.LocalID, p => p);
            var primitives = new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                .Select(o => new { s = o, a = o.ObjectsPrimitives.Copy().Values })
                .SelectMany(o => o.a.AsParallel().Where(p =>
                {
                    // find the parent of the primitive
                    var parent = p;
                    Avatar ancestorAvatar;
                    if (objectsAvatars.TryGetValue(p.ParentID, out ancestorAvatar))
                    {
                        parent = ancestorAvatar;
                    }
                    return (p.ParentID.Equals(0) || objectsAvatars.ContainsKey(p.ParentID)) &&
                           Vector3d.Distance(Helpers.GlobalPosition(o.s, parent.Position),
                               Helpers.GlobalPosition(Client.Network.CurrentSim, Client.Self.SimPosition)) <= range;
                })));
            if (!primitives.Any() || !UpdatePrimitives(Client, ref primitives, dataTimeout))
                return false;
            var localPrimitive =
                primitives.FirstOrDefault(o => string.Equals(o.Properties.Name, item, StringComparison.Ordinal));
            if (localPrimitive == null)
                return false;
            primitive = localPrimitive;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Download a texture by its asset UUID
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="assetUUID">the asset UUID of the texture</param>
        /// <param name="assetData">the asset data where to store the texture</param>
        /// <param name="dataTimeout">the timeout for downloading the texture</param>
        /// <returns>true of the texture could be downloaded successfully</returns>
        private static bool directDownloadTexture(GridClient Client, UUID assetUUID, out byte[] assetData,
            uint dataTimeout)
        {
            var AssetReceivedEvent = new ManualResetEvent(false);
            byte[] localAssetData = null;
            lock (Locks.ClientInstanceAssetsLock)
            {
                Client.Assets.RequestImage(assetUUID, (state, asset) =>
                {
                    if (!asset.AssetID.Equals(assetUUID))
                        return;
                    if (!state.Equals(TextureRequestState.Finished))
                        return;
                    localAssetData = asset.AssetData;
                    AssetReceivedEvent.Set();
                });
            }

            assetData = localAssetData;
            return AssetReceivedEvent.WaitOne((int)dataTimeout, false);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Cache wrapper for downloading a texture by its asset UUID.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="assetUUID">the asset UUID of the texture</param>
        /// <param name="assetData">the asset data where to store the texture</param>
        /// <param name="dataTimeout">the timeout for downloading the texture</param>
        /// <returns>true of the texture could be downloaded successfully</returns>
        public static bool DownloadTexture(GridClient Client, UUID assetUUID, out byte[] assetData, uint dataTimeout)
        {
            lock (Locks.ClientInstanceAssetsLock)
            {
                if (Client.Assets.Cache.HasAsset(assetUUID))
                {
                    assetData = Client.Assets.Cache.GetCachedAssetBytes(assetUUID);
                    return true;
                }
            }
            var succeeded = directDownloadTexture(Client, assetUUID, out assetData, dataTimeout);
            if (succeeded)
            {
                lock (Locks.ClientInstanceAssetsLock)
                {
                    Client.Assets.Cache.SaveAssetToCache(assetUUID, assetData);
                }
            }
            return succeeded;
        }
    }
}
