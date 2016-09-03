///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Serialization;
using Corrade.WebForms.SecondLife;
using CorradeConfiguration;
using HtmlAgilityPack;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getaccounttransactionsdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }

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
                                corradeCommandParameters.Message)), out @from))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DATE);
                        }

                        DateTime to;
                        if (!DateTime.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                corradeCommandParameters.Message)), out to))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DATE);
                        }

                        var cookieContainer = new CookieContainer();

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
                            "https://secondlife.com/my/account/download_transactions.php",
                            new Dictionary<string, string>
                            {
                                {"date_start", @from.ToString("yyyy-MM-dd ")},
                                {"date_end", to.ToString("yyyy-MM-dd ")},
                                {"type", "xml"},
                                {"include_zero", "yes"}
                            });

                        if (postData.Result == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_TRANSACTIONS_FOUND);

                        Transactions transactions;
                        var serializer = new XmlSerializer(typeof (Transactions));
                        try
                        {
                            using (TextReader reader = new StringReader(Encoding.UTF8.GetString(postData.Result)))
                            {
                                transactions = (Transactions) serializer.Deserialize(reader);
                            }
                        }
                        catch (Exception)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_RETRIEVE_TRANSACTIONS);
                        }
                        var data =
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message));
                        var csv =
                            new List<string>(
                                transactions.list.SelectMany(o => wasOpenMetaverse.Reflection.GetStructuredData(o, data)));
                        if (csv.Any())
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                        }
                    };
        }
    }
}