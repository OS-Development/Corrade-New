///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CorradeConfiguration;
using Mono.Data.Sqlite;
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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    if (string.IsNullOrEmpty(corradeCommandParameters.Group.DatabaseFile))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATABASE_FILE_CONFIGURED);
                    }
                    var sql = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SQL)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(sql))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_SQL_STRING_PROVIDED);
                    }
                    var data = new List<string>();

                    using (IDbConnection dbcon =
                        new SqliteConnection(@"URI=file:" + corradeCommandParameters.Group.DatabaseFile))
                    {
                        dbcon.Open();
                        using (var dbcmd = dbcon.CreateCommand())
                        {
                            dbcmd.CommandText = sql;
                            using (var dbtransaction = dbcon.BeginTransaction())
                            {
                                using (var reader = dbcmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        for (var i = 0; i < reader.FieldCount; ++i)
                                        {
                                            data.Add(reader.GetName(i));
                                            data.Add(reader.GetValue(i).ToString());
                                        }
                                    }
                                }
                                dbtransaction.Commit();
                            }
                        }
                    }

                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}