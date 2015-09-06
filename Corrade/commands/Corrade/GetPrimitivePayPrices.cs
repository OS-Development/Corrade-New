///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getprimitivepayprices =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)), message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    if (primitive.Properties.SaleType.Equals(SaleType.Not))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOR_SALE);
                    }
                    List<string> csv = new List<string>();
                    ManualResetEvent PayPrceReceivedEvent = new ManualResetEvent(false);
                    EventHandler<PayPriceReplyEventArgs> PayPriceReplyEventHandler = (sender, args) =>
                    {
                        csv.Add(args.DefaultPrice.ToString(CultureInfo.DefaultThreadCurrentCulture));
                        csv.AddRange(
                            args.ButtonPrices.Select(o => o.ToString(CultureInfo.DefaultThreadCurrentCulture)));
                        PayPrceReceivedEvent.Set();
                    };
                    lock (ClientInstanceObjectsLock)
                    {
                        Client.Objects.PayPriceReply += PayPriceReplyEventHandler;
                        Client.Objects.RequestPayPrice(
                            Client.Network.Simulators.FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
                            primitive.ID);
                        if (!PayPrceReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Objects.PayPriceReply -= PayPriceReplyEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_REQUESTING_PRICE);
                        }
                        Client.Objects.PayPriceReply -= PayPriceReplyEventHandler;
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}