///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> remoutfit = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true: // A single wearable
                        FieldInfo wearTypeInfo = typeof (WearableType).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                            .AsParallel().FirstOrDefault(
                                p => string.Equals(rule.Option, p.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (wearTypeInfo == null)
                        {
                            break;
                        }
                        InventoryItem wearable =
                            Inventory.GetWearables(Client, CurrentOutfitFolder)
                                .AsParallel()
                                .FirstOrDefault(
                                    o =>
                                        !Inventory.IsBodyPart(Client, o) &&
                                        (o as InventoryWearable).WearableType.Equals(
                                            (WearableType) wearTypeInfo.GetValue(null)));
                        if (wearable != null)
                        {
                            Inventory.UnWear(Client, CurrentOutfitFolder, wearable, corradeConfiguration.ServicesTimeout);
                        }
                        break;
                    default:
                        Parallel.ForEach(
                            Inventory.GetWearables(Client, CurrentOutfitFolder)
                                .Where(o => !Inventory.IsBodyPart(Client, o as InventoryWearable)),
                            o => Inventory.UnWear(Client, CurrentOutfitFolder, o, corradeConfiguration.ServicesTimeout));
                        break;
                }
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}