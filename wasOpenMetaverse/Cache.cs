///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using OpenMetaverse;
using wasSharp;

namespace wasOpenMetaverse
{
    public static class Cache
    {
        public static readonly Collections.ObservableHashSet<Region> ObservableRegionCache =
            new Collections.ObservableHashSet<Region>();

        public static readonly Collections.ObservableHashSet<Agent> ObservableAgentCache =
            new Collections.ObservableHashSet<Agent>();

        public static readonly Collections.ObservableHashSet<Group> ObservableGroupCache =
            new Collections.ObservableHashSet<Group>();

        public static readonly Collections.ObservableHashSet<MuteEntry> ObservableMuteCache =
            new Collections.ObservableHashSet<MuteEntry>();

        private static HashSet<UUID> _currentGroupsCache = new HashSet<UUID>();
        private static readonly object RegionCacheLock = new object();
        private static readonly object AgentCacheLock = new object();
        private static readonly object GroupCacheLock = new object();
        private static readonly object CurrentGroupsCacheLock = new object();
        private static readonly object MuteCacheLock = new object();

        public static Collections.ObservableHashSet<Region> RegionCache
        {
            get
            {
                lock (RegionCacheLock)
                {
                    return ObservableRegionCache;
                }
            }
            set
            {
                lock (RegionCacheLock)
                {
                    ObservableRegionCache.AddRange(value);
                }
            }
        }

        public static Collections.ObservableHashSet<Agent> AgentCache
        {
            get
            {
                lock (AgentCacheLock)
                {
                    return ObservableAgentCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    ObservableAgentCache.AddRange(value);
                }
            }
        }

        public static Collections.ObservableHashSet<Group> GroupCache
        {
            get
            {
                lock (GroupCacheLock)
                {
                    return ObservableGroupCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    ObservableGroupCache.AddRange(value);
                }
            }
        }

        public static HashSet<UUID> CurrentGroupsCache
        {
            get
            {
                lock (CurrentGroupsCacheLock)
                {
                    return _currentGroupsCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    _currentGroupsCache = value;
                }
            }
        }

        public static Collections.ObservableHashSet<MuteEntry> MuteCache
        {
            get
            {
                lock (MuteCacheLock)
                {
                    return ObservableMuteCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    ObservableMuteCache.AddRange(value);
                }
            }
        }

        public static void Purge()
        {
            lock (RegionCacheLock)
            {
                ObservableRegionCache.Clear();
            }
            lock (AgentCacheLock)
            {
                ObservableAgentCache.Clear();
            }
            lock (GroupCacheLock)
            {
                ObservableGroupCache.Clear();
            }
            lock (CurrentGroupsCacheLock)
            {
                _currentGroupsCache.Clear();
            }
            lock (MuteCacheLock)
            {
                ObservableMuteCache.Clear();
            }
        }

        public static bool AddRegion(string name, ulong handle)
        {
            return !string.IsNullOrEmpty(name) && !handle.Equals(0) && AddRegion(new Region
            {
                Name = name,
                Handle = handle
            });
        }

        private static bool AddRegion(Region region)
        {
            lock (RegionCacheLock)
            {
                if (ObservableRegionCache.Contains(region)) return false;
                ObservableRegionCache.Add(region);
                return true;
            }
        }

        public static bool RemoveRegion(string name, ulong handle)
        {
            Region region;
            lock (RegionCacheLock)
            {
                region =
                    ObservableRegionCache.AsParallel()
                        .FirstOrDefault(o => string.Equals(name, o.Name, StringComparison.OrdinalIgnoreCase) && handle.Equals(o.Handle));
            }
            if (region.Equals(default(Region)))
                return false;
            lock (RegionCacheLock)
            {
                return ObservableRegionCache.Remove(region);
            }
        }

        public static Region GetRegion(string name)
        {
            lock (RegionCacheLock)
            {
                return
                    ObservableRegionCache.AsParallel()
                        .FirstOrDefault(o => string.Equals(name, o.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool AddMute(MuteFlags flags, UUID uuid, string name, MuteType type)
        {
            return !uuid.Equals(UUID.Zero) && !string.IsNullOrEmpty(name) && AddMute(new MuteEntry
            {
                Flags = flags,
                ID = uuid,
                Name = name,
                Type = type
            });
        }

        private static bool AddMute(MuteEntry muteEntry)
        {
            lock (MuteCacheLock)
            {
                if (ObservableMuteCache.Contains(muteEntry)) return false;
                ObservableMuteCache.Add(muteEntry);
                return true;
            }
        }

        public static bool RemoveMute(MuteFlags flags, UUID muteUUID, string name, MuteType type)
        {
            MuteEntry mute;
            lock (MuteCacheLock)
            {
                mute =
                    ObservableMuteCache.AsParallel()
                        .FirstOrDefault(
                            o =>
                                muteUUID.Equals(o.ID) && flags.Equals(o.Flags) && type.Equals(o.Type) && string.Equals(name, o.Name, StringComparison.Ordinal));
            }
            if (mute == null || mute.Equals(default(MuteEntry)))
                return false;
            lock (MuteCacheLock)
            {
                return ObservableMuteCache.Remove(mute);
            }
        }

        public static bool AddAgent(string FirstName, string LastName, UUID agentUUID)
        {
            return !string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName) && !agentUUID.Equals(UUID.Zero) &&
                   AddAgent(new Agent
                   {
                       FirstName = FirstName,
                       LastName = LastName,
                       UUID = agentUUID
                   });
        }

        private static bool AddAgent(Agent agent)
        {
            lock (AgentCacheLock)
            {
                if (ObservableAgentCache.Contains(agent)) return false;
                ObservableAgentCache.Add(agent);
                return true;
            }
        }

        public static bool RemoveAgent(string firstName, string lastName, UUID agentUUID)
        {
            Agent agent;
            lock (AgentCacheLock)
            {
                agent =
                    ObservableAgentCache.AsParallel()
                        .FirstOrDefault(
                            o =>
                                string.Equals(firstName, o.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(lastName, o.LastName, StringComparison.OrdinalIgnoreCase) &&
                                agentUUID.Equals(o.UUID));
            }
            if (agent.Equals(default(Agent)))
                return false;
            lock (AgentCacheLock)
            {
                return ObservableAgentCache.Remove(agent);
            }
        }

        public static Agent GetAgent(string FirstName, string LastName)
        {
            lock (AgentCacheLock)
            {
                return ObservableAgentCache.AsParallel().FirstOrDefault(
                    o =>
                        o.FirstName.Equals(FirstName, StringComparison.OrdinalIgnoreCase) &&
                        o.LastName.Equals(LastName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static Agent GetAgent(UUID agentUUID)
        {
            lock (AgentCacheLock)
            {
                return ObservableAgentCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(agentUUID));
            }
        }

        public static void AddCurrentGroup(UUID GroupUUID)
        {
            lock (CurrentGroupsCacheLock)
            {
                if (!_currentGroupsCache.Contains(GroupUUID))
                {
                    _currentGroupsCache.Add(GroupUUID);
                }
            }
        }

        public static bool AddGroup(string GroupName, UUID GroupUUID)
        {
            return !string.IsNullOrEmpty(GroupName) && !GroupUUID.Equals(UUID.Zero) && AddGroup(new Group
            {
                Name = GroupName,
                UUID = GroupUUID
            });
        }

        private static bool AddGroup(Group group)
        {
            lock (GroupCacheLock)
            {
                if (ObservableGroupCache.Contains(@group)) return false;
                ObservableGroupCache.Add(@group);
                return true;
            }
        }

        public static bool RemoveGroup(string name, UUID groupUUID)
        {
            Group group;
            lock (RegionCacheLock)
            {
                group =
                    ObservableGroupCache.AsParallel()
                        .FirstOrDefault(o => string.Equals(name, o.Name, StringComparison.OrdinalIgnoreCase) && groupUUID.Equals(o.UUID));
            }
            if (group.Equals(default(Group)))
                return false;
            lock (GroupCacheLock)
            {
                return ObservableGroupCache.Remove(group);
            }
        }

        public static Group GetGroup(string GroupName)
        {
            lock (GroupCacheLock)
            {
                return
                    ObservableGroupCache.ToArray()
                        .AsParallel()
                        .FirstOrDefault(o => o.Name.Equals(GroupName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static Group GetGroup(UUID GroupUUID)
        {
            lock (GroupCacheLock)
            {
                return ObservableGroupCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(GroupUUID));
            }
        }

        /// <summary>
        ///     Serializes to a file.
        /// </summary>
        /// <param name="FileName">File path of the new xml file</param>
        /// <param name="o">the object to save</param>
        public static void Save<T>(string FileName, T o)
        {
            using (var fileStream = File.Open(FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    var serializer = new XmlSerializer(typeof (T));
                    serializer.Serialize(writer, o);
                    writer.Flush();
                }
            }
        }

        /// <summary>
        ///     Load an object from an xml file
        /// </summary>
        /// <param name="FileName">Xml file name</param>
        /// <param name="o">the object to load to</param>
        /// <returns>The object created from the xml file</returns>
        public static T Load<T>(string FileName, T o)
        {
            if (!File.Exists(FileName)) return o;

            using (var fileStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    var serializer = new XmlSerializer(typeof (T));
                    return (T) serializer.Deserialize(streamReader);
                }
            }
        }

        [XmlRoot("Region")]
        public struct Region
        {
            [XmlElement("Name")] public string Name;
            [XmlElement("Handle")] public ulong Handle;
        }

        [XmlRoot("Agent")]
        public struct Agent
        {
            [XmlElement("FirstName")] public string FirstName;
            [XmlElement("LastName")] public string LastName;
            [XmlElement("UUID")] public UUID UUID;
        }

        [XmlRoot("Group")]
        public struct Group
        {
            [XmlElement("Name")] public string Name;
            [XmlElement("UUID")] public UUID UUID;
        }
    }
}