///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getinventorypath
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var assetTypes = new HashSet<AssetType>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o => typeof(AssetType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                .ForAll(q =>
                                {
                                    lock (LockObject)
                                    {
                                        assetTypes.Add((AssetType) q.GetValue(null));
                                    }
                                }));
                    var pattern =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATTERN)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(pattern))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PATTERN_PROVIDED);
                    Regex search;
                    try
                    {
                        search = new Regex(pattern, RegexOptions.Compiled);
                    }
                    catch
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError
                            .COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                    }
                    var csv = new List<string>();
                    // Search inventory.
                    Inventory.FindInventoryPath<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                        search, new LinkedList<string>()).AsParallel().Select(o => o.Value).ForAll(o =>
                    {
                        lock (LockObject)
                        {
                            csv.Add(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR.ToString(), o.ToArray()));
                        }
                    });
                    // Search library.
                    Inventory.FindInventoryPath<InventoryBase>(Client, Client.Inventory.Store.LibraryRootNode,
                        search, new LinkedList<string>()).AsParallel().Select(o => o.Value).ForAll(o =>
                    {
                        lock (LockObject)
                        {
                            csv.Add(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR.ToString(), o.ToArray()));
                        }
                    });
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}