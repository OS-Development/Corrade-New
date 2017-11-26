///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> sensor =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var name = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                        corradeCommandParameters.Message));
                    UUID item;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)),
                            out item))
                        item = UUID.Zero;
                    var type = ScriptSensorTypeFlags.Agent;
                    CSV.ToEnumerable(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o =>
                                typeof(ScriptSensorTypeFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                    .ForAll(
                                        q =>
                                        {
                                            BitTwiddling.SetMaskFlag(ref type,
                                                (ScriptSensorTypeFlags) q.GetValue(null));
                                        }));
                    float range = 0;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    float arc = 0;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ARC)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        arc = Utils.PI;
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
                    var csv = new List<string>();
                    var request = UUID.Random();
                    var ScriptSensorReplyEvent = new ManualResetEventSlim(false);
                    EventHandler<ScriptSensorReplyEventArgs> ScriptSensorReplyDelegate = (sender, args) =>
                    {
                        if (!args.RequestorID.Equals(request)) return;

                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.GROUP), args.GroupID.ToString()});
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME), args.Name});
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM), args.ObjectID.ToString()});
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.OWNER), args.OwnerID.ToString()});
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION), args.Position.ToString()});
                        csv.AddRange(new[]
                        {
                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE),
                            args.Range.ToString(Utils.EnUsCulture)
                        });
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROTATION), args.Rotation.ToString()});
                        csv.AddRange(new[]
                            {Reflection.GetNameFromEnumValue(Command.ScriptKeys.VELOCITY), args.Velocity.ToString()});
                        csv.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE));
                        csv.AddRange(typeof(ScriptSensorTypeFlags).GetFields(BindingFlags.Public |
                                                                             BindingFlags.Static)
                            .AsParallel().Where(
                                p => args.Type.IsMaskFlagSet((ScriptSensorTypeFlags) p.GetValue(null)))
                            .Select(p => p.Name));

                        ScriptSensorReplyEvent.Set();
                    };
                    Client.Self.ScriptSensorReply += ScriptSensorReplyDelegate;
                    Client.Self.RequestScriptSensor(name, item, type, range, arc, request, simulator);
                    if (!ScriptSensorReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Self.ScriptSensorReply -= ScriptSensorReplyDelegate;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_WAITING_FOR_SENSOR);
                    }
                    Client.Self.ScriptSensorReply -= ScriptSensorReplyDelegate;
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), CSV.FromEnumerable(csv));
                };
        }
    }
}