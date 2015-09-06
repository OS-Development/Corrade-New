using System;
using System.Globalization;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> setrot = (message, rule, senderUUID) =>
            {
                double rotation;
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE) ||
                    !double.TryParse(rule.Option, NumberStyles.Float, CultureInfo.DefaultThreadCurrentCulture,
                        out rotation))
                {
                    return;
                }
                Client.Self.Movement.UpdateFromHeading(Math.PI/2d - rotation, true);
            };
        }
    }
}