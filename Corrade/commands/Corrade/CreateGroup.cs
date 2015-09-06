using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> creategroup =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    // if the grid is SecondLife and the group name length exceeds the allowed length...
                    if (IsSecondLife() &&
                        commandGroup.Name.Length > LINDEN_CONSTANTS.GROUPS.MAXIMUM_GROUP_NAME_LENGTH)
                    {
                        throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_NAME);
                    }
                    if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    if (Client.Self.Balance < corradeConfiguration.GroupCreateFee)
                    {
                        throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                    }
                    if (!corradeConfiguration.GroupCreateFee.Equals(0) &&
                        !HasCorradePermission(commandGroup.Name, (int) Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    OpenMetaverse.Group targetGroup = new OpenMetaverse.Group
                    {
                        Name = commandGroup.Name
                    };
                    wasCSVToStructure(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                            message)),
                        ref targetGroup);
                    bool succeeded = false;
                    ManualResetEvent GroupCreatedReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupCreatedReplyEventArgs> GroupCreatedEventHandler = (sender, args) =>
                    {
                        succeeded = args.Success;
                        GroupCreatedReplyEvent.Set();
                    };
                    lock (ClientInstanceGroupsLock)
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