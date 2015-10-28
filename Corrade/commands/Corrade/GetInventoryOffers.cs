///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorradeConfiguration;
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
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    object LockObject = new object();
                    List<string> csv = new List<string>();
                    lock (InventoryOffersLock)
                    {
                        Parallel.ForEach(InventoryOffers, o =>
                        {
                            List<string> name =
                                new List<string>(
                                    GetAvatarNames(o.Key.Offer.FromAgentName));
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                {Reflection.wasGetNameFromEnumValue(ScriptKeys.FIRSTNAME), name.First()});
                                csv.AddRange(new[]
                                {Reflection.wasGetNameFromEnumValue(ScriptKeys.FIRSTNAME), name.Last()});
                                csv.AddRange(new[]
                                {
                                    Reflection.wasGetNameFromEnumValue(ScriptKeys.TYPE),
                                    o.Key.AssetType.ToString()
                                });
                                csv.AddRange(new[]
                                {Reflection.wasGetNameFromEnumValue(ScriptKeys.MESSAGE), o.Key.Offer.Message});
                                csv.AddRange(new[]
                                {
                                    Reflection.wasGetNameFromEnumValue(ScriptKeys.SESSION),
                                    o.Key.Offer.IMSessionID.ToString()
                                });
                            }
                        });
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                            CSV.wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}