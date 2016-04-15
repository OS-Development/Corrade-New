///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> attachoverorreplace = attach;
        }
    }
}