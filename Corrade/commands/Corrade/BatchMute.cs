using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> batchmute =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name, (int) Permissions.Mute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();

                    ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();

                    Parallel.ForEach(wasCSVToEnumerable(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.MUTES)),
                            message)))
                        .AsParallel()
                        .Select((o, p) => new {o, p})
                        .GroupBy(q => q.p/2, q => q.o)
                        .Select(o => o.ToList())
                        .TakeWhile(o => o.Count%2 == 0)
                        .Where(o => !string.IsNullOrEmpty(o.First()) || !string.IsNullOrEmpty(o.Last()))
                        .ToDictionary(o => o.First(), p => p.Last()), o =>
                        {
                            IEnumerable<MuteEntry> mutes = Enumerable.Empty<MuteEntry>();
                            UUID targetUUID;
                            bool succeeded;
                            switch (
                                wasGetEnumValueFromDescription<Action>(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                            message)).ToLowerInvariant()))
                            {
                                case Action.MUTE:

                                    // retrieve the current mute list
                                    if (!UUID.TryParse(o.Value, out targetUUID) ||
                                        !GetMutes(corradeConfiguration.ServicesTimeout, ref mutes))
                                    {
                                        lock (LockObject)
                                        {
                                            data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // check that the mute list does not already exist
                                    if (
                                        mutes.ToList()
                                            .AsParallel()
                                            .Any(p => p.ID.Equals(targetUUID) && p.Name.Equals(o.Key)))
                                    {
                                        lock (LockObject)
                                        {
                                            data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // Get the mute type
                                    FieldInfo muteTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                                         BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            p =>
                                                p.Name.Equals(
                                                    wasInput(
                                                        wasKeyValueGet(
                                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                                            message)),
                                                    StringComparison.Ordinal));
                                    // ...or assume "Default" mute type from MuteType
                                    MuteType muteType = muteTypeInfo != null
                                        ? (MuteType)
                                            muteTypeInfo
                                                .GetValue(null)
                                        : MuteType.ByName;
                                    // Get the mute flags - default is "Default" equivalent to 0
                                    int muteFlags = 0;
                                    Parallel.ForEach(wasCSVToEnumerable(
                                        wasInput(
                                            wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FLAGS)),
                                                message))).AsParallel().Where(p => !string.IsNullOrEmpty(p)),
                                        p =>
                                            Parallel.ForEach(
                                                typeof (MuteFlags).GetFields(BindingFlags.Public |
                                                                             BindingFlags.Static)
                                                    .AsParallel().Where(q => q.Name.Equals(p, StringComparison.Ordinal)),
                                                r => { muteFlags |= ((int) r.GetValue(null)); }));
                                    succeeded = true;
                                    lock (ClientInstanceSelfLock)
                                    {
                                        Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                        MuteListUpdatedEvent.Reset();
                                        Client.Self.UpdateMuteListEntry(muteType, targetUUID, o.Key,
                                            (MuteFlags) muteFlags);
                                        if (
                                            !MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                false))
                                        {
                                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                            succeeded = false;
                                        }
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    }
                                    switch (succeeded)
                                    {
                                        case true:
                                            // add the mute to the cache
                                            lock (Cache.Locks.MutesCacheLock)
                                            {
                                                Cache.MutesCache.Add(new MuteEntry
                                                {
                                                    Flags = (MuteFlags) muteFlags,
                                                    ID = targetUUID,
                                                    Name = o.Key,
                                                    Type = muteType
                                                });
                                            }
                                            break;
                                        case false:
                                            lock (LockObject)
                                            {
                                                // they could not be muted so add the name to the output
                                                data.Add(o.Key);
                                            }
                                            break;
                                    }
                                    break;
                                case Action.UNMUTE:
                                    UUID.TryParse(o.Value, out targetUUID);

                                    if (targetUUID.Equals(UUID.Zero) ||
                                        !GetMutes(corradeConfiguration.ServicesTimeout, ref mutes))
                                    {
                                        lock (LockObject)
                                        {
                                            data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // find the mute either by name or by target
                                    MuteEntry mute =
                                        mutes.ToList().AsParallel()
                                            .FirstOrDefault(
                                                p =>
                                                    p.Name.Equals(o.Key) ||
                                                    (!targetUUID.Equals(UUID.Zero) && p.ID.Equals(targetUUID)));

                                    if (mute == null || mute.Equals(default(MuteEntry)))
                                    {
                                        lock (LockObject)
                                        {
                                            data.Add(o.Key);
                                        }
                                        return;
                                    }
                                    succeeded = true;
                                    lock (ClientInstanceSelfLock)
                                    {
                                        // remove the mute list
                                        Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                        MuteListUpdatedEvent.Reset();
                                        Client.Self.RemoveMuteListEntry(mute.ID, mute.Name);
                                        if (
                                            !MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                false))
                                        {
                                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                            succeeded = false;
                                        }
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    }
                                    switch (succeeded)
                                    {
                                        case true:
                                            // remove the mute from the cache
                                            lock (Cache.Locks.MutesCacheLock)
                                            {
                                                Cache.MutesCache.Remove(mute);
                                            }
                                            break;
                                        case false:
                                            lock (LockObject)
                                            {
                                                data.Add(o.Key);
                                            }
                                            break;
                                    }
                                    break;
                            }
                        });
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}