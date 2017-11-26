///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using BayesSharp;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> bayes =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Database))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    string data;
                    string source;
                    string target;
                    string category;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.TRAIN:
                            category = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CATEGORY)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(category))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CATEGORY_PROVIDED);
                            data =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            lock (GroupBayesClassifiersLock)
                            {
                                if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                    GroupBayesClassifiers.Add(corradeCommandParameters.Group.UUID,
                                        new BayesSimpleTextClassifier());
                                GroupBayesClassifiers[corradeCommandParameters.Group.UUID].Train(category, data);
                            }
                            // We are training so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        case Enumerations.Action.CLASSIFY:
                            data =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            Dictionary<string, double> output;
                            lock (GroupBayesClassifiersLock)
                            {
                                output = GroupBayesClassifiers[corradeCommandParameters.Group.UUID].Classify(data);
                            }
                            if (output != null && output.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromDictionary(output));
                            break;

                        case Enumerations.Action.LIST:
                            lock (GroupBayesClassifiersLock)
                            {
                                if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                    break;
                                var classifications =
                                    new HashSet<string>(
                                        GroupBayesClassifiers[corradeCommandParameters.Group.UUID].TagIds());
                                if (classifications.Any())
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                        CSV.FromEnumerable(classifications));
                            }
                            break;

                        case Enumerations.Action.MERGE:
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            source = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOURCE)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(source))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SOURCE_SPECIFIED);
                            target = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);
                            lock (GroupBayesClassifiersLock)
                            {
                                if (GroupBayesClassifiers[corradeCommandParameters.Group.UUID].GetTagById(source) !=
                                    null &&
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].GetTagById(target) !=
                                    null)
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID]
                                        .MergeTags(source, target);
                            }
                            // We are merging so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        case Enumerations.Action.UNTRAIN:
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            category = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CATEGORY)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(category))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CATEGORY_PROVIDED);
                            data =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            lock (GroupBayesClassifiersLock)
                            {
                                GroupBayesClassifiers[corradeCommandParameters.Group.UUID].Untrain(category, data);
                            }
                            // We are untraining so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        case Enumerations.Action.IMPORT:
                            data =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            lock (GroupBayesClassifiersLock)
                            {
                                if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                    GroupBayesClassifiers.Add(corradeCommandParameters.Group.UUID,
                                        new BayesSimpleTextClassifier());
                                GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ImportJsonData(data);
                            }
                            // We are importing so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            break;

                        case Enumerations.Action.EXPORT:
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            lock (GroupBayesClassifiersLock)
                            {
                                string jsonData;
                                lock (GroupBayesClassifiersLock)
                                {
                                    jsonData =
                                        GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData();
                                }
                                if (!string.IsNullOrEmpty(jsonData))
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), jsonData);
                            }
                            break;

                        case Enumerations.Action.REMOVE:
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            category = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CATEGORY)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(category))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CATEGORY_PROVIDED);
                            lock (GroupBayesClassifiersLock)
                            {
                                GroupBayesClassifiers[corradeCommandParameters.Group.UUID].RemoveTag(category);
                            }
                            // We are deleting so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        case Enumerations.Action.ADD:
                            category = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CATEGORY)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(category))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CATEGORY_PROVIDED);
                            lock (GroupBayesClassifiersLock)
                            {
                                if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                    GroupBayesClassifiers.Add(corradeCommandParameters.Group.UUID,
                                        new BayesSimpleTextClassifier());
                                GroupBayesClassifiers[corradeCommandParameters.Group.UUID].AddTag(category);
                            }
                            // We are deleting so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        case Enumerations.Action.RENAME:
                            if (!GroupBayesClassifiers.ContainsKey(corradeCommandParameters.Group.UUID))
                                break;
                            source = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOURCE)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(source))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SOURCE_SPECIFIED);
                            target = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);
                            lock (GroupBayesClassifiersLock)
                            {
                                if (GroupBayesClassifiers[corradeCommandParameters.Group.UUID].GetTagById(source) !=
                                    null &&
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].GetTagById(target) !=
                                    null)
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ChangeTagId(source,
                                        target);
                            }
                            // We are renaming so save the classificiations.
                            SaveGroupBayesClassificiations.Invoke();
                            // broadcast the bayes data.
                            lock (GroupBayesClassifiersLock)
                            {
                                HandleDistributeBayes(corradeCommandParameters.Group.UUID,
                                    GroupBayesClassifiers[corradeCommandParameters.Group.UUID].ExportJsonData(),
                                    Configuration.HordeDataSynchronizationOption.Add);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}