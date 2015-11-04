///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> changeprimitivelink =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Action action = Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.LINK:
                        case Action.DELINK:
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    List<string> items = new List<string>(CSV.ToEnumerable(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)), corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)));
                    if (!items.Any() || (action.Equals(Action.LINK) && items.Count < 2))
                    {
                        throw new ScriptException(ScriptError.INVALID_NUMBER_OF_ITEMS_SPECIFIED);
                    }
                    if (IsSecondLife())
                    {
                        if (items.Count > LINDEN_CONSTANTS.OBJECTS.MAXIMUM_PRIMITIVE_COUNT)
                        {
                            throw new ScriptException(ScriptError.LINK_WOULD_EXCEED_MAXIMUM_LINK_LIMIT);
                        }
                    }
                    Primitive[] searchPrimitives = new Primitive[items.Count];
                    bool succeeded = true;
                    Parallel.ForEach(Enumerable.Range(0, items.Count), (o, state) =>
                    {
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                StringOrUUID(items[o]),
                                range,
                                ref primitive, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout))
                        {
                            succeeded = false;
                            state.Break();
                        }
                        searchPrimitives[o] = primitive;
                    });
                    if (!succeeded) throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    List<Primitive> primitives = searchPrimitives.ToList();
                    Primitive rootPrimitive = primitives.First();
                    if (!primitives.AsParallel().All(o => o.RegionHandle.Equals(rootPrimitive.RegionHandle)))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVES_NOT_IN_SAME_REGION);
                    }
                    object LockObject = new object();
                    ManualResetEvent PrimChangeLinkEvent = new ManualResetEvent(false);
                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
                    {
                        lock (LockObject)
                        {
                            if (!primitives.Any())
                            {
                                PrimChangeLinkEvent.Set();
                                return;
                            }
                        }
                        lock (LockObject)
                        {
                            if (primitives.Any(o => o.LocalID.Equals(args.Prim.LocalID)))
                            {
                                primitives.RemoveAll(o => o.LocalID.Equals(args.Prim.LocalID));
                            }
                        }
                    };
                    lock (ClientInstanceObjectsLock)
                    {
                        Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                        switch (action)
                        {
                            case Action.LINK:
                                Client.Objects.LinkPrims(
                                    Client.Network.Simulators.AsParallel().FirstOrDefault(
                                        o => o.Handle.Equals(rootPrimitive.RegionHandle)),
                                    primitives.Select(o => o.LocalID).ToList());
                                break;
                            case Action.DELINK:
                                Client.Objects.DelinkPrims(
                                    Client.Network.Simulators.AsParallel().FirstOrDefault(
                                        o => o.Handle.Equals(rootPrimitive.RegionHandle)),
                                    primitives.Select(o => o.LocalID).ToList());
                                break;
                        }
                        if (!PrimChangeLinkEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_CHANGING_LINKS);
                        }
                        Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                    }
                };
        }
    }
}