using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getinventoryoffers =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Inventory))
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
                                {wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME), name.First()});
                                csv.AddRange(new[]
                                {wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME), name.Last()});
                                csv.AddRange(new[]
                                {
                                    wasGetDescriptionFromEnumValue(ScriptKeys.TYPE),
                                    o.Key.AssetType.ToString()
                                });
                                csv.AddRange(new[]
                                {wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE), o.Key.Offer.Message});
                                csv.AddRange(new[]
                                {
                                    wasGetDescriptionFromEnumValue(ScriptKeys.SESSION),
                                    o.Key.Offer.IMSessionID.ToString()
                                });
                            }
                        });
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