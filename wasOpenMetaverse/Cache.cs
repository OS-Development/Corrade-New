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

namespace wasOpenMetaverse
{
    public static class Cache
    {
        private static HashSet<Agents> _agentCache = new HashSet<Agents>();
        private static HashSet<Groups> _groupCache = new HashSet<Groups>();
        private static HashSet<UUID> _currentGroupsCache = new HashSet<UUID>();
        private static HashSet<MuteEntry> _mutesCache;
        private static readonly object AgentCacheLock = new object();
        private static readonly object GroupCacheLock = new object();
        private static readonly object CurrentGroupsCacheLock = new object();
        private static readonly object MutesCacheLock = new object();

        public static HashSet<Agents> AgentCache
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
                }
            }
        }

        public static HashSet<Groups> GroupCache
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

        public static HashSet<MuteEntry> MutesCache
        {
            get
            {
                lock (MutesCacheLock)
                {
                    return _mutesCache;
                }
            }
            set
            {
                lock (AgentCacheLock)
                {
                    _mutesCache = value;
                }
            }
        }

        internal static void Purge()
        {
            AgentCache.Clear();
            GroupCache.Clear();
            CurrentGroupsCache.Clear();
            MutesCache.Clear();
        }

        public static void AddAgent(string FirstName, string LastName, UUID agentUUID)
        {
            Agents agent = new Agents
            {
                FirstName = FirstName,
                LastName = LastName,
                UUID = agentUUID
            };

            if (!AgentCache.Contains(agent))
            {
                AgentCache.Add(agent);
            }
        }

        public static Agents GetAgent(string FirstName, string LastName)
        {
            return AgentCache.AsParallel().FirstOrDefault(
                o =>
                    o.FirstName.Equals(FirstName, StringComparison.OrdinalIgnoreCase) &&
                    o.LastName.Equals(LastName, StringComparison.OrdinalIgnoreCase));
        }

        public static Agents GetAgent(UUID agentUUID)
        {
            return AgentCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(agentUUID));
        }

        public static void AddGroup(string GroupName, UUID GroupUUID)
        {
            Groups group = new Groups
            {
                Name = GroupName,
                UUID = GroupUUID
            };
            if (!GroupCache.Contains(group))
            {
                GroupCache.Add(group);
            }
        }

        /// <summary>
        ///     Serializes to a file.
        /// </summary>
        /// <param name="FileName">File path of the new xml file</param>
        /// <param name="o">the object to save</param>
        public static void Save<T>(string FileName, T o)
        {
            using (
                FileStream fileStream = File.Open(FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (T));
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

            using (FileStream fileStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (T));
                    return (T) serializer.Deserialize(streamReader);
                }
            }
        }

        public struct Agents
        {
            public string FirstName;
            public string LastName;
            public UUID UUID;
        }

        public struct Groups
        {
            public string Name;
            public UUID UUID;
        }
    }
}