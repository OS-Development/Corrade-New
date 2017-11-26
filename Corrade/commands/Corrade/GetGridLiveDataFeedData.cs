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
using CorradeConfigurationSharp;
using OpenMetaverse.StructuredData;
using wasSharp;
using OSD = wasOpenMetaverse.OSD;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getgridlivedatafeeddata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                        Task<byte[]> liveData = null;
                        switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))
                        ))
                        {
                            case Enumerations.Entity.STATISTICS:
                                liveData = GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                                    @"http://secondlife.com/xmlhttp/homepage.php",
                                    new Dictionary<string, string>());
                                break;

                            case Enumerations.Entity.LINDEX:
                                liveData = GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                                    @"http://secondlife.com/xmlhttp/lindex.php",
                                    new Dictionary<string, string>());
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                        }

                        if (liveData?.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_RETRIEVE_DATA);

                        var osdMap = OSD.XMLToOSD(Encoding.UTF8.GetString(liveData.Result)) as OSDMap;
                        if (osdMap == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_PROCESS_DATA);

                        var data =
                            new List<string>(OSD.OSDMapGet(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)), osdMap));

                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}