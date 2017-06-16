///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasSharp.Collections.Specialized;
using System.Threading;
using ReaderWriterLockSlim = System.Threading.ReaderWriterLockSlim;

namespace wasOpenMetaverse.Caches
{
    public class GroupCache : ObservableHashSet<Cache.Group>
    {
        private readonly ReaderWriterLockSlim SyncRoot = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private Dictionary<UUID, Cache.Group> nameCache = new Dictionary<UUID, Cache.Group>();
        private Dictionary<string, Cache.Group> groupCache = new Dictionary<string, Cache.Group>();

        private MultiKeyDictionary<string, UUID, Cache.Group> nameUUIDHandleCache =
            new MultiKeyDictionary<string, UUID, Cache.Group>();

        public Cache.Group this[string name, UUID UUID]
        {
            get
            {
                Cache.Group group;
                SyncRoot.EnterReadLock();
                nameUUIDHandleCache.TryGetValue(name, UUID, out group);
                SyncRoot.ExitReadLock();
                return group;
            }
        }

        public Cache.Group this[UUID UUID]
        {
            get
            {
                Cache.Group group;
                SyncRoot.EnterReadLock();
                nameCache.TryGetValue(UUID, out group);
                SyncRoot.ExitReadLock();
                return group;
            }
        }

        public Cache.Group this[string name]
        {
            get
            {
                Cache.Group group;
                SyncRoot.EnterReadLock();
                groupCache.TryGetValue(name, out group);
                SyncRoot.ExitReadLock();
                return group;
            }
        }

        public Cache.Group this[Cache.Group group]
        {
            get
            {
                Cache.Group r = this[group.Name];

                if (!r.Equals(default(Cache.Group)))
                    return r;

                if (!group.UUID.Equals(UUID.Zero))
                {
                    r = this[group.UUID];
                    if (!r.Equals(default(Cache.Group)))
                        return r;
                }

                return default(Cache.Group);
            }
        }

        public new void Clear()
        {
            SyncRoot.EnterWriteLock();
            nameCache.Clear();
            nameUUIDHandleCache.Clear();
            base.Clear();
            SyncRoot.ExitWriteLock();
        }

        public new void Add(Cache.Group group)
        {
            SyncRoot.EnterWriteLock();
            if (!nameCache.ContainsKey(group.UUID))
                nameCache.Add(group.UUID, group);
            if (!nameUUIDHandleCache.ContainsKey(group.Name, group.UUID))
                nameUUIDHandleCache.Add(group.Name, group.UUID, group);
            if (!groupCache.ContainsKey(group.Name))
                groupCache.Add(group.Name, group);
            base.Add(group);
            SyncRoot.ExitWriteLock();
        }

        public new bool Remove(Cache.Group group)
        {
            SyncRoot.EnterWriteLock();
            nameCache.Remove(group.UUID);
            nameUUIDHandleCache.Remove(group.Name, group.UUID);
            groupCache.Remove(group.Name);
            var v = base.Remove(group);
            SyncRoot.ExitWriteLock();
            return v;
        }

        public new void UnionWith(IEnumerable<Cache.Group> list)
        {
            SyncRoot.EnterWriteLock();
            foreach (var group in list.Except(AsEnumerable()))
            {
                if (nameCache.ContainsKey(group.UUID))
                    nameCache.Remove(group.UUID);
                nameCache.Add(group.UUID, group);
                if (nameUUIDHandleCache.ContainsKey(group.Name, group.UUID))
                    nameUUIDHandleCache.Remove(group.Name, group.UUID);
                nameUUIDHandleCache.Add(group.Name, group.UUID, group);
                if (groupCache.ContainsKey(group.Name))
                    groupCache.Remove(group.Name);
                groupCache.Add(group.Name, group);
            }

            base.UnionWith(list);
            SyncRoot.ExitWriteLock();
        }

        public bool Contains(string name)
        {
            SyncRoot.EnterReadLock();
            var c = groupCache.ContainsKey(name);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(UUID UUID)
        {
            SyncRoot.EnterReadLock();
            var c = nameCache.ContainsKey(UUID);
            SyncRoot.ExitReadLock();
            return c;
        }
    }
}
