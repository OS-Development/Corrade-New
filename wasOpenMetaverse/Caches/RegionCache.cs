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
    public class RegionCache : ObservableHashSet<Cache.Region>
    {
        private readonly object SyncRoot = new object();

        public Dictionary<ulong, Cache.Region> handleCache = new Dictionary<ulong, Cache.Region>();
        public Dictionary<string, Cache.Region> nameCache = new Dictionary<string, Cache.Region>();

        public MultiKeyDictionary<string, ulong, Cache.Region> nameHandleCache =
            new MultiKeyDictionary<string, ulong, Cache.Region>();

        public Dictionary<UUID, Cache.Region> UUIDCache = new Dictionary<UUID, Cache.Region>();

        public MultiKeyDictionary<UUID, ulong, Cache.Region> UUIDHandleCache =
            new MultiKeyDictionary<UUID, ulong, Cache.Region>();

        public Cache.Region this[string name]
        {
            get
            {
                Cache.Region region;
                lock (SyncRoot)
                {
                    nameCache.TryGetValue(name, out region);
                }
                return region;
            }
        }

        public Cache.Region this[ulong handle]
        {
            get
            {
                Cache.Region region;
                lock (SyncRoot)
                {
                    handleCache.TryGetValue(handle, out region);
                }
                return region;
            }
        }

        public Cache.Region this[UUID UUID]
        {
            get
            {
                Cache.Region region;
                lock (SyncRoot)
                {
                    UUIDCache.TryGetValue(UUID, out region);
                }
                return region;
            }
        }

        public Cache.Region this[string name, ulong handle]
        {
            get
            {
                Cache.Region region;
                lock (SyncRoot)
                {
                    nameHandleCache.TryGetValue(name, handle, out region);
                }
                return region;
            }
        }

        public Cache.Region this[UUID UUID, ulong handle]
        {
            get
            {
                Cache.Region region;
                lock (SyncRoot)
                {
                    UUIDHandleCache.TryGetValue(UUID, handle, out region);
                }
                return region;
            }
        }

        public Cache.Region this[Cache.Region region]
        {
            get
            {
                Cache.Region r;
                lock (SyncRoot)
                {
                    r = this[region.Name];
                }
                if (!r.Equals(default(Cache.Region)))
                    return r;

                r = this[region.Handle];
                if (!r.Equals(default(Cache.Region)))
                    return r;

                if (!region.UUID.Equals(UUID.Zero))
                {
                    lock (SyncRoot)
                    {
                        r = this[region.UUID];
                    }
                    if (!r.Equals(default(Cache.Region)))
                        return r;
                }

                return default(Cache.Region);
            }
        }

        public new void Clear()
        {
            lock (SyncRoot)
            {
                nameCache.Clear();
                handleCache.Clear();
                UUIDCache.Clear();
                nameHandleCache.Clear();
                UUIDHandleCache.Clear();
                base.Clear();
            }
        }

        public new void Add(Cache.Region region)
        {
            lock (SyncRoot)
            {
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
            }
        }

        public new bool Remove(Cache.Region region)
        {
            lock (SyncRoot)
            {
                nameCache.Remove(region.Name);
                handleCache.Remove(region.Handle);
                UUIDCache.Remove(region.UUID);
                nameHandleCache.Remove(region.Name);
                UUIDHandleCache.Remove(region.UUID);
                return base.Remove(region);
            }
        }

        public new void UnionWith(IEnumerable<Cache.Region> list)
        {
            lock (SyncRoot)
            {
                var lazyList = new ConcurrentLazyList<Cache.Region>(list);
                lazyList.Except(AsEnumerable()).AsParallel().ForAll(region =>
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
                });

                base.UnionWith(lazyList);
            }
        }

        public bool Contains(string name)
        {
            lock (SyncRoot)
            {
                return nameCache.ContainsKey(name);
            }
        }

        public bool Contains(ulong handle)
        {
            lock (SyncRoot)
            {
                return handleCache.ContainsKey(handle);
            }
        }

        public bool Contains(UUID UUID)
        {
            lock (SyncRoot)
            {
                return UUIDCache.ContainsKey(UUID);
            }
        }

        public bool Contains(string name, ulong handle)
        {
            lock (SyncRoot)
            {
                return nameHandleCache.ContainsKey(name, handle);
            }
        }

        public bool Contains(UUID UUID, ulong handle)
        {
            lock (SyncRoot)
            {
                return UUIDHandleCache.ContainsKey(UUID, handle);
            }
        }
    }
}
