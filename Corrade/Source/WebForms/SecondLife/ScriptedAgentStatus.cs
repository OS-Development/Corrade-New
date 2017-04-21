///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2017 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using wasSharp.Web;
using static Corrade.Enumerations;

namespace Corrade.Source.WebForms.SecondLife
{
    public class ScriptedAgentStatus : IDisposable
    {
        private static wasHTTPClient HTTPClient;

        public ScriptedAgentStatus()
        {
            HTTPClient = new wasHTTPClient
            (CORRADE_CONSTANTS.USER_AGENT, new CookieContainer(), CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED,
                Corrade.corradeConfiguration.ServicesTimeout);
            Login();
        }

        public void SetScriptedAgentStatus(bool scripted)
        {
            // Retrieve the agent status form.
            var postData = HTTPClient.GET(
                "https://secondlife.com/my/account/sisa.php");

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.UNABLE_TO_REACH_SCRIPTED_AGENT_STATUS_PAGE);

            var doc = new HtmlDocument();
            HtmlNode.ElementsFlags.Remove("form");
            HtmlNode.ElementsFlags.Remove("option");
            doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
            var formNode = doc.DocumentNode.SelectSingleNode("//form[@action='/my/account/sisa.php']");
            if (formNode == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.UNABLE_TO_REACH_SCRIPTED_AGENT_STATUS_PAGE);

            // Build the new scripted agent status form.
            var setBotStatus = new Dictionary<string, string>();

            // Get the token.
            var tokenNode = formNode.SelectSingleNode("//input[@name='CSRFToken']");
            setBotStatus.Add(tokenNode.Attributes["name"].Value, tokenNode.Attributes["value"].Value);

            // Set the status.
            //var botNode = formNode.SelectSingleNode("//input[@name='isbot' and @value'yes']");
            //botNode.SetAttributeValue()
            setBotStatus.Add("isbot", scripted ? @"yes" : @"no");

            // Send the form.
            postData = HTTPClient.POST(
                "https://secondlife.com/my/account/sisa.php", setBotStatus);

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.COULD_NOT_SET_SCRIPTED_AGENT_STATUS);
        }

        public bool IsScriptedAgent()
        {
            // Retrieve the agent status.
            var postData = HTTPClient.GET(
                "https://secondlife.com/my/account/sisa.php");

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.UNABLE_TO_REACH_SCRIPTED_AGENT_STATUS_PAGE);

            var doc = new HtmlDocument();
            HtmlNode.ElementsFlags.Remove("form");
            HtmlNode.ElementsFlags.Remove("option");
            doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));
            var formNode = doc.DocumentNode.SelectSingleNode("//form[@action='/my/account/sisa.php']");
            if (formNode == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.UNABLE_TO_REACH_SCRIPTED_AGENT_STATUS_PAGE);

            var botStatusNode = formNode.SelectNodes("//input[@name='isbot']")
                .Where(o => o.Attributes.Contains("value") && !string.IsNullOrEmpty(o.Attributes["value"].Value))
                .FirstOrDefault(o => o.Attributes.Contains("checked"));

            if (botStatusNode == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.COULD_NOT_GET_SCRIPTED_AGENT_STATUS);

            return botStatusNode.Attributes["value"].Value.Equals("Yes");
        }

        private void Logout()
        {
            // Logout.
            var postData = HTTPClient.GET(
                "https://secondlife.com/my/account/logout.php");

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.LOGOUT_FAILED);
        }

        private void Login()
        {
            var postData = HTTPClient.POST(
                "https://id.secondlife.com/openid/loginsubmit",
                new Dictionary<string, string>
                {
                            {"username", $"{Corrade.corradeConfiguration.FirstName} {Corrade.corradeConfiguration.LastName}"},
                            {"password", Corrade.corradeConfiguration.Password},
                            {"language", "en_US"},
                            {"previous_language", "en_US"},
                            {"from_amazon", "False"},
                            {"stay_logged_in", "True"},
                            {"show_join", "False"},
                            {"return_to", "https://secondlife.com/auth/oid_return.php"}
                });

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.LOGIN_FAILED);

            var doc = new HtmlDocument();
            HtmlNode.ElementsFlags.Remove("form");
            doc.LoadHtml(Encoding.UTF8.GetString(postData.Result));

            var openIDNodes = doc.DocumentNode.SelectNodes("//form[@id='openid_message']/input[@type='hidden']");
            if (openIDNodes == null || !openIDNodes.Any())
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.LOGIN_FAILED);

            var openID =
                openIDNodes.AsParallel()
                    .Where(
                        o =>
                            o.Attributes.Contains("name") && o.Attributes["name"].Value != null &&
                            o.Attributes.Contains("value") && o.Attributes["value"].Value != null)
                    .ToDictionary(o => o.Attributes["name"].Value,
                        o => o.Attributes["value"].Value);

            if (!openID.Any())
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.LOGIN_FAILED);

            postData =
                HTTPClient.POST(
                    "https://id.secondlife.com/openid/openidserver", openID);

            if (postData.Result == null)
                throw new ScriptedAgentStatusException(ScriptedAgentStatusError.LOGIN_FAILED);
        }

        public void Dispose()
        {
            if (HTTPClient != null)
            {
                Logout();
            }
            HTTPClient?.Dispose();
            HTTPClient = null;
        }
    }
}
