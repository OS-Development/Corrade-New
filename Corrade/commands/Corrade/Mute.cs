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
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> mute =
                (corradeCommandParameters, result) =>
                {
                    /* Muting and unmuting is masked by the Corrade cache since, although the entries are created,
                     * respectively removed, the change is indeed propagated to the grid but it takes an unuseful
                     * amount of time for the grid to return them when asked to return them.
                     */
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Mute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var MuteListUpdatedEvent = new ManualResetEvent(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();
                    UUID targetUUID;
                    string name;
                    var mutes = Enumerable.Empty<MuteEntry>();
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.MUTE:
                            // we need an UUID and a name to create a mute
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    out targetUUID))
                            {
                                throw new ScriptException(ScriptError.INVALID_MUTE_TARGET);
                            }

                            name =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name))
                                throw new ScriptException(ScriptError.NO_NAME_PROVIDED);

                            // retrieve the current mute list
                            Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes);

                            // check that the mute entry does not already exist
                            if (mutes.ToList().AsParallel().Any(o => o.ID.Equals(targetUUID) && o.Name.Equals(name)))
                                throw new ScriptException(ScriptError.MUTE_ENTRY_ALREADY_EXISTS);

                            // Get the mute type
                            var muteTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                           BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
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
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FLAGS)),
                                        corradeCommandParameters.Message)))
                                .ToArray()
                                .AsParallel()
                                .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                    typeof (MuteFlags).GetFields(BindingFlags.Public |
                                                                 BindingFlags.Static)
                                        .AsParallel()
                                        .Where(p => Strings.Equals(o, p.Name, StringComparison.Ordinal))
                                        .ForAll(
                                            q =>
                                            {
                                                BitTwiddling.SetMaskFlag(ref muteFlags, (MuteFlags) q.GetValue(null));
                                            }));
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                Client.Self.UpdateMuteListEntry(muteType, targetUUID, name, muteFlags);
                                if (!MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                                }
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                            }
                            // add the mute to the cache
                            Cache.AddMute(muteFlags, targetUUID, name, muteType);
                            break;
                        case Action.UNMUTE:
                            UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message)),
                                out targetUUID);
                            name = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name) && targetUUID.Equals(UUID.Zero))
                                throw new ScriptException(ScriptError.NO_NAME_OR_UUID_PROVIDED);

                            // retrieve the current mute list
                            Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes);

                            // find the mute either by name or by target
                            var mute =
                                mutes.ToList().AsParallel()
                                    .FirstOrDefault(
                                        o =>
                                            (!string.IsNullOrEmpty(name) && o.Name.Equals(name)) ||
                                            (!targetUUID.Equals(UUID.Zero) && o.ID.Equals(targetUUID)));

                            if (mute == null || mute.Equals(default(MuteEntry)))
                                throw new ScriptException(ScriptError.MUTE_ENTRY_NOT_FOUND);

                            lock (Locks.ClientInstanceSelfLock)
                            {
                                // remove the mute list
                                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                Client.Self.RemoveMuteListEntry(mute.ID, mute.Name);
                                if (!MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                                }
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                            }
                            // remove the mute from the cache
                            Cache.RemoveMute(mute.Flags, mute.ID, mute.Name, mute.Type);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}