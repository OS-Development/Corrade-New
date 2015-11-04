///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> getstatus = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                string separator = RLV_CONSTANTS.PATH_SEPARATOR;
                string filter = string.Empty;
                if (!string.IsNullOrEmpty(rule.Option))
                {
                    string[] parts = rule.Option.Split(RLV_CONSTANTS.STATUS_SEPARATOR[0]);
                    if (parts.Length > 1 && parts[1].Length > 0)
                    {
                        separator = parts[1].Substring(0, 1);
                    }
                    if (parts.Length > 0 && parts[0].Length > 0)
                    {
                        filter = parts[0].ToLowerInvariant();
                    }
                }
                StringBuilder response = new StringBuilder();
                lock (RLVRulesLock)
                {
                    object LockObject = new object();
                    Parallel.ForEach(RLVRules.AsParallel().Where(o =>
                        o.ObjectUUID.Equals(senderUUID) && o.Behaviour.Contains(filter)
                        ), o =>
                        {
                            lock (LockObject)
                            {
                                response.AppendFormat("{0}{1}", separator, o.Behaviour);
                            }
                            if (!string.IsNullOrEmpty(o.Option))
                            {
                                lock (LockObject)
                                {
                                    response.AppendFormat("{0}{1}", RLV_CONSTANTS.PATH_SEPARATOR, o.Option);
                                }
                            }
                        });
                }
                Client.Self.Chat(response.ToString(),
                    channel,
                    ChatType.Normal);
            };
        }
    }
}