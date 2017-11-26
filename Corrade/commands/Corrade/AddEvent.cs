///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CorradeConfigurationSharp;
using HtmlAgilityPack;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> addevent =
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

                    #region Event Parameters

                    var name = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    // Sanitize input.
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                    {
                        // Check for description HTML.
                        var nameInput = new HtmlDocument();
                        nameInput.LoadHtml(name);
                        if (!nameInput.DocumentNode.InnerText.Equals(name))
                            throw new Command.ScriptException(Enumerations.ScriptError.NAME_MAY_NOT_CONTAIN_HTML);
                    }

                    var description = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(description))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DESCRIPTION_PROVIDED);
                    // Sanitize input.
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                    {
                        // Check for description length.
                        if (description.Length > wasOpenMetaverse.Constants.EVENTS.MAXIMUM_EVENT_DESCRIPTION_LENGTH)
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TOO_MANY_CHARACTERS_FOR_EVENT_DESCRIPTION);
                        // Check for description HTML.
                        var descriptionInput = new HtmlDocument();
                        descriptionInput.LoadHtml(description);
                        if (!descriptionInput.DocumentNode.InnerText.Equals(description))
                            throw new Command.ScriptException(Enumerations.ScriptError
                                .DESCRIPTION_MAY_NOT_CONTAIN_HTML);
                    }

                    DateTime date;
                    if (!DateTime.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATE)),
                            corradeCommandParameters.Message)), out date))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATE_PROVIDED);

                    DateTime time;
                    if (!DateTime.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME)),
                            corradeCommandParameters.Message)), out time))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TIME_PROVIDED);

                    uint duration;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DURATION)),
                            corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture, out duration))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DURATION_PROVIDED);

                    var location = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LOCATION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(location))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LOCATION_PROVIDED);

                    uint category;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CATEGORY)),
                            corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture, out category))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CATEGORY_PROVIDED);

                    uint amount;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AMOUNT)),
                            corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture, out amount))
                        amount = 0;

                    #endregion Event Parameters

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

                    // Events
                    postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                        "https://secondlife.com/my/community/events/tos.php",
                        new Dictionary<string, string>());

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var ToSNodes = doc.DocumentNode.SelectNodes("//form[@action='tos.php']/input[@type='hidden']");
                    if (ToSNodes == null || !ToSNodes.Any())
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    var eventToS =
                        ToSNodes
                            .ToDictionary(input => input.Attributes["name"].Value,
                                input => input.Attributes["value"].Value);
                    eventToS.Add("action", "I Agree");

                    postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].POST(
                        "https://secondlife.com/my/community/events/tos.php", eventToS);

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].GET(
                        "https://secondlife.com/my/community/events/edit.php",
                        new Dictionary<string, string>
                        {
                            {"lang", "en-US"}
                        });

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    HtmlNode.ElementsFlags.Remove("option");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='event_frm']");
                    if (formNode == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

                    // Build the new event form data.
                    var newEvent = new Dictionary<string, string>
                    {
                        {"event_name", name},
                        {"event_desc", description},
                        {"event_date", date.ToString("MM/dd/yyyy")},
                        {"event_selected_time", time.ToString("HH:mm:ss")},
                        {"event_time_select", time.ToString("HH:mm:ss")},
                        {"duration", duration.ToString()},
                        {"parcel_chosen", location},
                        {"category", category.ToString()},
                        {"amount", amount.ToString()}
                    };

                    // Get the token.
                    var tokenNode = formNode.SelectSingleNode("//input[@name='CSRFToken']");
                    newEvent.Add(tokenNode.Attributes["name"].Value, tokenNode.Attributes["value"].Value);
                    newEvent.Add("action", "Save Event");

                    // Send the form.
                    postData = GroupHTTPClients[corradeCommandParameters.Group.UUID].POST(
                        "https://secondlife.com/my/community/events/edit.php", newEvent);

                    if (postData.Result == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_POST_EVENT);

                    doc = new HtmlDocument();
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                    // Check for form errors.
                    var errorNodes = doc.DocumentNode.SelectNodes("//div[@id='display_errors']/ul/li");
                    if (errorNodes != null && errorNodes.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(errorNodes.Select(o => o.InnerText.Trim())));
                        throw new Command.ScriptException(Enumerations.ScriptError.EVENT_POSTING_REJECTED);
                    }

                    var eventDetailsNodes = doc.DocumentNode.SelectNodes("//span[@class='edit_controls']/a");
                    if (eventDetailsNodes == null || !eventDetailsNodes.Any())
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    var eventdetailsNode = eventDetailsNodes.FirstOrDefault();
                    if (eventdetailsNode == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    var idString = eventdetailsNode.Attributes["href"].Value;
                    if (string.IsNullOrEmpty(idString))
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    uint id;
                    switch (uint.TryParse(idString.Split('=').Last(), NumberStyles.Integer, Utils.EnUsCulture, out id))
                    {
                        case true:
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                id.ToString());
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);
                    }
                };
        }
    }
}