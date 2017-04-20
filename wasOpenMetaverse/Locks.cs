///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Threading;

namespace wasOpenMetaverse
{
    public static class Locks
    {
        public static readonly ReaderWriterLockSlim ClientInstanceGroupsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceInventoryLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceAvatarsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceSelfLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceConfigurationLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceParcelsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceNetworkLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceGridLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceDirectoryLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceEstateLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceObjectsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceFriendsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceAssetsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceAppearanceLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public static readonly ReaderWriterLockSlim ClientInstanceSoundLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    }
}
