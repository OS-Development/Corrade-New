///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> batchmute =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Mute))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var mutes = Enumerable.Empty<MuteEntry>();
                    // retrieve the current mute list
                    switch (Cache.MuteCache.IsVirgin)
                    {
                        case true:
                            if (!Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .COULD_NOT_RETRIEVE_MUTE_LIST);
                            break;

                        default:
                            mutes = Cache.MuteCache.OfType<MuteEntry>();
                            break;
                    }

                    var data = new HashSet<string>();
                    var LockObject = new object();
                    var MuteListUpdatedEvent = new ManualResetEventSlim(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();

                    CSV.ToKeyValue(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MUTES)),
                                corradeCommandParameters.Message)))
                        .AsParallel()
                        .GroupBy(o => o.Key)
                        .Select(o => o.FirstOrDefault())
                        .ToDictionary(o => wasInput(o.Key), o => wasInput(o.Value))
                        .AsParallel()
                        .ForAll(o =>
                        {
                            UUID targetUUID;
                            bool succeeded;
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Action>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                            corradeCommandParameters.Message))))
                            {
                                case Enumerations.Action.MUTE:

                                    if (!UUID.TryParse(o.Value, out targetUUID) ||
                                        !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o.Key))
                                                data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // check that the mute list does not already exist
                                    if (
                                        mutes
                                            .AsParallel()
                                            .Any(p => p.ID.Equals(targetUUID) && p.Name.Equals(o.Key)))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o.Key))
                                                data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // Get the mute type
                                    var muteTypeInfo = typeof(MuteType).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            p =>
                                                p.Name.Equals(
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(
                                                                    Command.ScriptKeys.TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    // ...or assume "Default" mute type from MuteType
                                    var muteType = muteTypeInfo != null
                                        ? (MuteType)
                                        muteTypeInfo
                                            .GetValue(null)
                                        : MuteType.ByName;
                                    // Get the mute flags - default is "Default" equivalent to 0
                                    var muteFlags = MuteFlags.Default;
                                    CSV.ToEnumerable(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                        .FLAGS)),
                                                    corradeCommandParameters.Message)))
                                        .AsParallel()
                                        .Where(p => !string.IsNullOrEmpty(p)).ForAll(p =>
                                            typeof(MuteFlags).GetFields(BindingFlags.Public |
                                                                        BindingFlags.Static)
                                                .AsParallel()
                                                .Where(
                                                    q => string.Equals(p, q.Name, StringComparison.Ordinal))
                                                .ForAll(
                                                    r =>
                                                    {
                                                        BitTwiddling.SetMaskFlag(ref muteFlags,
                                                            (MuteFlags) r.GetValue(null));
                                                    }));
                                    succeeded = true;
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                    MuteListUpdatedEvent.Reset();
                                    Client.Self.UpdateMuteListEntry(muteType, targetUUID, o.Key,
                                        muteFlags);
                                    if (
                                        !MuteListUpdatedEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                        succeeded = false;
                                    }
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    switch (succeeded)
                                    {
                                        case true:
                                            Cache.AddMute(muteFlags, targetUUID, o.Key, muteType);
                                            break;

                                        case false:
                                            lock (LockObject)
                                            {
                                                // they could not be muted so add the name to the output
                                                if (!data.Contains(o.Key))
                                                    data.Add(o.Key);
                                            }
                                            break;
                                    }
                                    break;

                                case Enumerations.Action.UNMUTE:
                                    UUID.TryParse(o.Value, out targetUUID);

                                    if (targetUUID.Equals(UUID.Zero) ||
                                        !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o.Key))
                                                data.Add(o.Key);
                                        }
                                        return;
                                    }

                                    // find the mute either by name or by target
                                    var mute =
                                        mutes.AsParallel()
                                            .FirstOrDefault(
                                                p =>
                                                    p.Name.Equals(o.Key) ||
                                                    !targetUUID.Equals(UUID.Zero) && p.ID.Equals(targetUUID));

                                    if (mute == null || mute.Equals(default(MuteEntry)))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o.Key))
                                                data.Add(o.Key);
                                        }
                                        return;
                                    }
                                    succeeded = true;
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    // remove the mute
                                    Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                    MuteListUpdatedEvent.Reset();
                                    Client.Self.RemoveMuteListEntry(mute.ID, mute.Name);
                                    if (
                                        !MuteListUpdatedEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                        succeeded = false;
                                    }
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    switch (succeeded)
                                    {
                                        case true:
                                            // remove the mute from the cache
                                            Cache.RemoveMute(mute.Flags, mute.ID, mute.Name, mute.Type);
                                            break;

                                        case false:
                                            lock (LockObject)
                                            {
                                                if (!data.Contains(o.Key))
                                                    data.Add(o.Key);
                                            }
                                            break;
                                    }
                                    break;
                            }
                        });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}