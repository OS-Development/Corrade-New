///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CorradeConfigurationSharp;
using HtmlAgilityPack;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> divorce =
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

                    #region Authenticate

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

                    #endregion Authenticate

                    // Check whether a proposal has been sent.
                    postData =
                        GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                            "https://secondlife.com/my/account/partners.php",
                            new Dictionary<string, string>
                            {
                                {"lang", "en-US"}
                            });

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_REACH_PARTNERSHIP_PAGE);

                    // Check for proposal errors (ie user already has a partner request, etc...).
                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    HtmlNode.ElementsFlags.Remove("option");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                    // Get the divorce form.
                    var formNode = doc.DocumentNode.SelectSingleNode("//form[@action='/my/account/partners.php']");
                    if (formNode == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PARTNER_FOUND);

                    // Build the new divorce request form.
                    var newDivorce = new Dictionary<string, string>();

                    // Get the token.
                    var tokenNode = formNode.SelectSingleNode("//input[@name='CSRFToken']");
                    newDivorce.Add(tokenNode.Attributes["name"].Value, tokenNode.Attributes["value"].Value);
                    // Get the partner.
                    var partnerNode = formNode.SelectSingleNode("//input[@name='form[partner_id]']");
                    newDivorce.Add(partnerNode.Attributes["name"].Value, partnerNode.Attributes["value"].Value);
                    // Select the button.
                    var dissolveNode = formNode.SelectSingleNode("//input[@id='partner-dissolve']");
                    newDivorce.Add(dissolveNode.Attributes["name"].Value, dissolveNode.Attributes["value"].Value);
                    newDivorce.Add("Submit", "Submit");

                    // Send the form.
                    postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].POST(
                        "https://secondlife.com/my/account/partners.php", newDivorce);

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_POST_DIVORCE);

                    // Check for proposal errors (ie user already has a partner request, etc...).
                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    HtmlNode.ElementsFlags.Remove("option");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var errorNodes = doc.DocumentNode.SelectNodes("//div[@class='error']/ul/li");
                    if (errorNodes != null && errorNodes.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(errorNodes.Select(o => o.InnerText.Trim())));
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_DIVORCE);
                    }
                };
        }
    }
}