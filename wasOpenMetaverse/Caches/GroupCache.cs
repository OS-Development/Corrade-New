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
    public class GroupCache : ObservableHashSet<Cache.Group>
    {
        public Dictionary<UUID, Cache.Group> nameCache = new Dictionary<UUID, Cache.Group>();
        public Dictionary<string, Cache.Group> groupCache = new Dictionary<string, Cache.Group>();

        public MultiKeyDictionary<string, UUID, Cache.Group> nameUUIDHandleCache =
            new MultiKeyDictionary<string, UUID, Cache.Group>();

        public Cache.Group this[string name, UUID UUID]
        {
            get
            {
                Cache.Group group;
                nameUUIDHandleCache.TryGetValue(name, UUID, out group);
                return group;
            }
        }

        public Cache.Group this[UUID UUID]
        {
            get
            {
                Cache.Group group;
                nameCache.TryGetValue(UUID, out group);
                return group;
            }
        }

        public Cache.Group this[string name]
        {
            get
            {
                Cache.Group group;
                groupCache.TryGetValue(name, out group);
                return group;
            }
        }

        public Cache.Group this[Cache.Group group]
        {
            get
            {
                var r = this[group.Name];
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
            nameCache.Clear();
            nameUUIDHandleCache.Clear();
            base.Clear();
        }

        public new void Add(Cache.Group group)
        {
            if (!nameCache.ContainsKey(group.UUID))
                nameCache.Add(group.UUID, group);
            if (!nameUUIDHandleCache.ContainsKey(group.Name, group.UUID))
                nameUUIDHandleCache.Add(group.Name, group.UUID, group);
            if (!groupCache.ContainsKey(group.Name))
                groupCache.Add(group.Name, group);
            base.Add(group);
        }

        public new bool Remove(Cache.Group group)
        {
            nameCache.Remove(group.UUID);
            nameUUIDHandleCache.Remove(group.Name, group.UUID);
            groupCache.Remove(group.Name);
            return base.Remove(group);
        }

        public new void UnionWith(IEnumerable<Cache.Group> list)
        {
            var enumerable = list as Cache.Group[] ?? list.ToArray();
            enumerable.Except(AsEnumerable()).AsParallel().ForAll(group =>
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
            });

            base.UnionWith(enumerable);
        }

        public bool Contains(string name)
        {
            return groupCache.ContainsKey(name);
        }

        public bool Contains(UUID UUID)
        {
            return nameCache.ContainsKey(UUID);
        }
    }
}
