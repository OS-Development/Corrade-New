///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures.Effects
{
    /// <summary>
    ///     A structure to track Sphere effects.
    /// </summary>
    public struct SphereEffect
    {
        [Reflection.NameAttribute("alpha")] public float Alpha;
        [Reflection.NameAttribute("color")] public Vector3 Color;
        [Reflection.NameAttribute("duration")] public float Duration;
        [Reflection.NameAttribute("effect")] public UUID Effect;
        [Reflection.NameAttribute("offset")] public Vector3d Offset;
        [Reflection.NameAttribute("termination")] public DateTime Termination;
    }
}