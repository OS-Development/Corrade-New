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
            public static Action<Group, string, Dictionary<string, string>> teleport =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name,
                            (int) Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    // We override the default teleport since region names are unique and case insensitive.
                    ulong regionHandle = 0;
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    if (string.IsNullOrEmpty(region))
                    {
                        region = Client.Network.CurrentSim.Name;
                    }
                    ManualResetEvent GridRegionEvent = new ManualResetEvent(false);
                    EventHandler<GridRegionEventArgs> GridRegionEventHandler =
                        (sender, args) =>
                        {
                            if (!args.Region.Name.Equals(region, StringComparison.InvariantCultureIgnoreCase))
                                return;
                            regionHandle = args.Region.RegionHandle;
                            GridRegionEvent.Set();
                        };
                    lock (ClientInstanceGridLock)
                    {
                        Client.Grid.GridRegion += GridRegionEventHandler;
                        Client.Grid.RequestMapRegion(region, GridLayerType.Objects);
                        if (!GridRegionEvent.WaitOne(Client.Settings.MAP_REQUEST_TIMEOUT, false))
                        {
                            Client.Grid.GridRegion -= GridRegionEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_REGION);
                        }
                        Client.Grid.GridRegion -= GridRegionEventHandler;
                    }
                    if (regionHandle.Equals(0))
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    if (regionHandle.Equals(Client.Network.CurrentSim.Handle) &&
                        Vector3.Distance(Client.Self.SimPosition, position) <
                        LINDEN_CONSTANTS.REGION.TELEPORT_MINIMUM_DISTANCE)
                    {
                        throw new ScriptException(ScriptError.DESTINATION_TOO_CLOSE);
                    }
                    ManualResetEvent TeleportEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    EventHandler<TeleportEventArgs> TeleportEventHandler = (sender, args) =>
                    {
                        switch (args.Status)
                        {
                            case TeleportStatus.Cancelled:
                            case TeleportStatus.Failed:
                            case TeleportStatus.Finished:
                                succeeded = args.Status.Equals(TeleportStatus.Finished);
                                TeleportEvent.Set();
                                break;
                        }
                    };
                    if (IsSecondLife() && !TimedTeleportThrottle.IsSafe)
                    {
                        throw new ScriptException(ScriptError.TELEPORT_THROTTLED);
                    }
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                    {
                        Client.Self.Stand();
                    }
                    // stop all non-built-in animations
                    List<UUID> lindenAnimations = new List<UUID>(typeof (Animations).GetProperties(
                        BindingFlags.Public |
                        BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)).ToList());
                    Parallel.ForEach(Client.Self.SignaledAnimations.Copy().Keys, o =>
                    {
                        if (!lindenAnimations.Contains(o))
                            Client.Self.AnimationStop(o, true);
                    });
                    lock (ClientInstanceSelfLock)
                    {
                        Client.Self.TeleportProgress += TeleportEventHandler;
                        Client.Self.Teleport(regionHandle, position);
                        if (!TeleportEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Self.TeleportProgress -= TeleportEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_DURING_TELEPORT);
                        }
                        Client.Self.TeleportProgress -= TeleportEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.TELEPORT_FAILED);
                    }
                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                };
        }
    }
}