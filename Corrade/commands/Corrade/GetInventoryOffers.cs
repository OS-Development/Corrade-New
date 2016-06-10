///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getinventoryoffers =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var LockObject = new object();
                    var csv = new List<string>();
                    lock (InventoryOffersLock)
                    {
                        InventoryOffers.AsParallel().ForAll(o =>
                        {
                            var name =
                                new List<string>(
                                    Helpers.GetAvatarNames(o.Key.Offer.FromAgentName));
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), name.First()});
                                csv.AddRange(new[]
                                {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), name.Last()});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetNameFromEnumValue(ScriptKeys.TYPE),
                                    o.Key.AssetType.ToString()
                                });
                                csv.AddRange(new[]
                                {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Key.Offer.Message});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetNameFromEnumValue(ScriptKeys.SESSION),
                                    o.Key.Offer.IMSessionID.ToString()
                                });
                            }
                        });
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}