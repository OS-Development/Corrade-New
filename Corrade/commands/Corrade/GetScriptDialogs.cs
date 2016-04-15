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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getscriptdialogs =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    object LockObject = new object();
                    lock (ScriptDialogLock)
                    {
                        Parallel.ForEach(ScriptDialogs, o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(new[] {Reflection.GetStructureMemberName(o, o.Message), o.Message});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.FirstName), o.Agent.FirstName});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.LastName), o.Agent.LastName});
                                csv.AddRange(new[]
                                {Reflection.GetStructureMemberName(o.Agent, o.Agent.UUID), o.Agent.UUID.ToString()});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(o, o.Channel),
                                    o.Channel.ToString(Utils.EnUsCulture)
                                });
                                csv.AddRange(new[] {Reflection.GetStructureMemberName(o, o.Name), o.Name});
                                csv.AddRange(new[] {Reflection.GetStructureMemberName(o, o.Item), o.Item.ToString()});
                                csv.Add(Reflection.GetStructureMemberName(o, o.Button));
                                csv.AddRange(o.Button.ToArray());
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