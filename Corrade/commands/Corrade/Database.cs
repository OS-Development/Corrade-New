using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> database =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Database))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    if (string.IsNullOrEmpty(commandGroup.DatabaseFile))
                    {
                        throw new ScriptException(ScriptError.NO_DATABASE_FILE_CONFIGURED);
                    }
                    if (!File.Exists(commandGroup.DatabaseFile))
                    {
                        // create the file and close it
                        File.Create(commandGroup.DatabaseFile).Close();
                    }
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    message)).ToLowerInvariant()))
                    {
                        case Action.GET:
                            string databaseGetkey =
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.KEY)),
                                        message));
                            if (string.IsNullOrEmpty(databaseGetkey))
                            {
                                throw new ScriptException(ScriptError.NO_DATABASE_KEY_SPECIFIED);
                            }
                            lock (DatabaseFileLock)
                            {
                                if (!DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Add(commandGroup.Name, new object());
                                }
                            }
                            lock (DatabaseLocks[commandGroup.Name])
                            {
                                string databaseGetValue = wasKeyValueGet(databaseGetkey,
                                    File.ReadAllText(commandGroup.DatabaseFile, Encoding.UTF8));
                                if (!string.IsNullOrEmpty(databaseGetValue))
                                {
                                    result.Add(databaseGetkey,
                                        wasInput(databaseGetValue));
                                }
                            }
                            lock (DatabaseFileLock)
                            {
                                if (DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Remove(commandGroup.Name);
                                }
                            }
                            break;
                        case Action.SET:
                            string databaseSetKey =
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.KEY)),
                                        message));
                            if (string.IsNullOrEmpty(databaseSetKey))
                            {
                                throw new ScriptException(ScriptError.NO_DATABASE_KEY_SPECIFIED);
                            }
                            string databaseSetValue =
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.VALUE)),
                                        message));
                            if (string.IsNullOrEmpty(databaseSetValue))
                            {
                                throw new ScriptException(ScriptError.NO_DATABASE_VALUE_SPECIFIED);
                            }
                            lock (DatabaseFileLock)
                            {
                                if (!DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Add(commandGroup.Name, new object());
                                }
                            }
                            lock (DatabaseLocks[commandGroup.Name])
                            {
                                string contents = File.ReadAllText(commandGroup.DatabaseFile, Encoding.UTF8);
                                using (
                                    StreamWriter recreateDatabase = new StreamWriter(commandGroup.DatabaseFile,
                                        false, Encoding.UTF8))
                                {
                                    recreateDatabase.Write(wasKeyValueSet(databaseSetKey,
                                        databaseSetValue, contents));
                                    recreateDatabase.Flush();
                                    //recreateDatabase.Close();
                                }
                            }
                            lock (DatabaseFileLock)
                            {
                                if (DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Remove(commandGroup.Name);
                                }
                            }
                            break;
                        case Action.DELETE:
                            string databaseDeleteKey =
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.KEY)),
                                        message));
                            if (string.IsNullOrEmpty(databaseDeleteKey))
                            {
                                throw new ScriptException(ScriptError.NO_DATABASE_KEY_SPECIFIED);
                            }
                            lock (DatabaseFileLock)
                            {
                                if (!DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Add(commandGroup.Name, new object());
                                }
                            }
                            lock (DatabaseLocks[commandGroup.Name])
                            {
                                string contents = File.ReadAllText(commandGroup.DatabaseFile, Encoding.UTF8);
                                using (
                                    StreamWriter recreateDatabase = new StreamWriter(commandGroup.DatabaseFile,
                                        false, Encoding.UTF8))
                                {
                                    recreateDatabase.Write(wasKeyValueDelete(databaseDeleteKey, contents));
                                    recreateDatabase.Flush();
                                    //recreateDatabase.Close();
                                }
                            }
                            lock (DatabaseFileLock)
                            {
                                if (DatabaseLocks.ContainsKey(commandGroup.Name))
                                {
                                    DatabaseLocks.Remove(commandGroup.Name);
                                }
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_DATABASE_ACTION);
                    }
                };
        }
    }
}