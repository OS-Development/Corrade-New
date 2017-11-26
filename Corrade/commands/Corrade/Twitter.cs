///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net;
using Corrade.Constants;
using CorradeConfigurationSharp;
using TweetSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> twitter =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    /*
                    * The first step to accessing the Twitter API is to create an application at
                    * (http://dev.twitter.com). When that process is complete, your application
                    * is issued a Consumer Key and Consumer Secret. These tokens are responsible
                    * for identifying your application when it is in use by your customers.
                    * Additionally the access tokens will have to be generated in order to be
                    * used with this command.
                    */
                    var consumerKey = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.KEY)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(consumerKey))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CONSUMER_KEY_PROVIDED);

                    var consumerSecret = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SECRET)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(consumerSecret))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CONSUMER_SECRET_PROVIDED);

                    var accessToken = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TOKEN)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(accessToken))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ACCESS_TOKEN_PROVIDED);

                    var accessTokenSecret = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACCESS)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(accessTokenSecret))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ACCESS_TOKEN_SECRET_PROVIDED);

                    var service = new TwitterService(consumerKey, consumerSecret);
                    service.AuthenticateWith(accessToken, accessTokenSecret);

                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.TWEET:
                            var message = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(message))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_MESSAGE_PROVIDED);
                            if (message.Length > CORRADE_CONSTANTS.TWITTER_MAXIMUM_TWEET_LENGTH)
                                throw new Command.ScriptException(Enumerations.ScriptError.MESSAGE_TOO_LONG);
                            service.SendTweet(new SendTweetOptions {Status = message},
                                (tweet, response) =>
                                {
                                    if (!response.StatusCode.Equals(HttpStatusCode.OK))
                                        throw new Command.ScriptException(Enumerations.ScriptError
                                            .COULD_NOT_POST_TWEET);
                                });
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}