///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using OpenMetaverse;

namespace wasOpenMetaverse
{
    public class Services
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates the current balance by requesting it from the grid.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the request in milliseconds</param>
        /// <returns>true if the balance could be retrieved</returns>
        public static bool UpdateBalance(GridClient Client, uint millisecondsTimeout)
        {
            ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
            EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceEventHandler =
                (sender, args) => MoneyBalanceEvent.Set();
            lock (Locks.ClientInstanceSelfLock)
            {
                Client.Self.MoneyBalanceReply += MoneyBalanceEventHandler;
                Client.Self.RequestBalance();
                if (!MoneyBalanceEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
                    return false;
                }
                Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
            }
            return true;
        }
    }
}