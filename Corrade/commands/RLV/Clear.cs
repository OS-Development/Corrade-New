using System;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> clear = (message, rule, senderUUID) =>
            {
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        lock (RLVRulesLock)
                        {
                            RLVRules.RemoveWhere(o => o.Behaviour.Contains(rule.Behaviour));
                        }
                        break;
                    case false:
                        lock (RLVRulesLock)
                        {
                            RLVRules.RemoveWhere(o => o.ObjectUUID.Equals(senderUUID));
                        }
                        break;
                }
            };
        }
    }
}