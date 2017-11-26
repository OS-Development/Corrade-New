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
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getprimitivepayprices =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        float range;
                        if (
                            !float.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                    corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out range))
                            range = corradeConfiguration.Range;
                        Primitive primitive = null;
                        var item = wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(item))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                        UUID itemUUID;
                        switch (UUID.TryParse(item, out itemUUID))
                        {
                            case true:
                                if (
                                    !Services.FindPrimitive(Client,
                                        itemUUID,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                break;

                            default:
                                if (
                                    !Services.FindPrimitive(Client,
                                        item,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                break;
                        }
                        if (primitive.Properties.SaleType.Equals(SaleType.Not))
                            throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOR_SALE);
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var region =
                            Client.Network.Simulators.AsParallel()
                                .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        var csv = new List<string>();
                        var PayPrceReceivedEvent = new ManualResetEventSlim(false);
                        EventHandler<PayPriceReplyEventArgs> PayPriceReplyEventHandler = (sender, args) =>
                        {
                            if (!args.ObjectID.Equals(primitive.ID) || !args.Simulator.Handle.Equals(region.Handle))
                                return;

                            csv.Add(args.DefaultPrice.ToString(Utils.EnUsCulture));
                            csv.AddRange(
                                args.ButtonPrices.Select(o => o.ToString(Utils.EnUsCulture)));
                            PayPrceReceivedEvent.Set();
                        };
                        Locks.ClientInstanceObjectsLock.EnterReadLock();
                        Client.Objects.PayPriceReply += PayPriceReplyEventHandler;
                        Client.Objects.RequestPayPrice(region,
                            primitive.ID);
                        if (!PayPrceReceivedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Objects.PayPriceReply -= PayPriceReplyEventHandler;
                            Locks.ClientInstanceObjectsLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_REQUESTING_PRICE);
                        }
                        Client.Objects.PayPriceReply -= PayPriceReplyEventHandler;
                        Locks.ClientInstanceObjectsLock.ExitReadLock();
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}