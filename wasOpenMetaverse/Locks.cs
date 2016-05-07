///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

namespace wasOpenMetaverse
{
    public static class Locks
    {
        public static readonly object ClientInstanceGroupsLock = new object();
        public static readonly object ClientInstanceInventoryLock = new object();
        public static readonly object ClientInstanceAvatarsLock = new object();
        public static readonly object ClientInstanceSelfLock = new object();
        public static readonly object ClientInstanceConfigurationLock = new object();
        public static readonly object ClientInstanceParcelsLock = new object();
        public static readonly object ClientInstanceNetworkLock = new object();
        public static readonly object ClientInstanceGridLock = new object();
        public static readonly object ClientInstanceDirectoryLock = new object();
        public static readonly object ClientInstanceEstateLock = new object();
        public static readonly object ClientInstanceObjectsLock = new object();
        public static readonly object ClientInstanceFriendsLock = new object();
        public static readonly object ClientInstanceAssetsLock = new object();
        public static readonly object ClientInstanceAppearanceLock = new object();
        public static readonly object ClientInstanceSoundLock = new object();
    }
}