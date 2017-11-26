///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Community.CsharpSqlite.SQLiteClient;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> database =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Database))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    if (string.IsNullOrEmpty(corradeCommandParameters.Group.DatabaseFile))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATABASE_FILE_CONFIGURED);

                    var sql = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SQL)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(sql))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_SQL_STRING_PROVIDED);

                    var data = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));

                    var csv = new List<string>();
                    try
                    {
                        using (var sqliteConnection =
                            new SqliteConnection($"URI=file:{corradeCommandParameters.Group.DatabaseFile}"))
                        {
                            sqliteConnection.Open();
                            using (var command = new SqliteCommand(sql, sqliteConnection))
                            {
                                if (!string.IsNullOrEmpty(data))
                                    foreach (var parameter in CSV.ToKeyValue(data)
                                        .AsParallel()
                                        .GroupBy(o => o.Key)
                                        .Select(o => o.FirstOrDefault())
                                        .ToDictionary(o => wasInput(o.Key), o => wasInput(o.Value)))
                                        command
                                            .Parameters
                                            .Add(new SqliteParameter(parameter.Key, parameter.Value));
                                using (var dbtransaction = sqliteConnection.BeginTransaction())
                                {
                                    using (var reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                            for (var i = 0; i < reader.FieldCount; ++i)
                                            {
                                                csv.Add(reader.GetName(i));
                                                csv.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
                                            }
                                    }
                                    dbtransaction.Commit();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.SQL_EXECUTION_FAILED);
                    }

                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}