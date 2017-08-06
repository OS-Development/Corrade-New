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
    public class RegionCache : ObservableHashSet<Cache.Region>
    {
        private readonly ReaderWriterLockSlim SyncRoot = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly Dictionary<ulong, Cache.Region> handleCache = new Dictionary<ulong, Cache.Region>();
        private readonly Dictionary<string, Cache.Region> nameCache = new Dictionary<string, Cache.Region>();

        private readonly MultiKeyDictionary<string, ulong, Cache.Region> nameHandleCache =
            new MultiKeyDictionary<string, ulong, Cache.Region>();

        private readonly Dictionary<UUID, Cache.Region> UUIDCache = new Dictionary<UUID, Cache.Region>();

        private readonly MultiKeyDictionary<UUID, ulong, Cache.Region> UUIDHandleCache =
            new MultiKeyDictionary<UUID, ulong, Cache.Region>();

        public Cache.Region this[string name]
        {
            get
            {
                Cache.Region region;
                SyncRoot.EnterReadLock();
                nameCache.TryGetValue(name, out region);
                SyncRoot.ExitReadLock();
                return region;
            }
        }

        public Cache.Region this[ulong handle]
        {
            get
            {
                Cache.Region region;
                SyncRoot.EnterReadLock();
                handleCache.TryGetValue(handle, out region);
                SyncRoot.ExitReadLock();
                return region;
            }
        }

        public Cache.Region this[UUID UUID]
        {
            get
            {
                Cache.Region region;
                SyncRoot.EnterReadLock();
                UUIDCache.TryGetValue(UUID, out region);
                SyncRoot.ExitReadLock();
                return region;
            }
        }

        public Cache.Region this[string name, ulong handle]
        {
            get
            {
                Cache.Region region;
                SyncRoot.EnterReadLock();
                nameHandleCache.TryGetValue(name, handle, out region);
                SyncRoot.ExitReadLock();
                return region;
            }
        }

        public Cache.Region this[UUID UUID, ulong handle]
        {
            get
            {
                Cache.Region region;
                SyncRoot.EnterReadLock();
                UUIDHandleCache.TryGetValue(UUID, handle, out region);
                SyncRoot.ExitReadLock();
                return region;
            }
        }

        public Cache.Region this[Cache.Region region]
        {
            get
            {
                Cache.Region r = this[region.Name];

                if (!r.Equals(default(Cache.Region)))
                    return r;

                r = this[region.Handle];
                if (!r.Equals(default(Cache.Region)))
                    return r;

                if (!region.UUID.Equals(UUID.Zero))
                {
                    r = this[region.UUID];

                    if (!r.Equals(default(Cache.Region)))
                        return r;
                }

                return default(Cache.Region);
            }
        }

        public new void Clear()
        {
            SyncRoot.EnterWriteLock();
            nameCache.Clear();
            handleCache.Clear();
            UUIDCache.Clear();
            nameHandleCache.Clear();
            UUIDHandleCache.Clear();
            base.Clear();
            SyncRoot.ExitWriteLock();
        }

        public new void Add(Cache.Region region)
        {
            SyncRoot.EnterWriteLock();
            if (!nameCache.ContainsKey(region.Name))
                nameCache.Add(region.Name, region);
            if (!handleCache.ContainsKey(region.Handle))
                handleCache.Add(region.Handle, region);
            if (!UUIDCache.ContainsKey(region.UUID))
                UUIDCache.Add(region.UUID, region);
            if (!nameHandleCache.ContainsKey(region.Name, region.Handle))
                nameHandleCache.Add(region.Name, region.Handle, region);
            if (!UUIDHandleCache.ContainsKey(region.UUID, region.Handle))
                UUIDHandleCache.Add(region.UUID, region.Handle, region);
            base.Add(region);
            SyncRoot.ExitWriteLock();
        }

        public new bool Remove(Cache.Region region)
        {
            SyncRoot.EnterWriteLock();
            nameCache.Remove(region.Name);
            handleCache.Remove(region.Handle);
            UUIDCache.Remove(region.UUID);
            nameHandleCache.Remove(region.Name);
            UUIDHandleCache.Remove(region.UUID);
            var v = base.Remove(region);
            SyncRoot.ExitWriteLock();
            return v;
        }

        public new void UnionWith(IEnumerable<Cache.Region> list)
        {
            SyncRoot.EnterWriteLock();
            var enumerable = list as IList<Cache.Region> ?? list.ToList();
            foreach (var region in enumerable.Except(AsEnumerable()))
            {
                if (nameCache.ContainsKey(region.Name))
                    nameCache.Remove(region.Name);
                nameCache.Add(region.Name, region);

                if (handleCache.ContainsKey(region.Handle))
                    handleCache.Remove(region.Handle);
                handleCache.Add(region.Handle, region);

                if (UUIDCache.ContainsKey(region.UUID))
                    UUIDCache.Remove(region.UUID);
                UUIDCache.Add(region.UUID, region);

                if (nameHandleCache.ContainsKey(region.Name, region.Handle))
                    nameHandleCache.Remove(region.Name, region.Handle);
                nameHandleCache.Add(region.Name, region.Handle, region);

                if (UUIDHandleCache.ContainsKey(region.UUID, region.Handle))
                    UUIDHandleCache.Remove(region.UUID, region.Handle);
                UUIDHandleCache.Add(region.UUID, region.Handle, region);
            }

            base.UnionWith(enumerable);
            SyncRoot.ExitWriteLock();
        }

        public bool Contains(string name)
        {
            SyncRoot.EnterReadLock();
            var c = nameCache.ContainsKey(name);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(ulong handle)
        {
            SyncRoot.EnterReadLock();
            var c = handleCache.ContainsKey(handle);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(UUID UUID)
        {
            SyncRoot.EnterReadLock();
            var c = UUIDCache.ContainsKey(UUID);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(string name, ulong handle)
        {
            SyncRoot.EnterReadLock();
            var c = nameHandleCache.ContainsKey(name, handle);
            SyncRoot.ExitReadLock();
            return c;
        }

        public bool Contains(UUID UUID, ulong handle)
        {
            SyncRoot.EnterReadLock();
            var c = UUIDHandleCache.ContainsKey(UUID, handle);
            SyncRoot.ExitReadLock();
            return c;
        }
    }
}
