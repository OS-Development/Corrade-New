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
            public static Action<Group, string, Dictionary<string, string>> getscriptpermissionrequests =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    object LockObject = new object();
                    lock (ScriptPermissionRequestLock)
                    {
                        Parallel.ForEach(ScriptPermissionRequests, o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Name), o.Name});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.FirstName), o.Agent.FirstName});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.LastName), o.Agent.LastName});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(o.Agent, o.Agent.UUID), o.Agent.UUID.ToString()});
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Item), o.Item.ToString()});
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Task), o.Task.ToString()});
                                csv.Add(wasGetStructureMemberDescription(o, o.Permission));
                                csv.AddRange(typeof (ScriptPermission).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                                    .AsParallel().Where(
                                        p =>
                                            !(((int) p.GetValue(null) &
                                               (int) o.Permission)).Equals(0))
                                    .Select(p => p.Name).ToArray());
                                csv.AddRange(new[] {wasGetStructureMemberDescription(o, o.Region), o.Region});
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