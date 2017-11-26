///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Linq;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                toggleparcelflags =
                    (corradeCommandParameters, result) =>
                    {
                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                out position))
                            position = Client.Self.SimPosition;
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        Parcel parcel = null;
                        if (
                            !Services.GetParcelAtPosition(Client, simulator, position,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                ref parcel))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);

                        var flags =
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLAGS)),
                                    corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(flags))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_FLAGS_PROVIDED);

                        // Set the flags as they are sent.
                        CSV.ToKeyValue(flags)
                            .GroupBy(o => o.Key)
                            .Select(o => o.FirstOrDefault())
                            .AsParallel()
                            .Select(o =>
                            {
                                ParcelFlags flag;
                                if (!Enum.TryParse(o.Key, out flag))
                                    return null;
                                bool isSet;
                                if (!bool.TryParse(o.Value, out isSet))
                                    return null;

                                return new {Flag = flag, Set = isSet};
                            })
                            .Where(o => o != null)
                            .ForAll(o => o.Set,
                                o => BitTwiddling.SetMaskFlag(ref parcel.Flags, o.Flag),
                                o => BitTwiddling.UnsetMaskFlag(ref parcel.Flags, o.Flag));

                        // Store the initial group.
                        var initialGroup = Client.Self.ActiveGroup;

                        // Activate parcel group.
                        Locks.ClientInstanceGroupsLock.EnterWriteLock();
                        Client.Groups.ActivateGroup(parcel.GroupID);

                        Locks.ClientInstanceParcelsLock.EnterWriteLock();
                        parcel.Update(simulator, true);
                        Locks.ClientInstanceParcelsLock.ExitWriteLock();

                        // Activate the initial group.
                        Client.Groups.ActivateGroup(initialGroup);
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();
                    };
        }
    }
}