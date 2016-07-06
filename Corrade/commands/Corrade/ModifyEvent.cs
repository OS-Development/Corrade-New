///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using CorradeConfiguration;
using HtmlAgilityPack;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> modifyevent =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    var firstname = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                            corradeCommandParameters.Message));

                    var lastname = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                            corradeCommandParameters.Message));

                    if (string.IsNullOrEmpty(firstname) && string.IsNullOrEmpty(lastname))
                    {
                        firstname = Client.Self.FirstName;
                        lastname = Client.Self.LastName;
                    }

                    var secret = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SECRET)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(secret))
                        throw new ScriptException(ScriptError.NO_SECRET_PROVIDED);

                    uint id;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ID)),
                            corradeCommandParameters.Message)), out id))
                        throw new ScriptException(ScriptError.NO_EVENT_IDENTIFIER_PROVIDED);

                    #region Event Parameters

                    var name = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                        corradeCommandParameters.Message));
                    if (!string.IsNullOrEmpty(name) && Helpers.IsSecondLife(Client))
                    {
                        // Check for description HTML.
                        var nameInput = new HtmlDocument();
                        nameInput.LoadHtml(name);
                        if (!nameInput.DocumentNode.InnerText.Equals(name))
                            throw new ScriptException(ScriptError.NAME_MAY_NOT_CONTAIN_HTML);
                    }

                    var description = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                        corradeCommandParameters.Message));
                    // Sanitize input.
                    if (!string.IsNullOrEmpty(description) && Helpers.IsSecondLife(Client))
                    {
                        // Check for description length.
                        if (description.Length > Constants.EVENTS.MAXIMUM_EVENT_DESCRIPTION_LENGTH)
                            throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_EVENT_DESCRIPTION);
                        // Check for description HTML.
                        var descriptionInput = new HtmlDocument();
                        descriptionInput.LoadHtml(description);
                        if (!descriptionInput.DocumentNode.InnerText.Equals(description))
                            throw new ScriptException(ScriptError.DESCRIPTION_MAY_NOT_CONTAIN_HTML);
                    }

                    var date = new DateTime();
                    if (!DateTime.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATE)),
                        corradeCommandParameters.Message)), out date))
                        date = new DateTime();

                    var time = new DateTime();
                    if (!DateTime.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TIME)),
                        corradeCommandParameters.Message)), out time))
                        time = new DateTime();

                    int duration;
                    if (!int.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION)),
                        corradeCommandParameters.Message)), out duration))
                        duration = -1;

                    var location = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LOCATION)),
                            corradeCommandParameters.Message));

                    int category;
                    if (!int.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.CATEGORY)),
                            corradeCommandParameters.Message)), out category))
                        category = -1;

                    int amount;
                    if (!int.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AMOUNT)),
                            corradeCommandParameters.Message)), out amount))
                        amount = -1;

                    #endregion

                    var cookieContainer = new CookieContainer();

                    var postData = Web.wasPOST(CORRADE_CONSTANTS.USER_AGENT,
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
                        }, CorradePOSTMediaType, cookieContainer, corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AUTHENTICATE);

                    var doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                    var openIDNodes = doc.DocumentNode.SelectNodes("//form[@id='openid_message']/input[@type='hidden']");
                    if (openIDNodes == null || !openIDNodes.Any())
                        throw new ScriptException(ScriptError.UNABLE_TO_AUTHENTICATE);

                    var openID =
                        openIDNodes.AsParallel()
                            .Where(
                                o =>
                                    o.Attributes.Contains("name") && o.Attributes["name"].Value != null &&
                                    o.Attributes.Contains("value") && o.Attributes["value"].Value != null)
                            .ToDictionary(o => o.Attributes["name"].Value,
                                o => o.Attributes["value"].Value);

                    if (!openID.Any())
                        throw new ScriptException(ScriptError.UNABLE_TO_AUTHENTICATE);

                    postData = Web.wasPOST(CORRADE_CONSTANTS.USER_AGENT, "https://id.secondlife.com/openid/openidserver",
                        openID, CorradePOSTMediaType,
                        cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AUTHENTICATE);

                    // Events
                    postData = Web.wasGET(CORRADE_CONSTANTS.USER_AGENT,
                        "https://secondlife.com/my/community/events/tos.php",
                        new Dictionary<string, string>(),
                        cookieContainer, corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var ToSNodes = doc.DocumentNode.SelectNodes("//form[@action='tos.php']/input[@type='hidden']");
                    if (ToSNodes == null || !ToSNodes.Any())
                        throw new ScriptException(ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    var eventToS =
                        ToSNodes
                            .ToDictionary(input => input.Attributes["name"].Value,
                                input => input.Attributes["value"].Value);
                    eventToS.Add("action", "I Agree");

                    postData = Web.wasPOST(CORRADE_CONSTANTS.USER_AGENT,
                        "https://secondlife.com/my/community/events/tos.php", eventToS,
                        CorradePOSTMediaType, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    postData = Web.wasGET(CORRADE_CONSTANTS.USER_AGENT,
                        "https://secondlife.com/my/community/events/edit.php",
                        new Dictionary<string, string>
                        {
                            {"id", id.ToString()},
                            {"lang", "en-US"}
                        }, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    HtmlNode.ElementsFlags.Remove("option");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='event_frm']");
                    if (formNode == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

                    if (string.IsNullOrEmpty(name))
                    {
                        var nameNode = formNode.SelectSingleNode("//input[@id='event_name']");
                        if (nameNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        name = nameNode.Attributes["value"].Value;
                    }

                    if (string.IsNullOrEmpty(description))
                    {
                        var descriptionNode = formNode.SelectSingleNode("//textarea[@id='event_desc']");
                        if (descriptionNode?.InnerText == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);

                        description = descriptionNode.InnerText;
                    }

                    if (date.Equals(default(DateTime)))
                    {
                        var dateNode = formNode.SelectSingleNode("//input[@id='event_date']");
                        if (dateNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        if (!DateTime.TryParse(dateNode.Attributes["value"].Value, out date))
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                    }

                    if (time.Equals(default(DateTime)))
                    {
                        var timeNode = formNode.SelectSingleNode("//input[@id='event_selected_time']");
                        if (timeNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        if (!DateTime.TryParse(timeNode.Attributes["value"].Value, out time))
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                    }

                    if (duration == -1)
                    {
                        var durationNode =
                            formNode.SelectSingleNode("//select[@id='duration']/option[@selected='selected']");
                        if (durationNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        if (!int.TryParse(durationNode.Attributes["value"].Value, out duration))
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                    }

                    if (string.IsNullOrEmpty(location))
                    {
                        var locationNode = formNode.SelectSingleNode("//select[@id='parcel_chosen']/option[@selected]");
                        if (locationNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);

                        location = locationNode.Attributes["value"].Value;
                    }

                    if (category == -1)
                    {
                        var categoryNode =
                            formNode.SelectSingleNode("//select[@id='category']/option[@selected='selected']");
                        if (categoryNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        if (!int.TryParse(categoryNode.Attributes["value"].Value, out category))
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                    }

                    if (amount == -1)
                    {
                        var amoutNode = formNode.SelectSingleNode("//input[@id='amount']");
                        if (amoutNode?.Attributes["value"].Value == null)
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                        if (!int.TryParse(amoutNode.Attributes["value"].Value, out amount))
                            throw new ScriptException(ScriptError.UNABLE_TO_RETRIEVE_FORM_PARAMETERS);
                    }

                    // Build the new event form data.
                    var newEvent = new Dictionary<string, string>
                    {
                        {"event_id", id.ToString()},
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
                    postData = Web.wasPOST(CORRADE_CONSTANTS.USER_AGENT,
                        "https://secondlife.com/my/community/events/edit.php",
                        newEvent, CorradePOSTMediaType, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_POST_EVENT);

                    doc = new HtmlDocument();
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                    // Check for form errors.
                    var errorNodes = doc.DocumentNode.SelectNodes("//div[@id='display_errors']/ul/li");
                    if (errorNodes != null && errorNodes.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(errorNodes.Select(o => o.InnerText.Trim())));
                        throw new ScriptException(ScriptError.EVENT_POSTING_REJECTED);
                    }
                };
        }
    }
}