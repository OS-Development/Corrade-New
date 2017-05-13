///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasSharp.Collections.Specialized;
using wasSharp.Collections.Generic;

namespace wasOpenMetaverse.Caches
{
    public class AgentCache : ObservableHashSet<Cache.Agent>
    {
        private readonly object SyncRoot = new object();

        public SerializableDictionary<UUID, Cache.Agent> nameCache = new SerializableDictionary<UUID, Cache.Agent>();

        public MultiKeyDictionary<string, string, Cache.Agent> nameHandleCache =
            new MultiKeyDictionary<string, string, Cache.Agent>();

        public MultiKeyDictionary<string, string, UUID, Cache.Agent> nameUUIDHandleCache =
            new MultiKeyDictionary<string, string, UUID, Cache.Agent>();

        public Cache.Agent this[string firstname, string lastname]
        {
            get
            {
                Cache.Agent agent;
                lock (SyncRoot)
                {
                    nameHandleCache.TryGetValue(firstname, lastname, out agent);
                }
                return agent;
            }
        }

        public Cache.Agent this[UUID @UUID]
        {
            get
            {
                Cache.Agent agent;
                lock (SyncRoot)
                {
                    nameCache.TryGetValue(@UUID, out agent);
                }
                return agent;
            }
        }

        public Cache.Agent this[string firstname, string lastname, UUID @UUID]
        {
            get
            {
                Cache.Agent agent;
                lock (SyncRoot)
                {
                    nameUUIDHandleCache.TryGetValue(firstname, lastname, @UUID, out agent);
                }
                return agent;
            }
        }

        public Cache.Agent this[Cache.Agent agent]
        {
            get
            {
                Cache.Agent r;

                lock (SyncRoot)
                {
                    r = this[agent.FirstName, agent.LastName];
                }

                if (!r.Equals(default(Cache.Agent)))
                    return r;

                if (!agent.UUID.Equals(UUID.Zero))
                {
                    lock (SyncRoot)
                    {
                        r = this[agent.UUID];
                    }
                    if (!r.Equals(default(Cache.Agent)))
                        return r;
                }

                return default(Cache.Agent);
            }
        }

        public new void Clear()
        {
            lock (SyncRoot)
            {
                nameCache.Clear();
                nameHandleCache.Clear();
                nameUUIDHandleCache.Clear();
                base.Clear();
            }
        }

        public new void Add(Cache.Agent agent)
        {
            lock (SyncRoot)
            {
                if (!nameCache.ContainsKey(agent.UUID))
                    nameCache.Add(agent.UUID, agent);
                if (!nameHandleCache.ContainsKey(agent.FirstName, agent.LastName))
                    nameHandleCache.Add(agent.FirstName, agent.LastName, agent);
                if (!nameUUIDHandleCache.ContainsKey(agent.FirstName, agent.LastName, agent.UUID))
                    nameUUIDHandleCache.Add(agent.FirstName, agent.LastName, agent.UUID, agent);
                base.Add(agent);
            }
        }

        public new bool Remove(Cache.Agent agent)
        {
            lock (SyncRoot)
            {
                nameCache.Remove(agent.UUID);
                nameHandleCache.Remove(agent.FirstName, agent.LastName);
                nameUUIDHandleCache.Remove(agent.FirstName, agent.LastName, agent.UUID);
                return base.Remove(agent);
            }
        }

        public new void UnionWith(IEnumerable<Cache.Agent> list)
        {
            lock (SyncRoot)
            {
                var enumerable = new HashSet<Cache.Agent>(list);
                enumerable.Except(AsEnumerable()).AsParallel().ForAll(agent =>
                {
                    if (nameCache.ContainsKey(agent.UUID))
                        nameCache.Remove(agent.UUID);
                    nameCache.Add(agent.UUID, agent);

                    if (nameHandleCache.ContainsKey(agent.FirstName, agent.LastName))
                        nameHandleCache.Remove(agent.FirstName, agent.LastName);
                    nameHandleCache.Add(agent.FirstName, agent.LastName, agent);

                    if (nameUUIDHandleCache.ContainsKey(agent.FirstName, agent.LastName, agent.UUID))
                        nameUUIDHandleCache.Remove(agent.FirstName, agent.LastName, agent.UUID);

                    nameUUIDHandleCache.Add(agent.FirstName, agent.LastName, agent.UUID, agent);
                });

                base.UnionWith(enumerable);
            }
        }

        public bool Contains(string firstname, string lastname)
        {
            lock (SyncRoot)
            {
                return nameHandleCache.ContainsKey(firstname, lastname);
            }
        }

        public bool Contains(UUID @UUID)
        {
            lock (SyncRoot)
            {
                return nameCache.ContainsKey(@UUID);
            }
        }
    }
}
