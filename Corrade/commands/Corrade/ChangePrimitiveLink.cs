///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                changeprimitivelink =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        float range;
                        if (
                            !float.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                    corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out range))
                            range = corradeConfiguration.Range;
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                        );
                        var items = new List<string>(CSV.ToEnumerable(wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)))
                            .AsParallel()
                            .Where(o => !string.IsNullOrEmpty(o)));
                        if (!items.Any() || action.Equals(Enumerations.Action.LINK) && items.Count < 2)
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.INVALID_NUMBER_OF_ITEMS_SPECIFIED);
                        if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                            if (items.Count > wasOpenMetaverse.Constants.OBJECTS.MAXIMUM_PRIMITIVE_COUNT)
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.LINK_WOULD_EXCEED_MAXIMUM_LINK_LIMIT);

                        var LockObject = new object();

                        // Update the primitives.
                        var updatePrimitives = new HashSet<Primitive>();
                        Services.GetPrimitives(Client, range).AsParallel().ForAll(o =>
                        {
                            if (Services.UpdatePrimitive(Client, ref o, corradeConfiguration.DataTimeout))
                                lock (LockObject)
                                {
                                    updatePrimitives.Add(o);
                                }
                        });

                        var searchPrimitives = new Primitive[items.Count];
                        var succeeded = true;
                        Parallel.ForEach(Enumerable.Range(0, items.Count), (o, state) =>
                        {
                            Primitive primitive;
                            UUID itemUUID;
                            switch (UUID.TryParse(items[o], out itemUUID))
                            {
                                case true:
                                    primitive = updatePrimitives.AsParallel()
                                        .FirstOrDefault(p => p.ID.Equals(itemUUID));
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

                        if (!succeeded) throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);

                        var primitives = searchPrimitives.ToList();
                        var rootPrimitive = primitives.First();
                        if (!primitives.Skip(1).AsParallel()
                            .All(o => o.RegionHandle.Equals(rootPrimitive.RegionHandle)))
                            throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVES_NOT_IN_SAME_REGION);

                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o => o.Handle.Equals(rootPrimitive.RegionHandle));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);

                        var PrimChangeLinkEvent = new ManualResetEventSlim(false);
                        var primitivesIDs = new HashSet<uint>();
                        var linkedPrimitives = 0;
                        EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
                        {
                            lock (LockObject)
                            {
                                if (!primitivesIDs.Contains(args.Prim.LocalID)) return;
                                primitivesIDs.Remove(args.Prim.LocalID);
                                if (primitivesIDs.Count - linkedPrimitives != 0) return;
                                PrimChangeLinkEvent.Set();
                            }
                        };

                        switch (action)
                        {
                            case Enumerations.Action.LINK:
                                bool restructure;
                                if (!bool.TryParse(wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RESTRUCTURE)),
                                        corradeCommandParameters.Message)), out restructure))
                                    restructure = false;
                                switch (restructure)
                                {
                                    case true:
                                        // If the links have to be restructured, then delink all the primitivws.
                                        primitivesIDs.UnionWith(primitives.Select(o => o.LocalID));
                                        Locks.ClientInstanceObjectsLock.EnterWriteLock();
                                        Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                                        Client.Objects.DelinkPrims(simulator, primitivesIDs.ToList());
                                        if (
                                            !PrimChangeLinkEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                        {
                                            Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                            Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_CHANGING_LINKS);
                                        }
                                        Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                        Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                        break;

                                    default:
                                        // If all primitives are linked to the root and no restructuring is requested then abandon.
                                        if (
                                            primitives.Skip(1)
                                                .AsParallel()
                                                .All(
                                                    o =>
                                                        !o.ParentID.Equals(0) &&
                                                        o.ParentID.Equals(rootPrimitive.LocalID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.PRIMITIVES_ALREADY_LINKED);
                                        // If all primitives have a common parent then abandon.
                                        if (
                                            primitives.Skip(1)
                                                .AsParallel()
                                                .All(
                                                    o =>
                                                        !o.ParentID.Equals(0) &&
                                                        o.ParentID.Equals(rootPrimitive.ParentID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.PRIMITIVES_ARE_CHILDREN_OF_OBJECT);
                                        break;
                                }
                                // Get the number of primitives that are already linked.
                                linkedPrimitives = primitives.Count(o => !o.ParentID.Equals(0));
                                // Get a hashset of primitive local IDs.
                                primitivesIDs.UnionWith(primitives.Select(o => o.LocalID));
                                PrimChangeLinkEvent.Reset();
                                Locks.ClientInstanceObjectsLock.EnterWriteLock();
                                Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                                // Link the primitives.
                                Client.Objects.LinkPrims(simulator, primitivesIDs.ToList());
                                if (!PrimChangeLinkEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                {
                                    Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                    Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.TIMEOUT_CHANGING_LINKS);
                                }
                                Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.DELINK:
                                // If all primitives to delink are not linked to any other primitive then abandon.
                                if (primitives.AsParallel().All(o => o.ParentID.Equals(0)))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.PRIMITIVES_ALREADY_DELINKED);

                                primitivesIDs.UnionWith(primitives.Select(o => o.LocalID));
                                Locks.ClientInstanceObjectsLock.EnterWriteLock();
                                Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                                Client.Objects.DelinkPrims(simulator, primitivesIDs.ToList());
                                if (!PrimChangeLinkEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                {
                                    Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                    Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.TIMEOUT_CHANGING_LINKS);
                                }
                                Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                Locks.ClientInstanceObjectsLock.ExitWriteLock();
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}