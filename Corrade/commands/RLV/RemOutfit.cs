///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
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
                                p => p.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        if (wearTypeInfo == null)
                        {
                            break;
                        }
                        InventoryItem wearable =
                            GetWearables(CurrentOutfitFolder)
                                .AsParallel()
                                .FirstOrDefault(
                                    o =>
                                        !IsBodyPart(o) &&
                                        (o as InventoryWearable).WearableType.Equals(
                                            (WearableType) wearTypeInfo.GetValue(null)));
                        if (wearable != null)
                        {
                            UnWear(wearable);
                        }
                        break;
                    default:
                        Parallel.ForEach(
                            GetWearables(CurrentOutfitFolder).Where(o => !IsBodyPart(o as InventoryWearable)), UnWear);
                        break;
                }
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}