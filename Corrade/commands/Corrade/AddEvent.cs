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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> addevent =
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

                    #region Event Parameters
                    var name = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);

                    var description = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(description))
                        throw new ScriptException(ScriptError.NO_DESCRIPTION_PROVIDED);

                    DateTime date;
                    if (!DateTime.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATE)),
                            corradeCommandParameters.Message)), out date))
                        throw new ScriptException(ScriptError.NO_DATE_PROVIDED);

                    DateTime time;
                    if (!DateTime.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TIME)),
                            corradeCommandParameters.Message)), out time))
                        throw new ScriptException(ScriptError.NO_TIME_PROVIDED);

                    uint duration;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION)),
                            corradeCommandParameters.Message)), out duration))
                        throw new ScriptException(ScriptError.NO_DURATION_PROVIDED);

                    var location = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LOCATION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(location))
                        throw new ScriptException(ScriptError.NO_LOCATION_PROVIDED);

                    uint category;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.CATEGORY)),
                            corradeCommandParameters.Message)), out category))
                        throw new ScriptException(ScriptError.NO_CATEGORY_PROVIDED);

                    uint amount;
                    if (!uint.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AMOUNT)),
                            corradeCommandParameters.Message)), out amount))
                        amount = 0;

                    #endregion

                    var cookieContainer = new CookieContainer();

                    var postData = wasPOST("https://id.secondlife.com/openid/loginsubmit",
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
                        }, cookieContainer, corradeConfiguration.ServicesTimeout);

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

                    postData = wasPOST("https://id.secondlife.com/openid/openidserver", openID, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AUTHENTICATE);

                    // Events
                    postData = wasGET("https://secondlife.com/my/community/events/tos.php",
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

                    postData = wasPOST("https://secondlife.com/my/community/events/tos.php", eventToS, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_AGREE_TO_TOS);

                    postData = wasGET("https://secondlife.com/my/community/events/edit.php",
                        new Dictionary<string, string>
                        {
                            { "lang", "en-US" }
                        }, cookieContainer, corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

                    doc = new HtmlDocument();
                    HtmlNode.ElementsFlags.Remove("form");
                    HtmlNode.ElementsFlags.Remove("option");
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
                    var formNode = doc.DocumentNode.SelectSingleNode("//form[@id='event_frm']");
                    if (formNode == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_REACH_EVENTS_PAGE);

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
                    postData = wasPOST("https://secondlife.com/my/community/events/edit.php", newEvent, cookieContainer,
                        corradeConfiguration.ServicesTimeout);

                    if (postData.Result == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_POST_EVENT);

                    doc = new HtmlDocument();
                    doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

                    var eventDetailsNodes = doc.DocumentNode.SelectNodes("//span[@class='edit_controls']/a");
                    if (eventDetailsNodes == null || !eventDetailsNodes.Any())
                        throw new ScriptException(ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    var eventdetailsNode = eventDetailsNodes.FirstOrDefault();
                    if (eventdetailsNode == null)
                        throw new ScriptException(ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    var idString = eventdetailsNode.Attributes["href"].Value;
                    if (string.IsNullOrEmpty(idString))
                        throw new ScriptException(ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);

                    uint id;
                    switch (uint.TryParse(idString.Split('=').Last(), out id))
                    {
                        case true:
                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                id.ToString());
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNABLE_TO_GET_EVENT_IDENTIFIER);
                    }
                };
        }
    }
}