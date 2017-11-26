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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> sethome =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var succeeded = true;
                    var AlertMessageEvent = new ManualResetEventSlim(false);
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                    {
                        switch (args.Message)
                        {
                            case wasOpenMetaverse.Constants.ALERTS.UNABLE_TO_SET_HOME:
                                succeeded = false;
                                AlertMessageEvent.Set();
                                break;

                            case wasOpenMetaverse.Constants.ALERTS.HOME_SET:
                                succeeded = true;
                                AlertMessageEvent.Set();
                                break;
                        }
                    };
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.AlertMessage += AlertMessageEventHandler;
                    Client.Self.SetHome();
                    if (!AlertMessageEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_REQUESTING_TO_SET_HOME);
                    }
                    Client.Self.AlertMessage -= AlertMessageEventHandler;
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_SET_HOME);
                };
        }
    }
}