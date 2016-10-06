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
                getestatecovenant =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var EstateCovenantReceivedEvent = new ManualResetEvent(false);
                        var csv = new List<string>();
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
                        lock (Locks.ClientInstanceEstateLock)
                        {
                            Client.Estate.EstateCovenantReply += EstateCovenantReplyEventhandler;
                            Client.Estate.RequestCovenant();
                            if (!EstateCovenantReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                Client.Estate.EstateCovenantReply -= EstateCovenantReplyEventhandler;
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_COVENANT);
                            }
                            Client.Estate.EstateCovenantReply -= EstateCovenantReplyEventhandler;
                        }
                        if (csv.Any())
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                        }
                    };
        }
    }
}