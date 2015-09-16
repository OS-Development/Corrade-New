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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytoscriptpermissionrequest =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID itemUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)),
                            out itemUUID))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID taskUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TASK)),
                                corradeCommandParameters.Message)),
                            out taskUUID))
                    {
                        throw new ScriptException(ScriptError.NO_TASK_SPECIFIED);
                    }
                    int permissionMask = 0;
                    Parallel.ForEach(wasCSVToEnumerable(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o =>
                            Parallel.ForEach(
                                typeof (ScriptPermission).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                q => { permissionMask |= ((int) q.GetValue(null)); }));
                    string region = wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            corradeCommandParameters.Message));
                    Simulator simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o => o.Name.Equals(region, StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Client.Self.ScriptQuestionReply(simulator, itemUUID, taskUUID,
                        (ScriptPermission) permissionMask);
                };
        }
    }
}