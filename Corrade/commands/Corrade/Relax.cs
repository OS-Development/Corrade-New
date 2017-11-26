///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> relax =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        Client.Self.Stand();
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEANIMATE)),
                                    corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.SitOnGround();
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // Set the camera on the avatar.
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                    );
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}