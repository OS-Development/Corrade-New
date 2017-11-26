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
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                batchavatarnametokey =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var csv = new List<string>();
                        var LockObject = new object();
                        CSV.ToEnumerable(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                        corradeCommandParameters.Message)))
                            .AsParallel()
                            .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                            {
                                var agentUUID = UUID.Zero;
                                var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                if (!fullName.Any() ||
                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                        corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                                    return;

                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                        {string.Join(@" ", fullName.First(), fullName.Last()), agentUUID.ToString()});
                                }
                            });
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}