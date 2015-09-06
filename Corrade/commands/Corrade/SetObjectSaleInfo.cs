///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> setobjectsaleinfo =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int price;
                    if (
                        !int.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PRICE)), message)),
                            out price))
                    {
                        throw new ScriptException(ScriptError.INVALID_PRICE);
                    }
                    if (price < 0)
                    {
                        throw new ScriptException(ScriptError.INVALID_PRICE);
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
                    // if the primitive is not an object (the root) or the primitive
                    // is not an object as an avatar attachment then bail out
                    if (!primitive.ParentID.Equals(0) && !primitive.ParentID.Equals(Client.Self.LocalID))
                    {
                        throw new ScriptException(ScriptError.ITEM_IS_NOT_AN_OBJECT);
                    }
                    FieldInfo saleTypeInfo = typeof (SaleType).GetFields(BindingFlags.Public |
                                                                         BindingFlags.Static)
                        .AsParallel().FirstOrDefault(o =>
                            o.Name.Equals(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                        message)),
                                StringComparison.Ordinal));
                    Client.Objects.SetSaleInfo(
                        Client.Network.Simulators.FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
                        primitive.LocalID, saleTypeInfo != null
                            ? (SaleType)
                                saleTypeInfo.GetValue(null)
                            : SaleType.Copy, price);
                };
        }
    }
}