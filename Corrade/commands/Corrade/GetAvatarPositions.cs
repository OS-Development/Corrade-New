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
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getavatarpositions =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
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
                        var entity = Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))
                        );
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        Parcel parcel = null;
                        switch (entity)
                        {
                            case Enumerations.Entity.REGION:
                                break;

                            case Enumerations.Entity.PARCEL:
                                if (
                                    !Services.GetParcelAtPosition(Client, simulator, position,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        ref parcel))
                                    throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                        }
                        var csv = new List<string>();
                        var LockObject = new object();
                        simulator.AvatarPositions.Copy().AsParallel().ForAll(p =>
                        {
                            var name = string.Empty;
                            if (
                                !Resolvers.AgentUUIDToName(Client, p.Key, corradeConfiguration.ServicesTimeout,
                                    ref name))
                                return;
                            switch (entity)
                            {
                                case Enumerations.Entity.REGION:
                                    break;

                                case Enumerations.Entity.PARCEL:
                                    Parcel avatarParcel = null;
                                    if (
                                        !Services.GetParcelAtPosition(Client, simulator, p.Value,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                            ref avatarParcel))
                                        return;
                                    if (!avatarParcel.LocalID.Equals(parcel.LocalID)) return;
                                    break;
                            }
                            lock (LockObject)
                            {
                                csv.Add(name);
                                csv.Add(p.Key.ToString());
                                csv.Add(p.Value.ToString());
                            }
                        });
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}