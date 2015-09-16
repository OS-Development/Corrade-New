///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getscriptdialogs =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Interact))
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
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Message), o.Message});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.FirstName), o.Agent.FirstName});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.LastName), o.Agent.LastName});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.UUID), o.Agent.UUID.ToString()});
                                csv.AddRange(new[]
                                {
                                    wasGetStructureMemberDescription(o, o.Channel),
                                    o.Channel.ToString(CultureInfo.InvariantCulture)
                                });
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Name), o.Name});
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Item), o.Item.ToString()});
                                csv.Add(wasGetStructureMemberDescription(o, o.Button));
                                csv.AddRange(o.Button.ToArray());
                            }
                        });
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}