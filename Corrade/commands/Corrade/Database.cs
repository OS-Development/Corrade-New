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
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> database =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Database))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    if (string.IsNullOrEmpty(corradeCommandParameters.Group.DatabaseFile))
                    {
                        throw new ScriptException(ScriptError.NO_DATABASE_FILE_CONFIGURED);
                    }
                    string sql = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SQL)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(sql))
                    {
                        throw new ScriptException(ScriptError.NO_SQL_STRING_PROVIDED);
                    }
                    List<string> data = new List<string>();

                    using (IDbConnection dbcon =
                        new SqliteConnection(@"URI=file:" + corradeCommandParameters.Group.DatabaseFile))
                    {
                        dbcon.Open();
                        using (IDbCommand dbcmd = dbcon.CreateCommand())
                        {
                            dbcmd.CommandText = sql;
                            using (IDbTransaction dbtransaction = dbcon.BeginTransaction())
                            {
                                using (IDataReader reader = dbcmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        for (int i = 0; i < reader.FieldCount; ++i)
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
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}