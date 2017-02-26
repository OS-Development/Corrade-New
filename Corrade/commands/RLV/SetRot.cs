///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using System.Globalization;
using OpenMetaverse;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> setrot =
                (message, rule, senderUUID) =>
                {
                    double rotation;
                    if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE) ||
                        !double.TryParse(rule.Option, NumberStyles.Float, Utils.EnUsCulture,
                            out rotation))
                    {
                        return;
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Movement.UpdateFromHeading(Math.PI/2d - rotation, true);
                    }
                };
        }
    }
}