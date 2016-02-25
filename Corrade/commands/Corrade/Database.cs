///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> database =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
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
                    // if the database does not exist...
                    if (!File.Exists(corradeCommandParameters.Group.DatabaseFile))
                    {
                        // ...create the database
                        SQLiteConnection.CreateFile(corradeCommandParameters.Group.DatabaseFile);
                    }
                    List<string> data = new List<string>();
                    using (
                        SQLiteConnection sqlConnection =
                            new SQLiteConnection("Data Source=" + corradeCommandParameters.Group.DatabaseFile +
                                                 ";Version=3;New=True;Compress=True;"))
                    {
                        sqlConnection.Open();
                        using (SQLiteCommand sqlCommand = sqlConnection.CreateCommand())
                        {
                            using (SQLiteTransaction sqlTransaction = sqlConnection.BeginTransaction())
                            {
                                sqlCommand.CommandText = sql;
                                using (SQLiteDataReader sqlDataReader = sqlCommand.ExecuteReader())
                                {
                                    if (sqlDataReader.HasRows)
                                    {
                                        while (sqlDataReader.Read())
                                        {
                                            for (int i = 0; i < sqlDataReader.FieldCount; ++i)
                                            {
                                                data.Add(sqlDataReader.GetName(i));
                                                data.Add(sqlDataReader.GetValue(i).ToString());
                                            }
                                        }
                                    }
                                }
                                sqlTransaction.Commit();
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