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
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getregionterrainheights =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<float> data;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        data = new List<float>
                        {
                            Client.Network.CurrentSim.TerrainStartHeight00, // Low SW
                            Client.Network.CurrentSim.TerrainHeightRange00, // High SW
                            Client.Network.CurrentSim.TerrainStartHeight01, // Low NW
                            Client.Network.CurrentSim.TerrainHeightRange01, // High NW
                            Client.Network.CurrentSim.TerrainStartHeight10, // Low SE
                            Client.Network.CurrentSim.TerrainHeightRange10, // High SE
                            Client.Network.CurrentSim.TerrainStartHeight11, // Low NE
                            Client.Network.CurrentSim.TerrainHeightRange11 // High NE
                        };
                    }
                    result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                        CSV.FromEnumerable(data.Select(o => o.ToString(Utils.EnUsCulture))));
                };
        }
    }
}