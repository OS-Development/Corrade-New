///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> listcommands =
                (corradeCommandParameters, result) =>
                {
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();
                    Parallel.ForEach(Reflection.GetEnumNames<ScriptKeys>(), o =>
                    {
                        ScriptKeys scriptKey = Reflection.GetEnumValueFromName<ScriptKeys>(o);
                        IsCorradeCommandAttribute isCommandAttribute =
                            Reflection.GetAttributeFromEnumValue<IsCorradeCommandAttribute>(scriptKey);
                        if (isCommandAttribute == null || !isCommandAttribute.IsCorradeCorradeCommand)
                            return;
                        CommandPermissionMaskAttribute commandPermissionMaskAttribute =
                            Reflection.GetAttributeFromEnumValue<CommandPermissionMaskAttribute>(scriptKey);
                        if (commandPermissionMaskAttribute == null) return;
                        if (!corradeCommandParameters.Group.Equals(default(Configuration.Group)) &&
                            !(corradeCommandParameters.Group.PermissionMask &
                              commandPermissionMaskAttribute.PermissionMask).Equals(0))
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