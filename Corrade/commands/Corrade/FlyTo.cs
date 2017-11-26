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
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> flyto =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    Vector3 position;
                    if (!Vector3.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                        out position))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    uint duration;
                    if (!uint.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DURATION)),
                                corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                        out duration))
                        duration = corradeConfiguration.ServicesTimeout;
                    float vicinity;
                    if (!float.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.VICINITY)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                        out vicinity))
                        vicinity = 2;
                    uint affinity;
                    if (!uint.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AFFINITY)),
                                corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                        out affinity))
                        affinity = 2;

                    // Generate the powers.
                    var segments =
                        new HashSet<int>(
                            Enumerable.Range(0, (int) affinity).Select(x => (int) Math.Pow(2, x)).Reverse());

                    var PositionReachedEvent = new ManualResetEventSlim(false);
                    EventHandler<TerseObjectUpdateEventArgs> TerseObjectUpdateEvent = (sender, args) =>
                    {
                        // If the distance is within the vicinity
                        if (Vector3.Distance(position, Client.Self.SimPosition) <= vicinity)
                        {
                            Client.Self.Movement.AtPos = false;
                            Client.Self.Movement.AtNeg = false;
                            Client.Self.Movement.UpPos = false;
                            Client.Self.Movement.UpNeg = false;
                            PositionReachedEvent.Set();
                            return;
                        }

                        // Only care about us.
                        if (!args.Update.LocalID.Equals(Client.Self.LocalID)) return;

                        // ZMovement
                        var diff = position.Z - Client.Self.SimPosition.Z;
                        Client.Self.Movement.UpPos = diff > 16 || segments.Select(
                                                             o =>
                                                                 new
                                                                 {
                                                                     f = new Func<int, bool>(
                                                                         p => diff > vicinity * p &&
                                                                              Client.Self.Velocity.Z < p * 2),
                                                                     i = o
                                                                 })
                                                         .Select(p => p.f.Invoke(p.i)).Any(o => o.Equals(true));
                        Client.Self.Movement.UpNeg = diff < -23 || segments.Select(
                                                             o =>
                                                                 new
                                                                 {
                                                                     f = new Func<int, bool>(
                                                                         p => diff < -vicinity * p &&
                                                                              Client.Self.Velocity.Z > -p * 2),
                                                                     i = o
                                                                 })
                                                         .Select(p => p.f.Invoke(p.i)).Any(o => o.Equals(true));

                        // XYMovement
                        diff = Vector2.Distance(new Vector2(position.X, position.Y),
                            new Vector2(Client.Self.SimPosition.X, Client.Self.SimPosition.Y));
                        var velocity = new Vector2(Client.Self.Velocity.X, Client.Self.Velocity.Y).Length();
                        Client.Self.Movement.AtPos = diff >= 16 || segments.Select(o => new
                        {
                            f = new Func<int, bool>(
                                p => diff >= vicinity * p && velocity < p * 2),
                            i = o
                        }).Select(p => p.f.Invoke(p.i)).Any(o => o.Equals(true));
                        Client.Self.Movement.AtNeg = false;

                        Client.Self.Movement.TurnToward(position);
                    };

                    var succeeded = true;

                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Objects.TerseObjectUpdate += TerseObjectUpdateEvent;
                    Client.Self.Movement.AtPos = false;
                    Client.Self.Movement.AtNeg = false;
                    Client.Self.Movement.UpNeg = false;
                    Client.Self.Fly(true);
                    // Initial thrust.
                    Client.Self.Movement.UpPos = true;
                    if (!PositionReachedEvent.Wait((int) duration))
                        succeeded = false;
                    Client.Objects.TerseObjectUpdate -= TerseObjectUpdateEvent;
                    Client.Self.Movement.AtPos = false;
                    Client.Self.Movement.AtNeg = false;
                    Client.Self.Movement.UpPos = false;
                    Client.Self.Movement.UpNeg = false;
                    Locks.ClientInstanceSelfLock.ExitWriteLock();

                    // in case the flying timed out, then bail
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_REACHING_DESTINATION);

                    // perform the post-action
                    bool fly;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLY)),
                            corradeCommandParameters.Message)), out fly))
                    {
                        case true:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Fly(fly);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }

                    SaveMovementState.Invoke();
                };
        }
    }
}