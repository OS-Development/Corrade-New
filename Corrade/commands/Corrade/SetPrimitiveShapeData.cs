///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Corrade.Constants;
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
                setprimitiveshapedata =
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
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        // build the primitive shape from presets by supplying "type" (or not)...
                        var primitiveShapesFieldInfo = typeof(CORRADE_CONSTANTS.PRIMTIVE_BODIES).GetFields(
                                BindingFlags.Public |
                                BindingFlags.Static)
                            .AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                                corradeCommandParameters.Message)),
                                        StringComparison.OrdinalIgnoreCase));
                        Primitive.ConstructionData constructionData;
                        switch (primitiveShapesFieldInfo != null)
                        {
                            case true:
                                constructionData = (Primitive.ConstructionData) primitiveShapesFieldInfo.GetValue(null);
                                break;

                            default:
                                constructionData = primitive.PrimData;
                                break;
                        }
                        // ... and overwrite with manual data settings.
                        constructionData =
                            constructionData.wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)), wasInput);
                        Locks.ClientInstanceObjectsLock.EnterWriteLock();
                        Client.Objects.SetShape(simulator,
                            primitive.LocalID, constructionData);
                        Locks.ClientInstanceObjectsLock.ExitWriteLock();
                    };
        }
    }
}