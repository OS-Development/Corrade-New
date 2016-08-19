///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> listcommands =
                (corradeCommandParameters, result) =>
                {
                    var data = new HashSet<string>();
                    var LockObject = new object();
                    Reflection.GetEnumNames<ScriptKeys>().AsParallel().ForAll(o =>
                    {
                        var scriptKey = Reflection.GetEnumValueFromName<ScriptKeys>(o);
                        if (scriptKey.Equals(default(ScriptKeys)))
                            return;
                        var commandPermissionMaskAttribute =
                            Reflection.GetAttributeFromEnumValue<CommandPermissionMaskAttribute>(scriptKey);
                        if (commandPermissionMaskAttribute == null) return;
                        if (!corradeCommandParameters.Group.Equals(default(Configuration.Group)) &&
                            corradeCommandParameters.Group.PermissionMask.IsMaskFlagSet(
                                commandPermissionMaskAttribute.PermissionMask))
                        {
                            lock (LockObject)
                            {
                                data.Add(o);
                            }
                        }
                    });
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), CSV.FromEnumerable(data));
                    }
                };
        }
    }
}