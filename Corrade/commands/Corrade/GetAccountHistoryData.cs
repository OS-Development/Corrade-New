///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Corrade.Source.WebForms.SecondLife;
using CorradeConfigurationSharp;
using HtmlAgilityPack;
using ServiceStack.Text;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getaccounthistorydata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                        var firstname = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                corradeCommandParameters.Message));

                        var lastname = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                corradeCommandParameters.Message));

                        if (string.IsNullOrEmpty(firstname) && string.IsNullOrEmpty(lastname))
                        {
                            firstname = Client.Self.FirstName;
                            lastname = Client.Self.LastName;
                        }

                        var secret = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SECRET)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(secret))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SECRET_PROVIDED);

                        DateTime date;
                        if (!DateTime.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATE)),
                                corradeCommandParameters.Message)), out date))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DATE);

                        var postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].POST(
                            "https://id.secondlife.com/openid/loginsubmit",
                            new Dictionary<string, string>
                            {
                                {"username", $"{firstname} {lastname}"},
                                {"password", secret},
                                {"language", "en_US"},
                                {"previous_language", "en_US"},
                                {"from_amazon", "False"},
                                {"stay_logged_in", "True"},
                                {"show_join", "False"},
                                {"return_to", "https://secondlife.com/auth/oid_return.php"}
                            });

                        if (postData.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AUTHENTICATE);

                        var doc = new HtmlDocument();
                        HtmlNode.ElementsFlags.Remove("form");
                        doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                        var openIDNodes =
                            doc.DocumentNode.SelectNodes("//form[@id='openid_message']/input[@type='hidden']");
                        if (openIDNodes == null || !openIDNodes.Any())
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AUTHENTICATE);

                        var openID =
                            openIDNodes.AsParallel()
                                .Where(
                                    o =>
                                        o.Attributes.Contains("name") && o.Attributes["name"].Value != null &&
                                        o.Attributes.Contains("value") && o.Attributes["value"].Value != null)
                                .ToDictionary(o => o.Attributes["name"].Value,
                                    o => o.Attributes["value"].Value);

                        if (!openID.Any())
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AUTHENTICATE);

                        postData =
                            GroupHTTPClients[corradeCommandParameters.Group.UUID].POST(
                                "https://id.secondlife.com/openid/openidserver", openID);

                        if (postData.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AUTHENTICATE);

                        postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                            "https://accounts.secondlife.com/get_account_history_file",
                            new Dictionary<string, string>
                            {
                                {"month", date.ToString("yyyy-MM")},
                                {"csv", "1"},
                                {"lang", "enUS"}
                            });

                        if (postData.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_HISTORY_FOUND);

                        List<Statement> statement;
                        try
                        {
                            statement =
                                CsvSerializer.DeserializeFromString<List<Statement>>(
                                    Encoding.UTF8.GetString(postData.Result));
                        }
                        catch (Exception)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_PROCESS_DATA);
                        }

                        var data =
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message));
                        var csv =
                            new List<string>(
                                statement.SelectMany(o => wasOpenMetaverse.Reflection.GetStructuredData(o, data)));
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}