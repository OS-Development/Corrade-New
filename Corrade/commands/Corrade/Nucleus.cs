///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> nucleus
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    switch (action)
                    {
                        case Enumerations.Action.START:
                            if (HttpListener.IsSupported && !NucleusHTTPServer.IsRunning)
                            {
                                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage
                                    .STARTING_NUCLEUS_SERVER));
                                try
                                {
                                    NucleusHTTPServer.Start(
                                        new List<string> {corradeConfiguration.NucleusServerPrefix});
                                }
                                catch (Exception ex)
                                {
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                        ex.Message);
                                    throw new Command.ScriptException(Enumerations.ScriptError.NUCLEUS_SERVER_ERROR);
                                }
                            }
                            break;

                        case Enumerations.Action.STOP:
                            if (HttpListener.IsSupported && NucleusHTTPServer.IsRunning)
                            {
                                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage
                                    .STARTING_NUCLEUS_SERVER));
                                try
                                {
                                    NucleusHTTPServer.Stop((int) corradeConfiguration.ServicesTimeout);
                                }
                                catch (Exception ex)
                                {
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                        ex.Message);
                                    throw new Command.ScriptException(Enumerations.ScriptError.NUCLEUS_SERVER_ERROR);
                                }
                            }
                            break;

                        case Enumerations.Action.PURGE:
                            NucleusHTTPServer.PurgeNucleus();
                            break;

                        case Enumerations.Action.SET:
                        case Enumerations.Action.GET:
                            var entity = Reflection.GetEnumValueFromName<Enumerations.Entity>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message)));
                            switch (entity)
                            {
                                case Enumerations.Entity.URL:
                                    switch (action)
                                    {
                                        case Enumerations.Action.GET:
                                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                                CSV.FromEnumerable(NucleusHTTPServer.Prefixes));
                                            break;
                                    }
                                    break;

                                case Enumerations.Entity.AUTHENTICATION:
                                    switch (action)
                                    {
                                        case Enumerations.Action.GET:
                                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                                (!NucleusHTTPServer.AuthenticationSchemes.Equals(AuthenticationSchemes
                                                    .Anonymous)).ToString());
                                            break;

                                        case Enumerations.Action.SET:
                                            NucleusHTTPServer.AuthenticationSchemes = AuthenticationSchemes.Basic;
                                            break;
                                    }
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}