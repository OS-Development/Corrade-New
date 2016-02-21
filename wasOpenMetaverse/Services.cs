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

namespace wasOpenMetaverse
{
    public class Services
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates the current balance by requesting it from the grid.
        /// </summary>
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
                MuteListUpdatedEvent.WaitOne((int)millisecondsTimeout, false);
                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
            }
            mutes = Client.Self.MuteList.Copy().Values;
            return true;
        }

        /// <summary>
        ///     A wrapper for retrieveing all the current groups that implements caching.
        /// </summary>
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
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetCurrentGroups(GridClient Client, uint millisecondsTimeout, ref IEnumerable<UUID> groups)
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
    }
}