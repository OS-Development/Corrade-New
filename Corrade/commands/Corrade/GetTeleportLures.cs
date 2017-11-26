///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getteleportlures
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var csv = new List<string>();
                    var LockObject = new object();
                    lock (TeleportLuresLock)
                    {
                        TeleportLures.Values.AsParallel().ForAll(o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                    {Reflection.GetStructureMemberName(o.Agent, o.Agent.FirstName), o.Agent.FirstName});
                                csv.AddRange(new[]
                                    {Reflection.GetStructureMemberName(o.Agent, o.Agent.LastName), o.Agent.LastName});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(o.Agent, o.Agent.UUID), o.Agent.UUID.ToString()
                                });
                                csv.AddRange(new[]
                                    {Reflection.GetStructureMemberName(o, o.Session), o.Session.ToString()});
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