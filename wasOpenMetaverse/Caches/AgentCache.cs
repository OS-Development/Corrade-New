///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasSharp.Collections.Specialized;

namespace wasOpenMetaverse.Caches
{
    public class AgentCache : ObservableHashSet<Cache.Agent>
    {
        public Dictionary<UUID, Cache.Agent> nameCache = new Dictionary<UUID, Cache.Agent>();

        public MultiKeyDictionary<string, string, Cache.Agent> nameHandleCache =
            new MultiKeyDictionary<string, string, Cache.Agent>();

        public MultiKeyDictionary<string, string, UUID, Cache.Agent> nameUUIDHandleCache =
            new MultiKeyDictionary<string, string, UUID, Cache.Agent>();

        public Cache.Agent this[string firstname, string lastname]
        {
            get
            {
                Cache.Agent agent;
                nameHandleCache.TryGetValue(firstname, lastname, out agent);
                return agent;
            }
        }

        public Cache.Agent this[UUID UUID]
        {
            get
            {
                Cache.Agent agent;
                nameCache.TryGetValue(UUID, out agent);
                return agent;
            }
        }

        public Cache.Agent this[string firstname, string lastname, UUID UUID]
        {
            get
            {
                Cache.Agent agent;
                nameUUIDHandleCache.TryGetValue(firstname, lastname, UUID, out agent);
                return agent;
            }
        }

        public Cache.Agent this[Cache.Agent agent]
        {
            get
            {
                var r = this[agent.FirstName, agent.LastName];
                if (!r.Equals(default(Cache.Agent)))
                    return r;

                if (!agent.UUID.Equals(UUID.Zero))
                {
                    r = this[agent.UUID];
                    if (!r.Equals(default(Cache.Agent)))
                        return r;
                }

                return default(Cache.Agent);
            }
        }

        public new void Clear()
        {
            nameCache.Clear();
            nameHandleCache.Clear();
            nameUUIDHandleCache.Clear();
            base.Clear();
        }

        public new void Add(Cache.Agent agent)
        {
            if (!nameCache.ContainsKey(agent.UUID))
                nameCache.Add(agent.UUID, agent);
            if (!nameHandleCache.ContainsKey(agent.FirstName, agent.LastName))
                nameHandleCache.Add(agent.FirstName, agent.LastName, agent);
            if (!nameUUIDHandleCache.ContainsKey(agent.FirstName, agent.LastName, agent.UUID))
                nameUUIDHandleCache.Add(agent.FirstName, agent.LastName, agent.UUID, agent);
            base.Add(agent);
        }

        public new bool Remove(Cache.Agent agent)
        {
            nameCache.Remove(agent.UUID);
            nameHandleCache.Remove(agent.FirstName, agent.LastName);
            nameUUIDHandleCache.Remove(agent.FirstName, agent.LastName, agent.UUID);
            return base.Remove(agent);
        }

        public new void UnionWith(IEnumerable<Cache.Agent> list)
        {
            var enumerable = list as Cache.Agent[] ?? list.ToArray();
            enumerable.Except(AsEnumerable()).AsParallel().ForAll(agent =>
            {
                nameCache.Remove(agent.UUID);
                nameHandleCache.Remove(agent.FirstName, agent.LastName);
                nameUUIDHandleCache.Remove(agent.FirstName, agent.LastName, agent.UUID);
            });

            base.UnionWith(enumerable);
        }

        public bool Contains(string firstname, string lastname)
        {
            return nameHandleCache.ContainsKey(firstname, lastname);
        }

        public bool Contains(UUID UUID)
        {
            return nameCache.ContainsKey(UUID);
        }
    }
}