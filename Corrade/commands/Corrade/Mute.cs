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
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> mute =
                (corradeCommandParameters, result) =>
                {
                    /* Muting and unmuting is masked by the Corrade cache since, although the entries are created,
                     * respectively removed, the change is indeed propagated to the grid but it takes an unuseful
                     * amount of time for the grid to return them when asked to return them.
                     */
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

                    // Get the mute type
                    var muteTypeInfo = typeof(MuteType).GetFields(BindingFlags.Public |
                                                                  BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));

                    if (muteTypeInfo == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_MUTE_TYPE);

                    var muteType = (MuteType) muteTypeInfo.GetValue(null);

                    // Get the UUID and name to mute from the mute type.
                    UUID targetUUID;
                    var name = string.Empty;
                    switch (muteType)
                    {
                        case MuteType.Resident:
                            if (
                                !UUID.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                        corradeCommandParameters.Message)),
                                    out targetUUID) && !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            if (!Resolvers.AgentUUIDToName(Client, targetUUID,
                                corradeConfiguration.ServicesTimeout, ref name))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            break;

                        case MuteType.Group:
                            var target = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);

                            if (!UUID.TryParse(target, out targetUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType), ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            if (!Resolvers.GroupUUIDToName(Client, targetUUID,
                                corradeConfiguration.ServicesTimeout, ref name))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;

                        case MuteType.ByName:
                            name =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            targetUUID = UUID.Zero;

                            break;

                        case MuteType.Object:
                        case MuteType.External:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    out targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_MUTE_TARGET);
                            name =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));

                            if (string.IsNullOrEmpty(name))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_MUTE_TYPE);
                    }

                    var MuteListUpdatedEvent = new ManualResetEventSlim(false);
                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                        (sender, args) => MuteListUpdatedEvent.Set();

                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.MUTE:

                            // check that the mute entry does not already exist
                            if (mutes.ToList().AsParallel().Any(o => o.ID.Equals(targetUUID) && o.Name.Equals(name)))
                                throw new Command.ScriptException(Enumerations.ScriptError.MUTE_ENTRY_ALREADY_EXISTS);

                            // Get the mute flags - default is "Default" equivalent to 0
                            var muteFlags = MuteFlags.Default;
                            CSV.ToEnumerable(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLAGS)),
                                            corradeCommandParameters.Message)))
                                .AsParallel()
                                .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                    typeof(MuteFlags).GetFields(BindingFlags.Public |
                                                                BindingFlags.Static)
                                        .AsParallel()
                                        .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                        .ForAll(
                                            q =>
                                            {
                                                BitTwiddling.SetMaskFlag(ref muteFlags, (MuteFlags) q.GetValue(null));
                                            }));

                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            // add mute
                            Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                            Client.Self.UpdateMuteListEntry(muteType, targetUUID, name, muteFlags);
                            if (!MuteListUpdatedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                            }
                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            // add the mute to the cache
                            Cache.AddMute(muteFlags, targetUUID, name, muteType);
                            break;

                        case Enumerations.Action.UNMUTE:

                            // find the mute either by name or by target
                            var mute =
                                mutes.ToList().AsParallel()
                                    .FirstOrDefault(
                                        o =>
                                            !string.IsNullOrEmpty(name) && o.Name.Equals(name) ||
                                            !targetUUID.Equals(UUID.Zero) && o.ID.Equals(targetUUID));

                            if (mute == null || mute.Equals(default(MuteEntry)))
                                throw new Command.ScriptException(Enumerations.ScriptError.MUTE_ENTRY_NOT_FOUND);

                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            // remove the mute
                            Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                            Client.Self.RemoveMuteListEntry(mute.ID, mute.Name);
                            if (!MuteListUpdatedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                            }
                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            // remove the mute from the cache
                            Cache.RemoveMute(mute.Flags, mute.ID, mute.Name, mute.Type);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}