///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> creategroup =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    // if the grid is SecondLife and the group name length exceeds the allowed length...
                    if (IsSecondLife() &&
                        corradeCommandParameters.Group.Name.Length > Constants.GROUPS.MAXIMUM_GROUP_NAME_LENGTH)
                    {
                        throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_NAME);
                    }
                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    if (Client.Self.Balance < corradeConfiguration.GroupCreateFee)
                    {
                        throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                    }
                    if (!corradeConfiguration.GroupCreateFee.Equals(0) &&
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Group targetGroup = new Group
                    {
                        Name = corradeCommandParameters.Group.Name
                    };
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)),
                        ref targetGroup);
                    bool succeeded = false;
                    ManualResetEvent GroupCreatedReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupCreatedReplyEventArgs> GroupCreatedEventHandler = (sender, args) =>
                    {
                        succeeded = args.Success;
                        GroupCreatedReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupCreatedReply += GroupCreatedEventHandler;
                        Client.Groups.RequestCreateGroup(targetGroup);
                        if (!GroupCreatedReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_CREATING_GROUP);
                        }
                        Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_CREATE_GROUP);
                    }
                };
        }
    }
}