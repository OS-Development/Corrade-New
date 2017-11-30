///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CookComputing.XmlRpc;
using CorradeConfigurationSharp;
using wasSharp;
using wasStitchNET;
using wasStitchNET.XmlRpc.Methods;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> stitch =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    if (Environment.UserInteractive)
                        throw new Command.ScriptException(Enumerations.ScriptError.ONLY_AVAILABLE_AS_SERVICE);

                    if (Environment.UserInteractive)
                        throw new Command.ScriptException(Enumerations.ScriptError.ONLY_AVAILABLE_AS_SERVICE);

                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.STITCH:
                            // The default service name defaults to the name of this Corrade instance.
                            var service = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SERVICE)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(service))
                                service = InstalledServiceName;

                            // Server defaults to official Stitch server.
                            var server = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SERVER)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(server))
                                server = STITCH_CONSTANTS.OFFICIAL_UPDATE_SERVER;

                            // Version defaults to latest release.
                            var version = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.VERSION)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(version))
                                version = STITCH_CONSTANTS.LATEST_RELEASE_PATH;

                            // The base Stitch server URL.
                            var url = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.URL)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(url))
                                url = corradeConfiguration.StitchURL;

                            // Local path to Corrade folder.
                            var path = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                                path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                            bool patch, clean, force, verify, dry, geolocation;
                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATCH)),
                                    corradeCommandParameters.Message)), out patch))
                                patch = true;

                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CLEAN)),
                                    corradeCommandParameters.Message)), out clean))
                                clean = false;

                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORCE)),
                                    corradeCommandParameters.Message)), out force))
                                force = false;

                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.VERIFY)),
                                    corradeCommandParameters.Message)), out verify))
                                verify = true;

                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DRY)),
                                    corradeCommandParameters.Message)), out dry))
                                dry = false;

                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.GEOLOCATION)),
                                    corradeCommandParameters.Message)), out geolocation))
                                geolocation = true;

                            var proxy = XmlRpcProxyGen.Create<IXmlRpcStitchProxy>();
                            proxy.Url = string.Join(@"/", url, @"Stitch");
                            proxy.Stitch(service, server, version,
                                path,
                                new XmlRpcStitchOptions
                                {
                                    NoPatch = !patch,
                                    Clean = clean,
                                    Force = force,
                                    NoVerify = !verify,
                                    DryRun = dry,
                                    NoGeoLocation = !geolocation
                                });
                            break;
                    }
                };
        }
    }
}