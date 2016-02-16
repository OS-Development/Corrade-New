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

namespace wasOpenMetaverse
{
    public static class Resolvers
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a group name to an UUID by using the directory search.
        /// </summary>
        /// <param name="GroupName">the name of the group to resolve</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="GroupUUID">an object in which to store the UUID of the group</param>
        /// <returns>true if the group name could be resolved to an UUID</returns>
        private static bool directGroupNameToUUID(GridClient Client, string GroupName,
            uint millisecondsTimeout, uint dataTimeout, Time.DecayingAlarm alarm,
            ref UUID GroupUUID)
        {
            UUID groupUUID = UUID.Zero;
            EventHandler<DirGroupsReplyEventArgs> DirGroupsReplyDelegate = (sender, args) =>
            {
                alarm.Alarm(dataTimeout);
                DirectoryManager.GroupSearchData groupSearchData =
                    args.MatchedGroups.AsParallel()
                        .FirstOrDefault(o => o.GroupName.Equals(GroupName, StringComparison.OrdinalIgnoreCase));
                switch (!groupSearchData.Equals(default(DirectoryManager.GroupSearchData)))
                {
                    case true:
                        groupUUID = groupSearchData.GroupID;
                        alarm.Signal.Set();
                        break;
                }
            };
            Client.Directory.DirGroupsReply += DirGroupsReplyDelegate;
            Client.Directory.StartGroupSearch(GroupName, 0);
            if (!alarm.Signal.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
                return false;
            }
            Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
            if (!groupUUID.Equals(UUID.Zero))
            {
                GroupUUID = groupUUID;
                return true;
            }
            return false;
        }

        /// <summary>
        ///     A wrapper for resolving group names to UUIDs by using Corrade's internal cache.
        /// </summary>
        /// <param name="GroupName">the name of the group to resolve</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="GroupUUID">an object in which to store the UUID of the group</param>
        /// <returns>true if the group name could be resolved to an UUID</returns>
        public static bool GroupNameToUUID(GridClient Client, string GroupName, uint millisecondsTimeout,
            uint dataTimeout, Time.DecayingAlarm alarm,
            ref UUID GroupUUID)
        {
            bool succeeded;
            lock (Locks.ClientInstanceDirectoryLock)
            {
                Cache.Groups @group = Cache.GroupCache.AsParallel().FirstOrDefault(o => o.Name.Equals(GroupName));

                if (!@group.Equals(default(Cache.Groups)))
                {
                    GroupUUID = @group.UUID;
                    return true;
                }

                succeeded = directGroupNameToUUID(Client, GroupName, millisecondsTimeout, dataTimeout, alarm,
                    ref GroupUUID);

                if (succeeded)
                {
                    Cache.GroupCache.Add(new Cache.Groups
                    {
                        Name = GroupName,
                        UUID = GroupUUID
                    });
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent name to an agent UUID by searching the directory
        ///     services.
        /// </summary>
        /// <param name="FirstName">the first name of the agent</param>
        /// <param name="LastName">the last name of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="AgentUUID">an object to store the agent UUID</param>
        /// <returns>true if the agent name could be resolved to an UUID</returns>
        private static bool directAgentNameToUUID(GridClient Client, string FirstName, string LastName,
            uint millisecondsTimeout,
            uint dataTimeout,
            Time.DecayingAlarm alarm,
            ref UUID AgentUUID)
        {
            UUID agentUUID = UUID.Zero;
            EventHandler<DirPeopleReplyEventArgs> DirPeopleReplyDelegate = (sender, args) =>
            {
                alarm.Alarm(dataTimeout);
                DirectoryManager.AgentSearchData agentSearchData =
                    args.MatchedPeople.AsParallel().FirstOrDefault(
                        o =>
                            o.FirstName.Equals(FirstName, StringComparison.OrdinalIgnoreCase) &&
                            o.LastName.Equals(LastName, StringComparison.OrdinalIgnoreCase));
                switch (!agentSearchData.Equals(default(DirectoryManager.AgentSearchData)))
                {
                    case true:
                        agentUUID = agentSearchData.AgentID;
                        alarm.Signal.Set();
                        break;
                }
            };
            Client.Directory.DirPeopleReply += DirPeopleReplyDelegate;
            Client.Directory.StartPeopleSearch(
                string.Format(Utils.EnUsCulture, "{0} {1}", FirstName, LastName), 0);
            if (!alarm.Signal.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
                return false;
            }
            Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
            if (!agentUUID.Equals(UUID.Zero))
            {
                AgentUUID = agentUUID;
                return true;
            }
            return false;
        }

        /// <summary>
        ///     A wrapper for looking up an agent name using Corrade's internal wasOpenMetaverse.Cache.
        /// </summary>
        /// <param name="FirstName">the first name of the agent</param>
        /// <param name="LastName">the last name of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="AgentUUID">an object to store the agent UUID</param>
        /// <returns>true if the agent name could be resolved to an UUID</returns>
        public static bool AgentNameToUUID(GridClient Client, string FirstName, string LastName,
            uint millisecondsTimeout,
            uint dataTimeout,
            Time.DecayingAlarm alarm,
            ref UUID AgentUUID)
        {
            bool succeeded;
            lock (Locks.ClientInstanceDirectoryLock)
            {
                Cache.Agents agent = Cache.GetAgent(FirstName, LastName);
                if (!agent.Equals(default(Cache.Agents)))
                {
                    AgentUUID = agent.UUID;
                    return true;
                }

                succeeded = directAgentNameToUUID(Client, FirstName, LastName, millisecondsTimeout, dataTimeout, alarm,
                    ref AgentUUID);

                if (succeeded)
                {
                    Cache.AddAgent(FirstName, LastName, AgentUUID);
                }
            }
            return succeeded;
        }


        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a group name to an UUID by using the directory search.
        /// </summary>
        /// <param name="GroupName">a string to store the name to</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="GroupUUID">the UUID of the group to resolve</param>
        /// <returns>true if the group UUID could be resolved to an name</returns>
        private static bool directGroupUUIDToName(GridClient Client, UUID GroupUUID, uint millisecondsTimeout,
            ref string GroupName)
        {
            string groupName = string.Empty;
            ManualResetEvent GroupProfileReceivedEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (o, s) =>
            {
                if (s.Group.ID.Equals(GroupUUID))
                    groupName = s.Group.Name;
                GroupProfileReceivedEvent.Set();
            };
            Client.Groups.GroupProfile += GroupProfileDelegate;
            Client.Groups.RequestGroupProfile(GroupUUID);
            if (!GroupProfileReceivedEvent.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Groups.GroupProfile -= GroupProfileDelegate;
                return false;
            }
            Client.Groups.GroupProfile -= GroupProfileDelegate;
            if (!string.IsNullOrEmpty(groupName))
            {
                GroupName = groupName;
                return true;
            }
            return false;
        }

        /// <summary>
        ///     A wrapper for resolving group names to UUIDs by using Corrade's internal cache.
        /// </summary>
        /// <param name="GroupName">a string to store the name to</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="GroupUUID">the UUID of the group to resolve</param>
        /// <returns>true if the group UUID could be resolved to an name</returns>
        public static bool GroupUUIDToName(GridClient Client, UUID GroupUUID, uint millisecondsTimeout,
            ref string GroupName)
        {
            bool succeeded;
            lock (Locks.ClientInstanceGroupsLock)
            {
                Cache.Groups @group = Cache.GroupCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(GroupUUID));

                if (!@group.Equals(default(Cache.Groups)))
                {
                    GroupName = @group.Name;
                    return true;
                }

                succeeded = directGroupUUIDToName(Client, GroupUUID, millisecondsTimeout, ref GroupName);

                if (succeeded)
                {
                    Cache.GroupCache.Add(new Cache.Groups
                    {
                        Name = GroupName,
                        UUID = GroupUUID
                    });
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent UUID to an agent name.
        /// </summary>
        /// <param name="AgentUUID">the UUID of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="AgentName">an object to store the name of the agent in</param>
        /// <returns>true if the UUID could be resolved to a name</returns>
        private static bool directAgentUUIDToName(GridClient Client, UUID AgentUUID, uint millisecondsTimeout,
            ref string AgentName)
        {
            if (AgentUUID.Equals(UUID.Zero))
                return false;
            string agentName = string.Empty;
            ManualResetEvent UUIDNameReplyEvent = new ManualResetEvent(false);
            EventHandler<UUIDNameReplyEventArgs> UUIDNameReplyDelegate = (sender, args) =>
            {
                args.Names.TryGetValue(AgentUUID, out agentName);
                UUIDNameReplyEvent.Set();
            };
            Client.Avatars.UUIDNameReply += UUIDNameReplyDelegate;
            Client.Avatars.RequestAvatarName(AgentUUID);
            if (!UUIDNameReplyEvent.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
                return false;
            }
            Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
            if (!string.IsNullOrEmpty(agentName))
            {
                AgentName = agentName;
                return true;
            }
            return false;
        }

        /// <summary>
        ///     A wrapper for agent to UUID lookups using Corrade's internal cache.
        /// </summary>
        /// <param name="AgentUUID">the UUID of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="AgentName">an object to store the name of the agent in</param>
        /// <returns>true if the UUID could be resolved to a name</returns>
        public static bool AgentUUIDToName(GridClient Client, UUID AgentUUID, uint millisecondsTimeout,
            ref string AgentName)
        {
            bool succeeded;
            lock (Locks.ClientInstanceAvatarsLock)
            {
                Cache.Agents agent = Cache.GetAgent(AgentUUID);
                if (!agent.Equals(default(Cache.Agents)))
                {
                    AgentName = string.Join(" ", agent.FirstName, agent.LastName);
                    return true;
                }

                succeeded = directAgentUUIDToName(Client, AgentUUID, millisecondsTimeout, ref AgentName);

                if (succeeded)
                {
                    List<string> name = new List<string>(Helpers.GetAvatarNames(AgentName));
                    Cache.AddAgent(name.First(), name.Last(), AgentUUID);
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////

        /// ///
        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="RoleName">the name of the role to be resolved to an UUID</param>
        /// <param name="GroupUUID">the UUID of the group to query for the role UUID</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="RoleUUID">an UUID object to store the role UUID in</param>
        /// <returns>true if the role could be found</returns>
        public static bool RoleNameToUUID(GridClient Client, string RoleName, UUID GroupUUID, uint millisecondsTimeout,
            ref UUID RoleUUID)
        {
            switch (!RoleName.Equals(Constants.GROUPS.EVERYONE_ROLE_NAME, StringComparison.Ordinal))
            {
                case false:
                    RoleUUID = UUID.Zero;
                    return true;
            }
            ManualResetEvent GroupRoleDataReceivedEvent = new ManualResetEvent(false);
            UUID roleUUID = UUID.Zero;
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                roleUUID =
                    args.Roles.AsParallel().Where(o => o.Value.Name.Equals(RoleName, StringComparison.Ordinal))
                        .Select(o => o.Key)
                        .FirstOrDefault();
                GroupRoleDataReceivedEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
                Client.Groups.RequestGroupRoles(GroupUUID);
                if (!GroupRoleDataReceivedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                    return false;
                }
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            }
            if (!roleUUID.Equals(UUID.Zero))
            {
                RoleUUID = roleUUID;
                return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="RoleUUID">the UUID of the role to be resolved to a name</param>
        /// <param name="GroupUUID">the UUID of the group to query for the role name</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="roleName">a string object to store the role name in</param>
        /// <returns>true if the role could be resolved</returns>
        public static bool RoleUUIDToName(GridClient Client, UUID RoleUUID, UUID GroupUUID, uint millisecondsTimeout,
            uint dataTimeout,
            ref string roleName)
        {
            switch (!RoleUUID.Equals(UUID.Zero))
            {
                case false:
                    roleName = Constants.GROUPS.EVERYONE_ROLE_NAME;
                    return true;
            }
            ManualResetEvent GroupRoleDataReceivedEvent = new ManualResetEvent(false);
            GroupRole groupRole = new GroupRole();
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                args.Roles.TryGetValue(RoleUUID, out groupRole);
                GroupRoleDataReceivedEvent.Set();
            };
            lock (Locks.ClientInstanceGroupsLock)
            {
                Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
                Client.Groups.RequestGroupRoles(GroupUUID);
                if (!GroupRoleDataReceivedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                    return false;
                }
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            }
            if (!groupRole.Equals(default(GroupRole)))
            {
                roleName = groupRole.Name;
                return true;
            }
            return false;
        }
    }
}