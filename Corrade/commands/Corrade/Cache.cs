///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> cache =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                            corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.PURGE:
                            CSV.ToEnumerable(wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message))).Where(o => !string.IsNullOrEmpty(o))
                                .Distinct()
                                .AsParallel().ForAll(o =>
                                {
                                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(o))
                                    {
                                        case Enumerations.Entity.NUCLEUS:
                                            NucleusHTTPServer.PurgeNucleus();
                                            break;

                                        case Enumerations.Entity.ASSET:
                                            Client.Assets.Cache.BeginPrune();
                                            break;

                                        case Enumerations.Entity.CORRADE:
                                            Cache.Purge();
                                            break;
                                    }
                                });
                            break;

                        case Enumerations.Action.SAVE:
                            SaveCorradeCache.Invoke();
                            break;

                        case Enumerations.Action.LOAD:
                            LoadCorradeCache.Invoke();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}