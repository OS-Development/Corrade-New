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
using Parallel = System.Threading.Tasks.Parallel;

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
            ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
            EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceEventHandler =
                (sender, args) => MoneyBalanceEvent.Set();
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.MoneyBalanceReply += MoneyBalanceEventHandler;
                Client.Self.RequestBalance();
                if (!MoneyBalanceEvent.WaitOne((int) millisecondsTimeout, false))
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
            ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
            EventHandler<EventArgs> MuteListUpdatedEventHandler =
                (sender, args) => MuteListUpdatedEvent.Set();
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                Client.Self.RequestMuteList();
                MuteListUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
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
                if (Cache.MutesCache != null)
                {
                    mutes = Cache.MutesCache;
                    return true;
                }

                succeeded = directGetMutes(Client, millisecondsTimeout, ref mutes);

                if (succeeded)
                {
                    switch (Cache.MutesCache != null)
                    {
                        case true:
                            Cache.MutesCache.UnionWith(mutes);
                            break;
                        default:
                            Cache.MutesCache = new HashSet<MuteEntry>(mutes);
                            break;
                    }
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
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetCurrentGroups(GridClient Client, uint millisecondsTimeout,
            ref IEnumerable<UUID> groups)
        {
            ManualResetEvent CurrentGroupsReceivedEvent = new ManualResetEvent(false);
            Dictionary<UUID, Group> currentGroups = null;
            EventHandler<CurrentGroupsEventArgs> CurrentGroupsEventHandler = (sender, args) =>
            {
                currentGroups = args.Groups;
                CurrentGroupsReceivedEvent.Set();
            };
            Client.Groups.CurrentGroups += CurrentGroupsEventHandler;
            Client.Groups.RequestCurrentGroups();
            if (!CurrentGroupsReceivedEvent.WaitOne((int) millisecondsTimeout, false))
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
                    Cache.CurrentGroupsCache = new HashSet<UUID>(groups);
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
            uint dataTimeout, Time.DecayingAlarm alarm)
        {
            List<AvatarGroup> avatarGroups = new List<AvatarGroup>();
            object LockObject = new object();
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
                if (!alarm.Signal.WaitOne((int) millisecondsTimeout, false))
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
            bool succeeded = false;
            ManualResetEvent GroupChatJoinedEvent = new ManualResetEvent(false);
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
                if (!GroupChatJoinedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                    return false;
                }
                Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
            }
            return succeeded;
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
            ManualResetEvent groupMembersReceivedEvent = new ManualResetEvent(false);
            HashSet<UUID> groupMembers = new HashSet<UUID>();
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                groupMembers.UnionWith(args.Members.Values.Select(o => o.ID));
                groupMembersReceivedEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                Client.Groups.RequestGroupMembers(groupUUID);
                if (!groupMembersReceivedEvent.WaitOne((int) millisecondsTimeout, false))
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
            Group localGroup = new Group();
            ManualResetEvent GroupProfileEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (sender, args) =>
            {
                localGroup = args.Group;
                GroupProfileEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupProfile += GroupProfileDelegate;
                Client.Groups.RequestGroupProfile(groupUUID);
                if (!GroupProfileEvent.WaitOne((int) millisecondsTimeout, false))
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
            ManualResetEvent RequestAllSimParcelsEvent = new ManualResetEvent(false);
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
                if (!RequestAllSimParcelsEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
                    return false;
                }
                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
            }
            Parcel localParcel = simulator.Parcels.Copy().Values
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
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches all the primitives in-range.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="range">the range to extend or contract to</param>
        /// <param name="maxRange">the maximum configured range for the grid</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>the primitives in range</returns>
        public static IEnumerable<Primitive> GetPrimitives(GridClient Client, float range, float maxRange,
            uint millisecondsTimeout, uint dataTimeout, Time.DecayingAlarm alarm)
        {
            switch (Client.Self.Movement.Camera.Far < range)
            {
                case true:
                    IEnumerable<Primitive> primitives;
                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler =
                        (sender, args) =>
                        {
                            // ignore if this is not a new primitive being added
                            if (!args.IsNew) return;
                            alarm.Alarm(dataTimeout);
                        };
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                        lock (Locks.ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = range;
                        }
                        alarm.Alarm(dataTimeout);
                        alarm.Signal.WaitOne((int) millisecondsTimeout, false);
                        primitives =
                            Client.Network.Simulators.AsParallel().Select(o => o.ObjectsPrimitives)
                                .Select(o => o.Copy().Values)
                                .SelectMany(o => o);
                        lock (Locks.ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = maxRange;
                        }
                        Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                    }
                    return primitives;
                default:
                    return Client.Network.Simulators.AsParallel().Select(o => o.ObjectsPrimitives)
                        .Select(o => o.Copy().Values)
                        .SelectMany(o => o);
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
            int primitiveUpdatesCount = 0;
            ManualResetEvent ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler =
                (sender, args) =>
                {
                    Interlocked.Increment(ref primitiveUpdatesCount);
                    ObjectPropertiesEvent.Set();
                };
            BlockingQueue<Primitive> primitiveQueue = new BlockingQueue<Primitive>(primitives);
            do
            {
                Primitive updatePrimitive = primitiveQueue.Dequeue();
                lock (Locks.ClientInstanceObjectsLock)
                {
                    Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                    ObjectPropertiesEvent.Reset();
                    Client.Objects.SelectObject(Client.Network.Simulators.AsParallel()
                        .FirstOrDefault(p => p.Handle.Equals(updatePrimitive.RegionHandle)), updatePrimitive.LocalID,
                        true);
                    ObjectPropertiesEvent.WaitOne((int) dataTimeout, false);
                    Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                }
            } while (primitiveQueue.Any());
            return primitiveUpdatesCount.Equals(primitives.Count());
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
        public static bool UpdatePrimitives(GridClient Client, ref Primitive primitive, uint dataTimeout)
        {
            int primitiveUpdatesCount = 0;
            ManualResetEvent ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler =
                (sender, args) =>
                {
                    Interlocked.Increment(ref primitiveUpdatesCount);
                    ObjectPropertiesEvent.Set();
                };
            Primitive updatePrimitive = primitive;
            lock (Locks.ClientInstanceObjectsLock)
            {
                Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                ObjectPropertiesEvent.Reset();
                Client.Objects.SelectObject(Client.Network.Simulators.AsParallel()
                    .FirstOrDefault(p => p.Handle.Equals(updatePrimitive.RegionHandle)), updatePrimitive.LocalID,
                    true);
                ObjectPropertiesEvent.WaitOne((int) dataTimeout, false);
                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
            }
            return primitiveUpdatesCount.Equals(1);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///     Fetches all the avatars in-range.
        /// </summary>
        /// <param name="Client">the OpenMetaverse grid client</param>
        /// <param name="range">the range to extend or contract to</param>
        /// <param name="maxRange">the maximum configured range for the grid</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>the avatars in range</returns>
        public static IEnumerable<Avatar> GetAvatars(GridClient Client, float range, float maxRange,
            uint millisecondsTimeout, uint dataTimeout, Time.DecayingAlarm alarm)
        {
            switch (Client.Self.Movement.Camera.Far < range)
            {
                case true:
                    IEnumerable<Avatar> avatars;
                    EventHandler<AvatarUpdateEventArgs> AvatarUpdateEventHandler =
                        (sender, args) =>
                        {
                            // ignore if this is not a new avatar being added
                            if (!args.IsNew) return;
                            alarm.Alarm(dataTimeout);
                        };
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.AvatarUpdate += AvatarUpdateEventHandler;
                        lock (Locks.ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = range;
                        }
                        alarm.Alarm(dataTimeout);
                        alarm.Signal.WaitOne((int) millisecondsTimeout, false);
                        avatars =
                            Client.Network.Simulators.AsParallel().Select(o => o.ObjectsAvatars)
                                .Select(o => o.Copy().Values)
                                .SelectMany(o => o);
                        lock (Locks.ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = maxRange;
                        }
                        Client.Objects.AvatarUpdate -= AvatarUpdateEventHandler;
                    }
                    return avatars;
                default:
                    return Client.Network.Simulators.AsParallel().Select(o => o.ObjectsAvatars)
                        .Select(o => o.Copy().Values)
                        .SelectMany(o => o);
            }
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
            uint dataTimeout, Time.DecayingAlarm alarm)
        {
            HashSet<Avatar> scansAvatars = new HashSet<Avatar>(avatars);
            Dictionary<UUID, Time.DecayingAlarm> avatarAlarms =
                new Dictionary<UUID, Time.DecayingAlarm>(scansAvatars.AsParallel()
                    .ToDictionary(o => o.ID, p => alarm.Clone()));
            Dictionary<UUID, Avatar> avatarUpdates = new Dictionary<UUID, Avatar>(scansAvatars.AsParallel()
                .ToDictionary(o => o.ID, p => p));
            object LockObject = new object();
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
                    Time.DecayingAlarm avatarAlarm;
                    lock (LockObject)
                    {
                        avatarAlarm = avatarAlarms[o.ID];
                    }
                    avatarAlarm.Signal.WaitOne((int) millisecondsTimeout, false);
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
        /// <param name="maxRange">the maximum configured range for the grid</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="millisecondsTimeout">the services timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindPrimitive(GridClient Client, UUID item, float range, float maxRange,
            ref Primitive primitive, uint millisecondsTimeout,
            uint dataTimeout, Time.DecayingAlarm alarm)
        {
            Primitive p = GetPrimitives(Client, range, maxRange, millisecondsTimeout, dataTimeout, alarm)
                .AsParallel()
                .FirstOrDefault(o => o.ID.Equals(item));
            if (p == null || !UpdatePrimitives(Client, ref p, dataTimeout))
                return false;
            primitive = p;
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
        /// <param name="maxRange">the maximum configured range for the grid</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="millisecondsTimeout">the services timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <param name="alarm">a decaying alarm for retrieving data</param>
        /// <returns>true if the primitive could be found</returns>
        public static bool FindPrimitive(GridClient Client, string item, float range, float maxRange,
            ref Primitive primitive, uint millisecondsTimeout,
            uint dataTimeout, Time.DecayingAlarm alarm)
        {
            HashSet<Primitive> p =
                new HashSet<Primitive>(GetPrimitives(Client, range, maxRange, millisecondsTimeout, dataTimeout, alarm));
            if (!p.Any() || !UpdatePrimitives(Client, ref p, dataTimeout))
                return false;
            Primitive localPrimitive = p.FirstOrDefault(o => string.Equals(o.Properties.Name, item, StringComparison.Ordinal));
            if (localPrimitive != null)
            {
                primitive = localPrimitive;
                return true;
            } 
            return false;
        }
    }
}