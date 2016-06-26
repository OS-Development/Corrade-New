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
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
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
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                    var action = Reflection.GetEnumValueFromName<Action>(
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
                    var items = new List<string>(CSV.ToEnumerable(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)), corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)));
                    if (!items.Any() || (action.Equals(Action.LINK) && items.Count < 2))
                    {
                        throw new ScriptException(ScriptError.INVALID_NUMBER_OF_ITEMS_SPECIFIED);
                    }
                    if (Helpers.IsSecondLife(Client))
                    {
                        if (items.Count > Constants.OBJECTS.MAXIMUM_PRIMITIVE_COUNT)
                        {
                            throw new ScriptException(ScriptError.LINK_WOULD_EXCEED_MAXIMUM_LINK_LIMIT);
                        }
                    }

                    var LockObject = new object();
                    var updatePrimitives = Services.GetPrimitives(Client, range);

                    // allow partial results
                    Services.UpdatePrimitives(Client, ref updatePrimitives, corradeConfiguration.DataTimeout);

                    var searchPrimitives = new Primitive[items.Count];
                    var succeeded = true;
                    Parallel.ForEach(Enumerable.Range(0, items.Count), (o, state) =>
                    {
                        Primitive primitive;
                        UUID itemUUID;
                        switch (UUID.TryParse(items[o], out itemUUID))
                        {
                            case true:
                                primitive = updatePrimitives.AsParallel().FirstOrDefault(p => p.ID.Equals(itemUUID));
                                break;
                            default:
                                primitive =
                                    updatePrimitives.AsParallel()
                                        .Where(p => p.Properties != null)
                                        .FirstOrDefault(p => p.Properties.Name.Equals(items[o]));
                                break;
                        }
                        switch (primitive != null)
                        {
                            case true:
                                searchPrimitives[o] = primitive;
                                break;
                            default:
                                succeeded = false;
                                state.Break();
                                break;
                        }
                    });

                    if (!succeeded) throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);

                    var primitives = searchPrimitives.ToList();
                    var rootPrimitive = primitives.First();
                    if (!primitives.AsParallel().All(o => o.RegionHandle.Equals(rootPrimitive.RegionHandle)))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVES_NOT_IN_SAME_REGION);
                    }
                    var PrimChangeLinkEvent = new ManualResetEvent(false);
                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
                    {
                        lock (LockObject)
                        {
                            if (!primitives.Any())
                            {
                                PrimChangeLinkEvent.Set();
                                return;
                            }
                            if (primitives.Any(o => o.LocalID.Equals(args.Prim.LocalID)))
                            {
                                primitives.RemoveAll(o => o.LocalID.Equals(args.Prim.LocalID));
                            }
                        }
                    };
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o => o.Handle.Equals(rootPrimitive.RegionHandle));
                    }
                    if (simulator == null)
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                        switch (action)
                        {
                            case Action.LINK:
                                Client.Objects.LinkPrims(
                                    simulator,
                                    primitives.Select(o => o.LocalID).ToList());
                                break;
                            case Action.DELINK:
                                Client.Objects.DelinkPrims(
                                    simulator,
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