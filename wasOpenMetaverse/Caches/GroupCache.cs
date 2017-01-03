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

        public MultiKeyDictionary<string, UUID, Cache.Group> nameHandleCache =
            new MultiKeyDictionary<string, UUID, Cache.Group>();

        public Cache.Group this[string name, UUID UUID]
        {
            get
            {
                Cache.Group group;
                nameHandleCache.TryGetValue(name, UUID, out group);
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
            nameHandleCache.Clear();
            base.Clear();
        }

        public new void Add(Cache.Group group)
        {
            if (!nameCache.ContainsKey(group.UUID))
                nameCache.Add(group.UUID, group);
            if (!nameHandleCache.ContainsKey(group.Name, group.UUID))
                nameHandleCache.Add(group.Name, group.UUID, group);
            base.Add(group);
        }

        public new bool Remove(Cache.Group group)
        {
            nameCache.Remove(group.UUID);
            nameHandleCache.Remove(group.Name, group.UUID);
            return base.Remove(group);
        }

        public new void UnionWith(IEnumerable<Cache.Group> list)
        {
            var enumerable = list as Cache.Group[] ?? list.ToArray();
            enumerable.Except(AsEnumerable()).AsParallel().ForAll(group =>
            {
                nameCache.Remove(group.UUID);
                nameHandleCache.Remove(group.Name, group.UUID);
            });

            base.UnionWith(enumerable);
        }

        public bool Contains(string name)
        {
            return nameHandleCache.ContainsKey(name);
        }

        public bool Contains(UUID UUID)
        {
            return nameCache.ContainsKey(UUID);
        }
    }
}