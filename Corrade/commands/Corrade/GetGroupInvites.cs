///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getgroupinvites =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    object LockObject = new object();
                    lock (GroupInviteLock)
                    {
                        Parallel.ForEach(GroupInvites, o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.FirstName), o.Agent.FirstName});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.LastName), o.Agent.LastName});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.UUID), o.Agent.UUID.ToString()});
                                csv.AddRange(new[] {Reflection.GetStructureMemberName(o, o.Group), o.Group});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o, o.Session), o.Session.ToString()});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(o, o.Fee),
                                    o.Fee.ToString(Utils.EnUsCulture)
                                });
                            }
                        });
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}