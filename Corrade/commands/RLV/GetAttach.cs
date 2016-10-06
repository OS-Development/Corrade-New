///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getattach =
                (message, rule, senderUUID) =>
                {
                    int channel;
                    if (!int.TryParse(rule.Param, out channel) || channel < 1)
                    {
                        return;
                    }
                    var attachments = new HashSet<Primitive>(
                        Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                            .Select(o => o.Key));
                    var response = new StringBuilder();
                    if (!attachments.Any())
                    {
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(response.ToString(), channel, ChatType.Normal);
                        }
                        return;
                    }
                    var attachmentPoints =
                        new HashSet<AttachmentPoint>(attachments.AsParallel()
                            .Select(o => o.PrimData.AttachmentPoint));
                    switch (!string.IsNullOrEmpty(rule.Option))
                    {
                        case true:
                            var RLVattachment = wasOpenMetaverse.RLV.RLVAttachments.AsParallel().FirstOrDefault(
                                o => Strings.Equals(rule.Option, o.Name, StringComparison.InvariantCultureIgnoreCase));
                            switch (!RLVattachment.Equals(default(wasOpenMetaverse.RLV.RLVAttachment)))
                            {
                                case true:
                                    if (!attachmentPoints.Contains(RLVattachment.AttachmentPoint))
                                        goto default;
                                    response.Append(wasOpenMetaverse.RLV.RLV_CONSTANTS.TRUE_MARKER);
                                    break;
                                default:
                                    response.Append(wasOpenMetaverse.RLV.RLV_CONSTANTS.FALSE_MARKER);
                                    break;
                            }
                            break;
                        default:
                            var data = new string[wasOpenMetaverse.RLV.RLVAttachments.Count];
                            Enumerable.Range(0, wasOpenMetaverse.RLV.RLVAttachments.Count).AsParallel().ForAll(o =>
                            {
                                switch (
                                    !attachmentPoints.Contains(wasOpenMetaverse.RLV.RLVAttachments[o].AttachmentPoint))
                                {
                                    case true:
                                        data[o] = wasOpenMetaverse.RLV.RLV_CONSTANTS.FALSE_MARKER;
                                        return;
                                    default:
                                        data[o] = wasOpenMetaverse.RLV.RLV_CONSTANTS.TRUE_MARKER;
                                        break;
                                }
                            });
                            response.Append(string.Join("", data.ToArray()));
                            break;
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Chat(response.ToString(), channel, ChatType.Normal);
                    }
                };
        }
    }
}