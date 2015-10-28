///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getestatecovenant =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    ManualResetEvent EstateCovenantReceivedEvent = new ManualResetEvent(false);
                    List<string> csv = new List<string>();
                    EventHandler<EstateCovenantReplyEventArgs> EstateCovenantReplyEventhandler = (sender, args) =>
                    {
                        csv.AddRange(new[]
                        {
                            args.CovenantID.ToString(),
                            args.EstateName,
                            args.EstateOwnerID.ToString(),
                            args.Timestamp.ToString()
                        });
                        EstateCovenantReceivedEvent.Set();
                    };
                    lock (ClientInstanceEstateLock)
                    {
                        Client.Estate.EstateCovenantReply += EstateCovenantReplyEventhandler;
                        Client.Estate.RequestCovenant();
                        if (!EstateCovenantReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Estate.EstateCovenantReply -= EstateCovenantReplyEventhandler;
                            throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_ESTATE_COVENANT);
                        }
                        Client.Estate.EstateCovenantReply -= EstateCovenantReplyEventhandler;
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