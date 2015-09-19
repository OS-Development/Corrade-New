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
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

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
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Mute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();
                    UUID targetUUID;
                    string name;
                    IEnumerable<MuteEntry> mutes = Enumerable.Empty<MuteEntry>();
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.MUTE:
                            // we need an UUID and a name to create a mute
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    out targetUUID))
                            {
                                throw new ScriptException(ScriptError.INVALID_MUTE_TARGET);
                            }

                            name =
                                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name))
                                throw new ScriptException(ScriptError.NO_NAME_PROVIDED);

                            // retrieve the current mute list
                            GetMutes(corradeConfiguration.ServicesTimeout, ref mutes);

                            // check that the mute list does not already exist
                            if (mutes.ToList().AsParallel().Any(o => o.ID.Equals(targetUUID) && o.Name.Equals(name)))
                                throw new ScriptException(ScriptError.MUTE_ENTRY_ALREADY_EXISTS);

                            // Get the mute type
                            FieldInfo muteTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
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
                            Parallel.ForEach(wasCSVToEnumerable(
                                wasInput(
                                    wasKeyValueGet(
                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FLAGS)),
                                        corradeCommandParameters.Message)))
                                .AsParallel()
                                .Where(o => !string.IsNullOrEmpty(o)),
                                o =>
                                    Parallel.ForEach(
                                        typeof (MuteFlags).GetFields(BindingFlags.Public |
                                                                     BindingFlags.Static)
                                            .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                        q => { muteFlags |= ((int) q.GetValue(null)); }));
                            lock (ClientInstanceSelfLock)
                            {
                                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                Client.Self.UpdateMuteListEntry(muteType, targetUUID, name, (MuteFlags) muteFlags);
                                if (!MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                                }
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                            }
                            // add the mute to the cache
                            Cache.MutesCache.Add(new MuteEntry
                            {
                                Flags = (MuteFlags) muteFlags,
                                ID = targetUUID,
                                Name = name,
                                Type = muteType
                            });

                            break;
                        case Action.UNMUTE:
                            UUID.TryParse(
                                wasInput(wasKeyValueGet(
                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message)),
                                out targetUUID);
                            name = wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name) && targetUUID.Equals(UUID.Zero))
                                throw new ScriptException(ScriptError.NO_NAME_OR_UUID_PROVIDED);

                            // retrieve the current mute list
                            GetMutes(corradeConfiguration.ServicesTimeout, ref mutes);

                            // find the mute either by name or by target
                            MuteEntry mute =
                                mutes.ToList().AsParallel()
                                    .FirstOrDefault(
                                        o =>
                                            (!string.IsNullOrEmpty(name) && o.Name.Equals(name)) ||
                                            (!targetUUID.Equals(UUID.Zero) && o.ID.Equals(targetUUID)));

                            if (mute == null || mute.Equals(default(MuteEntry)))
                                throw new ScriptException(ScriptError.MUTE_ENTRY_NOT_FOUND);

                            lock (ClientInstanceSelfLock)
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
                            Cache.MutesCache.Remove(mute);

                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}