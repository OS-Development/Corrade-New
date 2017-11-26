///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getgroupinvites
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var csv = new List<string>();
                    var LockObject = new object();
                    lock (GroupInvitesLock)
                    {
                        GroupInvites.Values.AsParallel().ForAll(o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                    {Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME), o.Agent.FirstName});
                                csv.AddRange(new[]
                                    {Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME), o.Agent.LastName});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT), o.Agent.UUID.ToString()
                                });
                                csv.AddRange(new[]
                                    {Reflection.GetNameFromEnumValue(Command.ScriptKeys.GROUP), o.Group});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION), o.Session.ToString()
                                });
                                csv.AddRange(new[]
                                {
                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.FEE),
                                    o.Fee.ToString(Utils.EnUsCulture)
                                });
                            }
                        });
                    }
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}