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
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> batchmute =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Mute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();

                    ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();

                    CSV.ToKeyValue(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MUTES)),
                            corradeCommandParameters.Message))).ToArray().AsParallel().ForAll(o =>
                            {
                                IEnumerable<MuteEntry> mutes = Enumerable.Empty<MuteEntry>();
                                UUID targetUUID;
                                bool succeeded;
                                switch (
                                    Reflection.GetEnumValueFromName<Action>(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                                corradeCommandParameters.Message)).ToLowerInvariant()))
                                {
                                    case Action.MUTE:

                                        // retrieve the current mute list
                                        if (!UUID.TryParse(o.Value, out targetUUID) ||
                                            !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                        {
                                            lock (LockObject)
                                            {
                                                data.Add(o.Key);
                                            }
                                            return;
                                        }

                                        // check that the mute list does not already exist
                                        if (
                                            mutes.ToArray()
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
                                                            KeyValue.Get(
                                                                wasOutput(
                                                                    Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                                                corradeCommandParameters.Message)),
                                                        StringComparison.Ordinal));
                                        // ...or assume "Default" mute type from MuteType
                                        MuteType muteType = muteTypeInfo != null
                                            ? (MuteType)
                                                muteTypeInfo
                                                    .GetValue(null)
                                            : MuteType.ByName;
                                        // Get the mute flags - default is "Default" equivalent to 0
                                        int muteFlags = 0;
                                        CSV.ToEnumerable(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FLAGS)),
                                                    corradeCommandParameters.Message)))
                                            .ToArray()
                                            .AsParallel()
                                            .Where(p => !string.IsNullOrEmpty(p)).ForAll(p =>
                                                typeof (MuteFlags).GetFields(BindingFlags.Public |
                                                                             BindingFlags.Static)
                                                    .AsParallel()
                                                    .Where(q => string.Equals(p, q.Name, StringComparison.Ordinal))
                                                    .ForAll(
                                                        r => { muteFlags |= ((int) r.GetValue(null)); }));
                                        succeeded = true;
                                        lock (Locks.ClientInstanceSelfLock)
                                        {
                                            Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                            MuteListUpdatedEvent.Reset();
                                            Client.Self.UpdateMuteListEntry(muteType, targetUUID, o.Key,
                                                (MuteFlags) muteFlags);
                                            if (
                                                !MuteListUpdatedEvent.WaitOne(
                                                    (int) corradeConfiguration.ServicesTimeout,
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
                                                Cache.AddMute((MuteFlags) muteFlags, targetUUID, o.Key, muteType);
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
                                            !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                        {
                                            lock (LockObject)
                                            {
                                                data.Add(o.Key);
                                            }
                                            return;
                                        }

                                        // find the mute either by name or by target
                                        MuteEntry mute =
                                            mutes.ToArray().AsParallel()
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
                                        lock (Locks.ClientInstanceSelfLock)
                                        {
                                            // remove the mute list
                                            Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                            MuteListUpdatedEvent.Reset();
                                            Client.Self.RemoveMuteListEntry(mute.ID, mute.Name);
                                            if (
                                                !MuteListUpdatedEvent.WaitOne(
                                                    (int) corradeConfiguration.ServicesTimeout,
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
                                                Cache.RemoveMute(mute);
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
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}