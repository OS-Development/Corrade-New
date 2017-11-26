///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getregiontop =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Client.Network.CurrentSim.IsEstateManager)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    var topTasks = new Dictionary<UUID, EstateTask>();
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                        ))
                    {
                        case Enumerations.Type.SCRIPTS:
                            var TopScriptsReplyEvent = new ManualResetEventSlim(false);
                            EventHandler<TopScriptsReplyEventArgs> TopScriptsReplyEventHandler = (sender, args) =>
                            {
                                topTasks =
                                    args.Tasks.OrderByDescending(o => o.Value.Score)
                                        .GroupBy(o => o.Key)
                                        .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                                TopScriptsReplyEvent.Set();
                            };
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.TopScriptsReply += TopScriptsReplyEventHandler;
                            Client.Estate.RequestTopScripts();
                            if (!TopScriptsReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_TOP_SCRIPTS);
                            }
                            Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.COLLIDERS:
                            var TopCollidersReplyEvent = new ManualResetEventSlim(false);
                            EventHandler<TopCollidersReplyEventArgs> TopCollidersReplyEventHandler =
                                (sender, args) =>
                                {
                                    topTasks =
                                        args.Tasks.OrderByDescending(o => o.Value.Score)
                                            .GroupBy(o => o.Key)
                                            .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                                    TopCollidersReplyEvent.Set();
                                };
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.TopCollidersReply += TopCollidersReplyEventHandler;
                            Client.Estate.RequestTopColliders();
                            if (
                                !TopCollidersReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_TOP_COLLIDERS);
                            }
                            Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_TOP_TYPE);
                    }
                    uint amount;
                    if (
                        !uint.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AMOUNT)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                            out amount))
                        amount = (uint) topTasks.Count;
                    var data = new List<string>(topTasks.Take((int) amount).Select(o => new[]
                    {
                        o.Value.Score.ToString(Utils.EnUsCulture),
                        o.Value.TaskName,
                        o.Key.ToString(),
                        o.Value.OwnerName,
                        o.Value.Position.ToString()
                    }).SelectMany(o => o));
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}