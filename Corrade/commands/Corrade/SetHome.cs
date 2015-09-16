///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> sethome =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    bool succeeded = true;
                    ManualResetEvent AlertMessageEvent = new ManualResetEvent(false);
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                    {
                        switch (args.Message)
                        {
                            case LINDEN_CONSTANTS.ALERTS.UNABLE_TO_SET_HOME:
                                succeeded = false;
                                AlertMessageEvent.Set();
                                break;
                            case LINDEN_CONSTANTS.ALERTS.HOME_SET:
                                succeeded = true;
                                AlertMessageEvent.Set();
                                break;
                        }
                    };
                    lock (ClientInstanceSelfLock)
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