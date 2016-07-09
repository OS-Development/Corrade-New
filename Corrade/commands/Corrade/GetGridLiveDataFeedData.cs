///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CorradeConfiguration;
using OpenMetaverse.StructuredData;
using wasSharp;
using OSD = wasOpenMetaverse.OSD;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getgridlivedatafeeddata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    Task<byte[]> liveData = null;
                    switch (Reflection.GetEnumValueFromName<Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Entity.STATISTICS:
                            liveData = Web.wasGET(CORRADE_CONSTANTS.USER_AGENT,
                                @"http://secondlife.com/xmlhttp/homepage.php",
                                new Dictionary<string, string>(),
                                GroupCookieContainers[corradeCommandParameters.Group.UUID],
                                corradeConfiguration.ServicesTimeout);
                            break;
                        case Entity.LINDEX:
                            liveData = Web.wasGET(CORRADE_CONSTANTS.USER_AGENT,
                                @"http://secondlife.com/xmlhttp/lindex.php",
                                new Dictionary<string, string>(),
                                GroupCookieContainers[corradeCommandParameters.Group.UUID],
                                corradeConfiguration.ServicesTimeout);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }

                    if (liveData?.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_DATA);

                    var osdMap = OSD.XMLToOSD(Encoding.UTF8.GetString(liveData.Result)) as OSDMap;
                    if (osdMap == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_PROCESS_DATA);

                    var data =
                        new List<string>(OSD.OSDMapGet(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                corradeCommandParameters.Message)), osdMap));

                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}