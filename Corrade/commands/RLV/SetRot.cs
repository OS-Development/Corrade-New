///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> setrot = (message, rule, senderUUID) =>
            {
                double rotation;
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE) ||
                    !double.TryParse(rule.Option, NumberStyles.Float, Utils.EnUsCulture,
                        out rotation))
                {
                    return;
                }
                Client.Self.Movement.UpdateFromHeading(Math.PI/2d - rotation, true);
            };
        }
    }
}