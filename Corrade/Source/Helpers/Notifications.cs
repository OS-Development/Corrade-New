///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BayesSharp;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using LanguageDetection;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade.Helpers
{
    public static class Notifications
    {
        public static SerializedNotification
            LoadSerializedNotificationParameters(
            Configuration.Notifications type)
        {
            var path = Path.Combine(CORRADE_CONSTANTS.TEMPLATES_DIRECTORY,
                CORRADE_CONSTANTS.NOTIFICATIONS_TEMPLATE_DIRECTORY,
                Reflection.GetNameFromEnumValue(type) + ".xml");

            if (!File.Exists(path))
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.COULD_NOT_FIND_NOTIFICATION_FILE));
                return null;
            }

            // Deserialize the notification data.
            SerializedNotification notification;
            try
            {
                notification =
                    SerializedNotification.LoadFromFile(path);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_DESERIALIZE_NOTIFICATION_DATA), ex.Message);
                return null;
            }

            if (!notification.Notification.Equals(type))
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.PARAMETERS_FOR_REQUESTED_EVENT_NOT_FOUND));
                return null;
            }

            return notification;
        }

        public static void ProcessParameters(this SerializedNotification.Parameter o, GridClient Client,
            Configuration corradeConfiguration, string type, List<object> args,
            Dictionary<string, string> store, object sync, LanguageDetector languageDetector,
            BayesSimpleTextClassifier bayesSimpleClassifier)
        {
            object value;
            switch (o.Value != null)
            {
                case false:
                    var arg = o.Type != null
                        ? args.AsParallel()
                            .FirstOrDefault(
                                a =>
                                    Strings.StringEquals(a.GetType().FullName, o.Type) ||
                                    a.GetType()
                                        .GetBaseTypes()
                                        .AsParallel()
                                        .Any(t => Strings.StringEquals(t.FullName, o.Type)))
                        : args.AsParallel()
                            .FirstOrDefault(
                                a =>
                                    Strings.StringEquals(a.GetType().FullName, type) ||
                                    a.GetType()
                                        .GetBaseTypes()
                                        .AsParallel()
                                        .Any(t => Strings.StringEquals(t.FullName, o.Type)));

                    if (arg == null)
                        return;

                    // Process all conditions and return if they all fail.
                    if (o.Condition != null)
                    {
                        if (
                            o.Condition.AsParallel()
                                .Select(condition => new {condition, conditional = arg.GetFP(condition.Path)})
                                .Where(t => t.conditional != null && !t.conditional.Equals(t.condition.Value))
                                .Select(t => t.condition).Any())
                            return;
                    }

                    value = arg.GetFP(o.Path);

                    if (o.Processing != null)
                    {
                        foreach (var process in o.Processing)
                        {
                            if (process.ToLower != null)
                            {
                                value = process.ToLower.Culture != null
                                    ? value.ToString().ToLower(CultureInfo.GetCultureInfo(process.ToLower.Culture))
                                    : value.ToString().ToLower(CultureInfo.InvariantCulture);

                                continue;
                            }

                            if (process.GetValue != null)
                            {
                                IDictionary iDict = null;
                                var dict = arg.GetFP(process.GetValue.Path);
                                var internalDictionaryInfo = dict.GetType()
                                    .GetField("Dictionary",
                                        BindingFlags.Default | BindingFlags.CreateInstance | BindingFlags.Instance |
                                        BindingFlags.NonPublic);

                                if (dict is IDictionary)
                                {
                                    iDict = dict as IDictionary;
                                    goto PROCESS;
                                }

                                if (internalDictionaryInfo != null)
                                {
                                    iDict = internalDictionaryInfo.GetValue(dict) as IDictionary;
                                }

                                PROCESS:
                                if (iDict != null)
                                {
                                    var look = arg.GetFP(process.GetValue.Value);

                                    if (!iDict.Contains(look))
                                        continue;

                                    value = process.GetValue.Get != null
                                        ? iDict[look].GetFP(process.GetValue.Get)
                                        : iDict[look];
                                }
                                continue;
                            }

                            if (process.ConditionalSubstitution != null)
                            {
                                dynamic l = null;
                                dynamic r = null;
                                if (process.ConditionalSubstitution.Type != null)
                                {
                                    arg =
                                        args.AsParallel()
                                            .FirstOrDefault(
                                                a =>
                                                    Strings.StringEquals(a.GetType().FullName,
                                                        process.ConditionalSubstitution.Type));
                                    if (arg != null)
                                    {
                                        l = arg.GetFP(process.ConditionalSubstitution.Path);
                                        r = process.ConditionalSubstitution.Check;
                                    }
                                }

                                if (l == null || r == null)
                                    continue;

                                if (l == r)
                                {
                                    value = process.ConditionalSubstitution.Value;
                                    break;
                                }

                                continue;
                            }

                            if (process.TernarySubstitution != null)
                            {
                                dynamic l = null;
                                dynamic r = null;
                                if (process.TernarySubstitution.Type != null)
                                {
                                    arg =
                                        args.AsParallel()
                                            .FirstOrDefault(
                                                a =>
                                                    Strings.StringEquals(a.GetType().FullName,
                                                        process.TernarySubstitution.Type));
                                    if (arg != null)
                                    {
                                        l = arg.GetFP(process.TernarySubstitution.Path);
                                        r = process.TernarySubstitution.Value;
                                    }
                                }

                                if (l == null || r == null)
                                    continue;

                                value = l == r
                                    ? process.TernarySubstitution.Left
                                    : process.TernarySubstitution.Right;

                                continue;
                            }

                            if (process.Resolve != null)
                            {
                                switch (process.Resolve.ResolveType)
                                {
                                    case SerializedNotification.ResolveType.AGENT:
                                        switch (process.Resolve.ResolveDestination)
                                        {
                                            case SerializedNotification.ResolveDestination.UUID:
                                                var name =
                                                    wasOpenMetaverse.Helpers.GetAvatarNames(value as string);
                                                if (name == null) break;
                                                var fullName = new List<string>(name);
                                                if (!fullName.Count.Equals(2)) break;
                                                var agentUUID = UUID.Zero;
                                                if (
                                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                                        corradeConfiguration.ServicesTimeout,
                                                        corradeConfiguration.DataTimeout,
                                                        new DecayingAlarm(corradeConfiguration.DataDecayType),
                                                        ref agentUUID))
                                                    break;
                                                value = agentUUID;
                                                break;
                                        }
                                        break;
                                }
                                continue;
                            }

                            if (process.ToEnumMemberName != null && process.ToEnumMemberName.Type != null &&
                                process.ToEnumMemberName.Assembly != null)
                            {
                                value =
                                    Enum.GetName(
                                        Assembly.Load(process.ToEnumMemberName.Assembly)
                                            .GetType(process.ToEnumMemberName.Type), value);
                                continue;
                            }

                            if (process.NameSplit != null)
                            {
                                if (process.NameSplit.Condition != null)
                                {
                                    var nameSplitCondition =
                                        arg.GetFP(process.NameSplit.Condition.Path);
                                    if (!nameSplitCondition.Equals(process.NameSplit.Condition.Value))
                                        return;
                                }
                                var name = wasOpenMetaverse.Helpers.GetAvatarNames(value as string);
                                if (name != null)
                                {
                                    var fullName = new List<string>(name);
                                    if (fullName.Count.Equals(2))
                                    {
                                        lock (sync)
                                        {
                                            store.Add(process.NameSplit.First,
                                                fullName.First());
                                            store.Add(process.NameSplit.Last,
                                                fullName.Last());
                                        }
                                    }
                                }
                                return;
                            }

                            if (process.IdentifyLanguage != null)
                            {
                                var detectedLanguage = languageDetector.Detect(value as string);
                                if (detectedLanguage != null)
                                {
                                    lock (sync)
                                    {
                                        store.Add(process.IdentifyLanguage.Name, detectedLanguage);
                                    }
                                }
                                continue;
                            }

                            if (process.BayesClassify != null)
                            {
                                var bayesClassification =
                                    bayesSimpleClassifier.Classify(value as string).FirstOrDefault();
                                if (!bayesClassification.Equals(default(KeyValuePair<string, double>)))
                                {
                                    lock (sync)
                                    {
                                        store.Add(process.BayesClassify.Name, CSV.FromKeyValue(bayesClassification));
                                    }
                                }
                                continue;
                            }

                            if (process.Method != null)
                            {
                                Type methodType;
                                switch (process.Method.Assembly != null)
                                {
                                    case true:
                                        methodType = Assembly.Load(process.Method.Assembly).GetType(process.Method.Type);
                                        break;
                                    default:
                                        methodType = Type.GetType(process.Method.Type);
                                        break;
                                }
                                object instance;
                                try
                                {
                                    instance = Activator.CreateInstance(methodType);
                                }
                                catch (Exception)
                                {
                                    instance = null;
                                }
                                switch (process.Method.Parameters != null)
                                {
                                    case true:
                                        value = methodType.GetMethod(process.Method.Name,
                                            process.Method.Parameters.Values.Select(Type.GetType).ToArray())
                                            .Invoke(instance,
                                                process.Method.Parameters.Keys.Select(arg.GetFP).ToArray());
                                        break;
                                    default:
                                        value =
                                            methodType.GetMethod(process.Method.Name)
                                                .Invoke(
                                                    Activator.CreateInstance(methodType).GetFP(process.Method.Path),
                                                    null);
                                        break;
                                }
                                break;
                            }
                        }
                    }
                    break;
                default:
                    if (!args.AsParallel().Any(a => Strings.StringEquals(a.GetType().FullName, type)))
                        return;
                    value = o.Value;
                    break;
            }

            var data = new HashSet<string>(wasOpenMetaverse.Reflection.wasSerializeObject(value));
            if (!data.Any()) return;

            var output = CSV.FromEnumerable(data);
            if (data.Count.Equals(1))
                output = data.First().Trim('"');

            lock (sync)
            {
                store.Add(o.Name, output);
            }
        }
    }
}