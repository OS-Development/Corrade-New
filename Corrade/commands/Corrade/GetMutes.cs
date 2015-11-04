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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getmutes =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Mute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    IEnumerable<MuteEntry> mutes = Enumerable.Empty<MuteEntry>();
                    if (!GetMutes(corradeConfiguration.ServicesTimeout, ref mutes))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_MUTE_LIST);
                    }
                    List<string> data = mutes.ToList().AsParallel().Select(o => new[]
                    {
                        o.Name,
                        o.ID.ToString(),
                        o.Flags.ToString(),
                        o.Type.ToString()
                    }).SelectMany(o => o).ToList();
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}