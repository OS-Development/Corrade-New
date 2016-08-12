///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using OpenMetaverse;

namespace wasOpenMetaverse
{
    public static class Cache
    {
        public static readonly ObservableCollection<Region> ObservableRegionCache = new ObservableCollection<Region>();
        private static HashSet<Region> _regionCache = new HashSet<Region>();
        public static readonly ObservableCollection<Agent> ObservableAgentCache = new ObservableCollection<Agent>();
        private static HashSet<Agent> _agentCache = new HashSet<Agent>();
        public static readonly ObservableCollection<Group> ObservableGroupCache = new ObservableCollection<Group>();
        private static HashSet<Group> _groupCache = new HashSet<Group>();
        public static readonly ObservableCollection<MuteEntry> ObservableMuteCache = new ObservableCollection<MuteEntry>();
        private static HashSet<MuteEntry> _muteCache = new HashSet<MuteEntry>();

        private static HashSet<UUID> _currentGroupsCache = new HashSet<UUID>();
        private static readonly object RegionCacheLock = new object();
        private static readonly object AgentCacheLock = new object();
        private static readonly object GroupCacheLock = new object();
        private static readonly object CurrentGroupsCacheLock = new object();
        private static readonly object MuteCacheLock = new object();

        public static HashSet<Region> RegionCache
        {
            get
            {
                lock (RegionCacheLock)
                {
                    return _regionCache;
                }
            }
            set
            {
                lock (RegionCacheLock)
                {
                    _regionCache = value;
                    ObservableRegionCache.Clear();
                    foreach (var region in value)
                        ObservableRegionCache.Add(region);
                }
            }
        }

        public static HashSet<Agent> AgentCache
        {
            get
            {
                lock (AgentCacheLock)
                {
                    return _agentCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    _agentCache = value;
                    ObservableAgentCache.Clear();
                    foreach (var agent in value)
                        ObservableAgentCache.Add(agent);
                }
            }
        }

        public static HashSet<Group> GroupCache
        {
            get
            {
                lock (GroupCacheLock)
                {
                    return _groupCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    _groupCache = value;
                    ObservableGroupCache.Clear();
                    foreach (var agent in value)
                        ObservableGroupCache.Add(agent);
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

        public static HashSet<MuteEntry> MuteCache
        {
            get
            {
                lock (MuteCacheLock)
                {
                    return _muteCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    _muteCache = value;
                    ObservableMuteCache.Clear();
                    foreach (var mute in value)
                        ObservableMuteCache.Add(mute);
                }
            }
        }

        public static void Purge()
        {
            lock (RegionCacheLock)
            {
                _regionCache.Clear();
                ObservableRegionCache.Clear();
            }
            lock (AgentCacheLock)
            {
                _agentCache.Clear();
                ObservableAgentCache.Clear();
            }
            lock (GroupCacheLock)
            {
                _groupCache.Clear();
                ObservableGroupCache.Clear();
            }
            lock (CurrentGroupsCacheLock)
            {
                _currentGroupsCache.Clear();
            }
            lock (MuteCacheLock)
            {
                _muteCache.Clear();
                ObservableMuteCache.Clear();
            }
        }

        public static bool AddRegion(string name, ulong handle)
        {
            return AddRegion(new Region
            {
                Name = name,
                Handle = handle
            });
        }

        public static bool AddRegion(Region region)
        {
            lock (RegionCacheLock)
            {
                if (!_regionCache.Contains(region))
                {
                    _regionCache.Add(region);
                    ObservableRegionCache.Add(region);
                    return true;
                }
                return false;
            }
        }

        public static Region GetRegion(string name)
        {
            lock (RegionCacheLock)
            {
                return
                    _regionCache.AsParallel()
                        .FirstOrDefault(o => string.Equals(name, o.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool AddMute(MuteFlags flags, UUID uuid, string name, MuteType type)
        {
            return AddMute(new MuteEntry
            {
                Flags = flags,
                ID = uuid,
                Name = name,
                Type = type
            });
        }

        public static bool AddMute(MuteEntry muteEntry)
        {
            lock (MuteCacheLock)
            {
                if (!_muteCache.Contains(muteEntry))
                {
                    _muteCache.Add(muteEntry);
                    ObservableMuteCache.Add(muteEntry);
                    return true;
                }
                return false;
            }
        }

        public static void RemoveMute(MuteEntry mute)
        {
            lock (MuteCacheLock)
            {
                _muteCache.Remove(mute);
            }
        }

        public static bool AddAgent(string FirstName, string LastName, UUID agentUUID)
        {
            return AddAgent(new Agent
            {
                FirstName = FirstName,
                LastName = LastName,
                UUID = agentUUID
            });
        }

        public static bool AddAgent(Agent agent)
        {
            lock (AgentCacheLock)
            {
                if (!_agentCache.Contains(agent))
                {
                    _agentCache.Add(agent);
                    ObservableAgentCache.Add(agent);
                    return true;
                }
                return false;
            }
        }

        public static Agent GetAgent(string FirstName, string LastName)
        {
            lock (AgentCacheLock)
            {
                return _agentCache.AsParallel().FirstOrDefault(
                    o =>
                        o.FirstName.Equals(FirstName, StringComparison.OrdinalIgnoreCase) &&
                        o.LastName.Equals(LastName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static Agent GetAgent(UUID agentUUID)
        {
            lock (AgentCacheLock)
            {
                return _agentCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(agentUUID));
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
            return AddGroup(new Group
            {
                Name = GroupName,
                UUID = GroupUUID
            });
        }

        public static bool AddGroup(Group group)
        {
            lock (GroupCacheLock)
            {
                if (!_groupCache.Contains(group))
                {
                    _groupCache.Add(group);
                    ObservableGroupCache.Add(group);
                    return true;
                }
                return false;
            }
        }

        public static Group GetGroup(string GroupName)
        {
            lock (GroupCacheLock)
            {
                return
                    _groupCache.ToArray()
                        .AsParallel()
                        .FirstOrDefault(o => o.Name.Equals(GroupName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static Group GetGroup(UUID GroupUUID)
        {
            lock (GroupCacheLock)
            {
                return _groupCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(GroupUUID));
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