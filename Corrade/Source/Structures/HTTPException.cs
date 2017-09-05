///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;

using System.Runtime.Serialization;

namespace Corrade.Structures
{
    /// <summary>
    ///     An exception thrown on processing commands via the HTTP server.
    /// </summary>
    [Serializable]
    public class HTTPException : Exception
    {
        public int StatusCode;

        public HTTPException()
        {
        }

        public HTTPException(int code) : base($"Status Code: {code}")
        {
            StatusCode = code;
        }

        protected HTTPException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
