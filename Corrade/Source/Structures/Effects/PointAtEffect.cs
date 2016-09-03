///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures.Effects
{
    /// <summary>
    ///     A structure to track PointAt effects.
    /// </summary>
    public struct PointAtEffect
    {
        [Reflection.NameAttribute("effect")] public UUID Effect;
        [Reflection.NameAttribute("offset")] public Vector3d Offset;
        [Reflection.NameAttribute("source")] public UUID Source;
        [Reflection.NameAttribute("target")] public UUID Target;
        [Reflection.NameAttribute("type")] public PointAtType Type;
    }
}