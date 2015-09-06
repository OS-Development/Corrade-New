using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> changeprimitivelink =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)), message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Action action = wasGetEnumValueFromDescription<Action>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.LINK:
                        case Action.DELINK:
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    List<string> items = new List<string>(wasCSVToEnumerable(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message)))
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
                    List<Primitive> primitives = new List<Primitive>();
                    foreach (string item in items)
                    {
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                StringOrUUID(item),
                                range,
                                ref primitive, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                        primitives.Add(primitive);
                    }
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
                                    Client.Network.Simulators.FirstOrDefault(
                                        o => o.Handle.Equals(rootPrimitive.RegionHandle)),
                                    primitives.Select(o => o.LocalID).ToList());
                                break;
                            case Action.DELINK:
                                Client.Objects.DelinkPrims(
                                    Client.Network.Simulators.FirstOrDefault(
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