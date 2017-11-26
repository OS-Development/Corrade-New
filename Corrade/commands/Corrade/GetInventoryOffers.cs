///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getinventoryoffers =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Inventory))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var LockObject = new object();
                        var csv = new List<string>();
                        lock (InventoryOffersLock)
                        {
                            InventoryOffers.Values.AsParallel().ForAll(o =>
                            {
                                var fullName =
                                    new List<string>(
                                        wasOpenMetaverse.Helpers.GetAvatarNames(o.Args.Offer.FromAgentName));
                                if (!fullName.Any())
                                    return;
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME), fullName.First()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME), fullName.Last()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE),
                                        o.Args.AssetType.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE),
                                        o.Args.Offer.Message
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION),
                                        o.Args.Offer.IMSessionID.ToString()
                                    });
                                }
                            });
                        }
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}