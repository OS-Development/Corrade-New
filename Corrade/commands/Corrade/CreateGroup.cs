///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> creategroup =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(target))
                        target = corradeCommandParameters.Group.Name;
                    // if the grid is SecondLife and the group name length exceeds the allowed length...
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        target.Length > wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_NAME_LENGTH)
                        throw new Command.ScriptException(Enumerations.ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_NAME);
                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    if (Client.Self.Balance < corradeConfiguration.GroupCreateFee)
                        throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                    if (!corradeConfiguration.GroupCreateFee.Equals(0) &&
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var targetGroup = new Group
                    {
                        Name = target
                    };
                    targetGroup =
                        targetGroup.wasCSVToStructure(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message)), wasInput);
                    var succeeded = false;
                    var GroupCreatedReplyEvent = new ManualResetEventSlim(false);
                    var groupUUID = UUID.Zero;
                    EventHandler<GroupCreatedReplyEventArgs> GroupCreatedEventHandler = (sender, args) =>
                    {
                        groupUUID = args.GroupID;
                        succeeded = args.Success;
                        GroupCreatedReplyEvent.Set();
                    };
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.GroupCreatedReply += GroupCreatedEventHandler;
                    Client.Groups.RequestCreateGroup(targetGroup);
                    if (!GroupCreatedReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_GROUP);
                    }
                    Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                    var groupName = string.Empty;
                    if (!succeeded ||
                        !Resolvers.GroupUUIDToName(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                            ref groupName) || !groupName.Equals(target))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_CREATE_GROUP);
                };
        }
    }
}