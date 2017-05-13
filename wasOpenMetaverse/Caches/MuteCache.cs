///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using wasSharp.Collections.Specialized;

namespace wasOpenMetaverse.Caches
{
    public class MuteCache : ObservableHashSet<Cache.MuteEntry>
    {
        private readonly object SyncRoot = new object();

        public Cache.MuteEntry this[Cache.MuteEntry muteEntry]
        {
            get
            {
                lock (SyncRoot)
                {
                    return Contains(muteEntry) ? muteEntry : default(Cache.MuteEntry);
                }
            }
        }
    }
}
