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
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getmutes =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Mute))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var mutes = Enumerable.Empty<MuteEntry>();
                    // retrieve the current mute list
                    switch (Cache.MuteCache.IsVirgin)
                    {
                        case true:
                            if (!Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .COULD_NOT_RETRIEVE_MUTE_LIST);
                            break;

                        default:
                            mutes = Cache.MuteCache.OfType<MuteEntry>();
                            break;
                    }
                    var data = mutes.AsParallel().Select(o => new[]
                    {
                        o.Name,
                        o.ID.ToString(),
                        o.Flags.ToString(),
                        o.Type.ToString()
                    }).SelectMany(o => o).ToList();
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}