///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using Facebook;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> facebook =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    /*
                    * Create a new application (canvas is the way to go) and then generate
                    * an user access token: https://developers.facebook.com/tools/explorer
                    * using the Graph API explorer whilst granting appropriate permissions.
                    */
                    string accessToken = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TOKEN)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new ScriptException(ScriptError.NO_ACCESS_TOKEN_PROVIDED);
                    }

                    FacebookClient client = new FacebookClient(accessToken);

                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.POST:
                            string pageID = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ID)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(pageID))
                            {
                                pageID = "/me";
                            }
                            client.Post(pageID + "/feed", new Dictionary<string, object>
                            {
                                {
                                    "message", wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                            corradeCommandParameters.Message))
                                },
                                {
                                    "name", wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                                            corradeCommandParameters.Message))
                                },
                                {
                                    "link", wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.URL)),
                                            corradeCommandParameters.Message))
                                },
                                {
                                    "description", wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message))
                                }
                            });
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}