using System;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule> version = (message, rule) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                Client.Self.Chat(
                    $"{RLV_CONSTANTS.VIEWER} v{RLV_CONSTANTS.SHORT_VERSION} (Corrade Version: {CORRADE_CONSTANTS.CORRADE_VERSION} Compiled: {CORRADE_CONSTANTS.CORRADE_COMPILE_DATE})",
                    channel,
                    ChatType.Normal);
            };
        }
    }
}