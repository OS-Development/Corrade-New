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
using System.Threading;
using ReaderWriterLockSlim = System.Threading.ReaderWriterLockSlim;

namespace wasOpenMetaverse.Caches
{
    public class AgentCache : ObservableHashSet<Cache.Agent>
    {
        private readonly ReaderWriterLockSlim SyncRoot = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private SerializableDictionary<UUID, Cache.Agent> nameCache = new SerializableDictionary<UUID, Cache.Agent>();

        private MultiKeyDictionary<string, string, Cache.Agent> nameHandleCache =
            new MultiKeyDictionary<string, string, Cache.Agent>();

        private MultiKeyDictionary<string, string, UUID, Cache.Agent> nameUUIDHandleCache =
            new MultiKeyDictionary<string, string, UUID, Cache.Agent>();

        public Cache.Agent this[string firstname, string lastname]
        {
            get
            {
                Cache.Agent agent;
                SyncRoot.EnterReadLock();
                nameHandleCache.TryGetValue(firstname, lastname, out agent);
                SyncRoot.ExitReadLock();
                return agent;
            }
        }

        public Cache.Agent this[UUID @UUID]
        {
            get
            {
                Cache.Agent agent;
                SyncRoot.EnterReadLock();
                nameCache.TryGetValue(@UUID, out agent);
                SyncRoot.ExitReadLock();
                return agent;
            }
        }

        public Cache.Agent this[string firstname, string lastname, UUID @UUID]
        {
            get
            {
                Cache.Agent agent;
                SyncRoot.EnterReadLock();
                nameUUIDHandleCache.TryGetValue(firstname, lastname, @UUID, out agent);
                SyncRoot.ExitReadLock();
                return agent;
            }
        }

        public Cache.Agent this[Cache.Agent agent]
        {
            get
            {
                Cache.Agent r = this[agent.FirstName, agent.LastName];

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
            SyncRoot.EnterWriteLock();
            nameCache.Clear();
            nameHandleCache.Clear();
            nameUUIDHandleCache.Clear();
            base.Clear();
            SyncRoot.ExitWriteLock();
        }

        public new void Add(Cache.Agent agent)
        {
            SyncRoot.EnterWriteLock();
            if (!nameCache.ContainsKey(agent.UUID))
                nameCache.Add(agent.UUID, agent);
            if (!nameHandleCache.ContainsKey(agent.FirstName, agent.LastName))
                nameHandleCache.Add(agent.FirstName, agent.LastName, agent);
            if (!nameUUIDHandleCache.ContainsKey(agent.FirstName, agent.LastName, agent.UUID))
                nameUUIDHandleCache.Add(agent.FirstName, agent.LastName, agent.UUID, agent);

            base.Add(agent);
            SyncRoot.ExitWriteLock();
        }

        public new bool Remove(Cache.Agent agent)
        {
            SyncRoot.EnterWriteLock();
            nameCache.Remove(agent.UUID);
            nameHandleCache.Remove(agent.FirstName, agent.LastName);
            nameUUIDHandleCache.Remove(agent.FirstName, agent.LastName, agent.UUID);
            var v = base.Remove(agent);
            SyncRoot.ExitWriteLock();
            return v;
        }

        public new void UnionWith(IEnumerable<Cache.Agent> list)
        {
            SyncRoot.EnterWriteLock();
            var enumerable = list as IList<Cache.Agent> ?? list.ToList();
            foreach (var agent in enumerable.Except(AsEnumerable()))
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
            }
            base.UnionWith(enumerable);
            SyncRoot.ExitWriteLock();
        }

        public bool Contains(string firstname, string lastname)
        {
            SyncRoot.EnterReadLock();
            var c = nameHandleCache.ContainsKey(firstname, lastname);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(UUID @UUID)
        {
            SyncRoot.ExitReadLock();
            var c = nameCache.ContainsKey(@UUID);
            SyncRoot.ExitReadLock();
            return c;
        }
    }
}
