///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setmovementdata
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming) ||
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Locks.ClientInstanceSelfLock.EnterReadLock();
                    var movement = Client.Self.Movement;
                    Locks.ClientInstanceSelfLock.ExitReadLock();
                    movement = movement.wasCSVToStructure(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message)), wasInput);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.AlwaysRun = movement.AlwaysRun;
                    Client.Self.Movement.AtNeg = movement.AtNeg;
                    Client.Self.Movement.AtPos = movement.AtPos;
                    Client.Self.Movement.AutoResetControls = movement.AutoResetControls;
                    Client.Self.Movement.Away = movement.Away;
                    Client.Self.Movement.BodyRotation = movement.BodyRotation;
                    Client.Self.Movement.FastAt = movement.FastAt;
                    Client.Self.Movement.FastLeft = movement.FastLeft;
                    Client.Self.Movement.FastUp = movement.FastUp;
                    Client.Self.Movement.Flags = movement.Flags;
                    Client.Self.Movement.Fly = movement.Fly;
                    Client.Self.Movement.HeadRotation = movement.HeadRotation;
                    Client.Self.Movement.LButtonDown = movement.LButtonDown;
                    Client.Self.Movement.MLButtonUp = movement.MLButtonUp;
                    Client.Self.Movement.Mouselook = movement.Mouselook;
                    Client.Self.Movement.NudgeAtNeg = movement.NudgeAtNeg;
                    Client.Self.Movement.NudgeAtPos = movement.NudgeAtPos;
                    Client.Self.Movement.NudgeLeftNeg = movement.LeftNeg;
                    Client.Self.Movement.NudgeLeftPos = movement.NudgeLeftPos;
                    Client.Self.Movement.NudgeUpNeg = movement.NudgeUpPos;
                    Client.Self.Movement.PitchNeg = movement.PitchNeg;
                    Client.Self.Movement.PitchPos = movement.PitchPos;
                    Client.Self.Movement.SitOnGround = movement.SitOnGround;
                    Client.Self.Movement.StandUp = movement.StandUp;
                    Client.Self.Movement.State = movement.State;
                    Client.Self.Movement.Stop = movement.Stop;
                    Client.Self.Movement.TurnLeft = movement.TurnLeft;
                    Client.Self.Movement.TurnRight = movement.TurnRight;
                    Client.Self.Movement.UpdateInterval = movement.UpdateInterval;
                    Client.Self.Movement.UpNeg = movement.UpNeg;
                    Client.Self.Movement.UpPos = movement.UpPos;
                    Client.Self.Movement.YawNeg = movement.YawNeg;
                    Client.Self.Movement.YawPos = movement.YawPos;
                    // Send update.
                    Client.Self.Movement.SendUpdate(true);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // Save movement state.
                    SaveMovementState.Invoke();
                };
        }
    }
}