///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2017 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.Serialization;
using wasSharp;
using static Corrade.Enumerations;

namespace Corrade.Source.WebForms.SecondLife
{
    /// <summary>
    ///     An exception thrown on script errors.
    /// </summary>
    [Serializable]
    public class ScriptedAgentStatusException : Exception
    {
        public ScriptedAgentStatusException(ScriptedAgentStatusError error)
            : base(Reflection.GetDescriptionFromEnumValue(error))
        {
            Error = error;
        }

        protected ScriptedAgentStatusException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public ScriptedAgentStatusError Error { get; }
    }
}
