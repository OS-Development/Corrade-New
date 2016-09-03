///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;
using OpenMetaverse;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getstatus = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                var separator = wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR;
                var filter = string.Empty;
                if (!string.IsNullOrEmpty(rule.Option))
                {
                    var parts = rule.Option.Split(wasOpenMetaverse.RLV.RLV_CONSTANTS.STATUS_SEPARATOR[0]);
                    if (parts.Length > 1 && parts[1].Length > 0)
                    {
                        separator = parts[1].Substring(0, 1);
                    }
                    if (parts.Length > 0 && parts[0].Length > 0)
                    {
                        filter = parts[0].ToLowerInvariant();
                    }
                }
                var response = new StringBuilder();
                lock (RLV.RLVRulesLock)
                {
                    var LockObject = new object();
                    RLVRules.AsParallel().Where(o =>
                        o.ObjectUUID.Equals(senderUUID) && o.Behaviour.Contains(filter)
                        ).ForAll(o =>
                        {
                            lock (LockObject)
                            {
                                response.AppendFormat("{0}{1}", separator, o.Behaviour);
                            }
                            if (!string.IsNullOrEmpty(o.Option))
                            {
                                lock (LockObject)
                                {
                                    response.AppendFormat("{0}{1}", wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR,
                                        o.Option);
                                }
                            }
                        });
                }
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.Chat(response.ToString(),
                        channel,
                        ChatType.Normal);
                }
            };
        }
    }
}