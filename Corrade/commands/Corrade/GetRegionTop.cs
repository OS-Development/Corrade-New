///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getregiontop =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    Dictionary<UUID, EstateTask> topTasks = new Dictionary<UUID, EstateTask>();
                    switch (
                        Reflection.GetEnumValueFromName<Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                                .ToLowerInvariant()))
                    {
                        case Type.SCRIPTS:
                            ManualResetEvent TopScriptsReplyEvent = new ManualResetEvent(false);
                            EventHandler<TopScriptsReplyEventArgs> TopScriptsReplyEventHandler = (sender, args) =>
                            {
                                topTasks =
                                    args.Tasks.OrderByDescending(o => o.Value.Score)
                                        .ToDictionary(o => o.Key, o => o.Value);
                                TopScriptsReplyEvent.Set();
                            };
                            lock (Locks.ClientInstanceEstateLock)
                            {
                                Client.Estate.TopScriptsReply += TopScriptsReplyEventHandler;
                                Client.Estate.RequestTopScripts();
                                if (!TopScriptsReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_TOP_SCRIPTS);
                                }
                                Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                            }
                            break;
                        case Type.COLLIDERS:
                            ManualResetEvent TopCollidersReplyEvent = new ManualResetEvent(false);
                            EventHandler<TopCollidersReplyEventArgs> TopCollidersReplyEventHandler =
                                (sender, args) =>
                                {
                                    topTasks =
                                        args.Tasks.OrderByDescending(o => o.Value.Score)
                                            .ToDictionary(o => o.Key, o => o.Value);
                                    TopCollidersReplyEvent.Set();
                                };
                            lock (Locks.ClientInstanceEstateLock)
                            {
                                Client.Estate.TopCollidersReply += TopCollidersReplyEventHandler;
                                Client.Estate.RequestTopColliders();
                                if (
                                    !TopCollidersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_TOP_COLLIDERS);
                                }
                                Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_TOP_TYPE);
                    }
                    int amount;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AMOUNT)),
                                    corradeCommandParameters.Message)),
                            out amount))
                    {
                        amount = topTasks.Count;
                    }
                    List<string> data = new List<string>(topTasks.Take(amount).Select(o => new[]
                    {
                        o.Value.Score.ToString(Utils.EnUsCulture),
                        o.Value.TaskName,
                        o.Key.ToString(),
                        o.Value.OwnerName,
                        o.Value.Position.ToString()
                    }).SelectMany(o => o));
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}