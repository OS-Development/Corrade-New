///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Corrade.WebForms.SecondLife;
using CorradeConfigurationSharp;
using HtmlAgilityPack;
using wasSharp;
using wasSharpNET.Serialization;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getaccounttransactionsdata =
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

                        DateTime from;
                        if (!DateTime.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                corradeCommandParameters.Message)), out from))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DATE);

                        DateTime to;
                        if (!DateTime.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                corradeCommandParameters.Message)), out to))
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
                            "https://accounts.secondlife.com/get_transaction_history_csv",
                            new Dictionary<string, string>
                            {
                                {"startDate", from.ToString("yyyy-MM-dd")},
                                {"endDate", to.ToString("yyyy-MM-dd")},
                                {"type", "xml"},
                                {"xml", "1"},
                                {"omit_zero_amounts", "false"}
                            });

                        if (postData.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_TRANSACTIONS_FOUND);

                        Transactions transactions;
                        try
                        {
                            using (TextReader reader = new StringReader(Encoding.UTF8.GetString(postData.Result)))
                            {
                                transactions = XmlSerializerCache.Deserialize<Transactions>(reader);
                            }
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
                                transactions.list.SelectMany(
                                    o => wasOpenMetaverse.Reflection.GetStructuredData(o, data)));
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}