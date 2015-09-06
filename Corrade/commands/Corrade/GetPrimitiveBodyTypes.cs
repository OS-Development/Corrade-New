using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getprimitivebodytypes =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name,
                            (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    CORRADE_CONSTANTS.PRIMTIVE_BODIES primitiveBodies = new CORRADE_CONSTANTS.PRIMTIVE_BODIES();
                    List<string> data = new List<string>(typeof (AssetType).GetFields(BindingFlags.Public |
                                                                                      BindingFlags.Static)
                        .AsParallel().Select(
                            o =>
                                wasGetStructureMemberDescription(primitiveBodies, o)));
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}