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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> sethome =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var succeeded = true;
                    var AlertMessageEvent = new ManualResetEvent(false);
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                    {
                        switch (args.Message)
                        {
                            case Constants.ALERTS.UNABLE_TO_SET_HOME:
                                succeeded = false;
                                AlertMessageEvent.Set();
                                break;
                            case Constants.ALERTS.HOME_SET:
                                succeeded = true;
                                AlertMessageEvent.Set();
                                break;
                        }
                    };
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.AlertMessage += AlertMessageEventHandler;
                        Client.Self.SetHome();
                        if (!AlertMessageEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Self.AlertMessage -= AlertMessageEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_REQUESTING_TO_SET_HOME);
                        }
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.UNABLE_TO_SET_HOME);
                    }
                };
        }
    }
}