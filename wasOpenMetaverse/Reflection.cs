///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using wasSharp;

namespace wasOpenMetaverse
{
    public static class Reflection
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     The function serializes an object as an enumerable.
        /// </summary>
        /// <param name="data">the value to get</param>
        /// <returns>the value or values as a string</returns>
        public static IEnumerable<string> wasSerializeObject(object data)
        {
            if (data == null) yield break;
            // Handle arrays and lists
            if (data is Array || data is IList)
            {
                var iList = (IList) data;
                foreach (
                    var item in iList.Cast<object>().Where(o => o != null).Select((value, index) => new {value, index}))
                {
                    // These are index collections so pre-prend an index.
                    yield return "Index";
                    yield return item.index.ToString();
                    switch (item.value.GetType().IsPrimitive || item.value is string)
                    {
                        case true: // Don't bother with primitive types.
                            yield return item.value.ToString();
                            break;
                        default:
                            foreach (
                                var fi in wasSharpNET.Reflection.wasGetFields(item.value, item.value.GetType().Name))
                            {
                                if (fi.Key != null)
                                {
                                    foreach (
                                        var fieldString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(fi.Key, fi.Value))
                                        )
                                    {
                                        yield return fi.Key.Name;
                                        yield return fieldString;
                                    }
                                }
                            }
                            foreach (
                                var pi in wasSharpNET.Reflection.wasGetProperties(item.value, item.value.GetType().Name)
                                )
                            {
                                if (pi.Key != null)
                                {
                                    foreach (
                                        var propertyString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(pi.Key, pi.Value))
                                        )
                                    {
                                        yield return pi.Key.Name;
                                        yield return propertyString;
                                    }
                                }
                            }
                            break;
                    }
                }
                yield break;
            }
            // Handle Dictionary
            if (data is IDictionary)
            {
                var dictionary = (IDictionary) data;
                foreach (DictionaryEntry entry in dictionary)
                {
                    // First the keys.
                    switch (entry.Key.GetType().IsPrimitive || entry.Key is string)
                    {
                        case true: // Don't bother with primitive types.
                            yield return entry.Key.ToString();
                            break;
                        default:
                            foreach (var fi in wasSharpNET.Reflection.wasGetFields(entry.Key, entry.Key.GetType().Name))
                            {
                                if (fi.Key != null)
                                {
                                    foreach (
                                        var fieldString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(fi.Key, fi.Value))
                                        )
                                    {
                                        yield return fi.Key.Name;
                                        yield return fieldString;
                                    }
                                }
                            }
                            foreach (
                                var pi in wasSharpNET.Reflection.wasGetProperties(entry.Key, entry.Key.GetType().Name))
                            {
                                if (pi.Key != null)
                                {
                                    foreach (
                                        var propertyString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(pi.Key, pi.Value))
                                        )
                                    {
                                        yield return pi.Key.Name;
                                        yield return propertyString;
                                    }
                                }
                            }
                            break;
                    }

                    // Then the values.
                    switch (entry.Value.GetType().IsPrimitive || entry.Value is string)
                    {
                        case true: // Don't bother with primitive types.
                            yield return entry.Value.ToString();
                            break;
                        default:
                            foreach (
                                var fi in wasSharpNET.Reflection.wasGetFields(entry.Value, entry.Value.GetType().Name)
                                )
                            {
                                if (fi.Key != null)
                                {
                                    foreach (
                                        var fieldString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(fi.Key, fi.Value))
                                        )
                                    {
                                        yield return fi.Key.Name;
                                        yield return fieldString;
                                    }
                                }
                            }
                            foreach (
                                var pi in
                                    wasSharpNET.Reflection.wasGetProperties(entry.Value, entry.Value.GetType().Name))
                            {
                                if (pi.Key != null)
                                {
                                    foreach (
                                        var propertyString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(pi.Key, pi.Value))
                                        )
                                    {
                                        yield return pi.Key.Name;
                                        yield return propertyString;
                                    }
                                }
                            }
                            break;
                    }
                }
                yield break;
            }
            // Handle InternalDictionary
            var internalDictionaryInfo = data.GetType()
                .GetField("Dictionary",
                    BindingFlags.Default | BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic);
            if (internalDictionaryInfo != null)
            {
                var iDictionary = internalDictionaryInfo.GetValue(data) as IDictionary;
                if (iDictionary == null) yield break;
                foreach (DictionaryEntry entry in iDictionary)
                {
                    // First the keys.
                    switch (entry.Key.GetType().IsPrimitive || entry.Key is string)
                    {
                        case true: // Don't bother with primitive types.
                            yield return entry.Key.ToString();
                            break;
                        default:
                            foreach (var fi in wasSharpNET.Reflection.wasGetFields(entry.Key, entry.Key.GetType().Name))
                            {
                                if (fi.Key != null)
                                {
                                    foreach (
                                        var fieldString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(fi.Key, fi.Value))
                                        )
                                    {
                                        yield return fi.Key.Name;
                                        yield return fieldString;
                                    }
                                }
                            }
                            foreach (
                                var pi in wasSharpNET.Reflection.wasGetProperties(entry.Key, entry.Key.GetType().Name))
                            {
                                if (pi.Key != null)
                                {
                                    foreach (
                                        var propertyString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(pi.Key, pi.Value))
                                        )
                                    {
                                        yield return pi.Key.Name;
                                        yield return propertyString;
                                    }
                                }
                            }
                            break;
                    }

                    // Then the values.
                    switch (entry.Value.GetType().IsPrimitive || entry.Value is string)
                    {
                        case true: // Don't bother with primitive types.
                            yield return entry.Value.ToString();
                            break;
                        default:
                            foreach (
                                var fi in wasSharpNET.Reflection.wasGetFields(entry.Value, entry.Value.GetType().Name)
                                )
                            {
                                if (fi.Key != null)
                                {
                                    foreach (
                                        var fieldString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(fi.Key, fi.Value))
                                        )
                                    {
                                        yield return fi.Key.Name;
                                        yield return fieldString;
                                    }
                                }
                            }
                            foreach (
                                var pi in
                                    wasSharpNET.Reflection.wasGetProperties(entry.Value, entry.Value.GetType().Name))
                            {
                                if (pi.Key != null)
                                {
                                    foreach (
                                        var propertyString in
                                            wasSerializeObject(wasSharpNET.Reflection.wasGetInfoValue(pi.Key, pi.Value))
                                        )
                                    {
                                        yield return pi.Key.Name;
                                        yield return propertyString;
                                    }
                                }
                            }
                            break;
                    }
                }
                yield break;
            }

            if (data is IEnumerable<string>)
            {
                var iEnumerable = (IEnumerable<string>) data;
                foreach (var item in iEnumerable)
                {
                    yield return item;
                }
                yield break;
            }

            // Handle friend rights.
            if (data is FriendRights)
            {
                var friendRights = (FriendRights) data;
                foreach (var flag in typeof(FriendRights).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .AsParallel()
                    .Where(o => friendRights.IsMaskFlagSet((FriendRights) o.GetValue(null)))
                    .Select(o => o.Name))
                {
                    yield return flag;
                }
                yield break;
            }

            // Handle script controls.
            if (data is ScriptControlChange)
            {
                var scriptControlChange = (ScriptControlChange) data;
                foreach (var flag in typeof(ScriptControlChange).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .AsParallel()
                    .Where(o => scriptControlChange.IsMaskFlagSet((ScriptControlChange) o.GetValue(null)))
                    .Select(o => o.Name))
                {
                    yield return flag;
                }
                yield break;
            }

            // Handle date and time as an LSL timestamp.
            if (data is DateTime)
            {
                yield return ((DateTime) data).ToString(Constants.LSL.DATE_TIME_STAMP);
                yield break;
            }

            // Use the Corrade permission system instead.
            if (data is Permissions)
            {
                yield return Inventory.wasPermissionsToString((Permissions) data);
                yield break;
            }

            if (data is ParcelFlags)
            {
                var parcelFlags = (ParcelFlags) data;
                foreach (var flag in typeof(ParcelFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .AsParallel()
                    .Where(o => parcelFlags.IsMaskFlagSet((ParcelFlags) o.GetValue(null)))
                    .Select(o => o.Name))
                {
                    yield return flag;
                }
                yield break;
            }

            if (data is GroupPowers)
            {
                var groupPowers = (GroupPowers) data;
                foreach (var power in typeof(GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .AsParallel()
                    .Where(o => groupPowers.IsMaskFlagSet((GroupPowers) o.GetValue(null)))
                    .Select(o => o.Name))
                {
                    yield return power;
                }
                yield break;
            }

            var @string = data.ToString();
            if (string.IsNullOrEmpty(@string)) yield break;
            yield return @string;
        }

        public static object GetStructuredData(object toInventory, string v)
        {
            throw new NotImplementedException();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Takes as input a CSV data values and sets the corresponding
        ///     structure's fields or properties from the CSV data.
        /// </summary>
        /// <typeparam name="T">the type of the structure</typeparam>
        /// <param name="kvp">a CSV string</param>
        /// <param name="structure">the structure to set the fields and properties for</param>
        public static T wasCSVToStructure<T>(this T structure, string kvp)
        {
            CSV.ToKeyValue(kvp).AsParallel().ForAll(d =>
            {
                var info = structure.GetFPInfo(d.Key);

                if (info == null) return;

                var data = wasSharpNET.Reflection.wasGetInfoValue(info, structure);

                // OpenMetaverse particular flags.
                if (data is ParcelFlags)
                {
                    ParcelFlags parcelFlags;
                    switch (!Enum.TryParse(d.Value, out parcelFlags))
                    {
                        case true:
                            var allFlags =
                                typeof(ParcelFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .ToDictionary(o => o.Name, o => (ParcelFlags) o.GetValue(null));
                            CSV.ToEnumerable(d.Value).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(
                                o =>
                                {
                                    ParcelFlags parcelFlag;
                                    if (allFlags.TryGetValue(o, out parcelFlag))
                                    {
                                        BitTwiddling.SetMaskFlag(ref parcelFlags, parcelFlag);
                                    }
                                });
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, parcelFlags);
                    return;
                }

                if (data is GroupPowers)
                {
                    GroupPowers groupPowers;
                    switch (!Enum.TryParse(d.Value, out groupPowers))
                    {
                        case true:
                            var allPowers =
                                typeof(GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .ToDictionary(o => o.Name, o => (GroupPowers) o.GetValue(null));
                            CSV.ToEnumerable(d.Value).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(
                                o =>
                                {
                                    GroupPowers groupPower;
                                    if (allPowers.TryGetValue(o, out groupPower))
                                    {
                                        BitTwiddling.SetMaskFlag(ref groupPowers, groupPower);
                                    }
                                });
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, groupPowers);
                    return;
                }
                if (data is AttachmentPoint)
                {
                    byte attachmentPoint;
                    switch (!byte.TryParse(d.Value, out attachmentPoint))
                    {
                        case true:
                            var attachmentPointFieldInfo =
                                typeof(AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (attachmentPointFieldInfo == null) break;
                            attachmentPoint = (byte) attachmentPointFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, attachmentPoint);
                    return;
                }
                if (data is Tree)
                {
                    byte tree;
                    switch (!byte.TryParse(d.Value, out tree))
                    {
                        case true:
                            var treeFieldInfo = typeof(Tree).GetFields(BindingFlags.Public |
                                                                       BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (treeFieldInfo == null) break;
                            tree = (byte) treeFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, tree);
                    return;
                }
                if (data is Material)
                {
                    byte material;
                    switch (!byte.TryParse(d.Value, out material))
                    {
                        case true:
                            var materialFieldInfo = typeof(Material).GetFields(BindingFlags.Public |
                                                                               BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (materialFieldInfo == null) break;
                            material = (byte) materialFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, material);
                    return;
                }
                if (data is PathCurve)
                {
                    byte pathCurve;
                    switch (!byte.TryParse(d.Value, out pathCurve))
                    {
                        case true:
                            var pathCurveFieldInfo = typeof(PathCurve).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (pathCurveFieldInfo == null) break;
                            pathCurve = (byte) pathCurveFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, pathCurve);
                    return;
                }
                if (data is PCode)
                {
                    byte pCode;
                    switch (!byte.TryParse(d.Value, out pCode))
                    {
                        case true:
                            var pCodeFieldInfo = typeof(PCode).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (pCodeFieldInfo == null) break;
                            pCode = (byte) pCodeFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, pCode);
                    return;
                }
                if (data is ProfileCurve)
                {
                    byte profileCurve;
                    switch (!byte.TryParse(d.Value, out profileCurve))
                    {
                        case true:
                            var profileCurveFieldInfo =
                                typeof(ProfileCurve).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (profileCurveFieldInfo == null) break;
                            profileCurve = (byte) profileCurveFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, profileCurve);
                    return;
                }
                if (data is HoleType)
                {
                    byte holeType;
                    switch (!byte.TryParse(d.Value, out holeType))
                    {
                        case true:
                            var holeTypeFieldInfo = typeof(HoleType).GetFields(BindingFlags.Public |
                                                                               BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (holeTypeFieldInfo == null) break;
                            holeType = (byte) holeTypeFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, holeType);
                    return;
                }
                if (data is SculptType)
                {
                    byte sculptType;
                    switch (!byte.TryParse(d.Value, out sculptType))
                    {
                        case true:
                            var sculptTypeFieldInfo = typeof(SculptType).GetFields(BindingFlags.Public |
                                                                                   BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => Strings.StringEquals(d.Value, p.Name, StringComparison.Ordinal));
                            if (sculptTypeFieldInfo == null) break;
                            sculptType = (byte) sculptTypeFieldInfo.GetValue(null);
                            break;
                    }
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, sculptType);
                    return;
                }
                // OpenMetaverse Primitive Types
                if (data is UUID)
                {
                    UUID UUIDData;
                    if (!UUID.TryParse(d.Value, out UUIDData))
                        return;

                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, UUIDData);
                    return;
                }
                if (data is Vector3)
                {
                    Vector3 vector3Data;
                    if (Vector3.TryParse(d.Value, out vector3Data))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, vector3Data);
                        return;
                    }
                }
                if (data is Vector2)
                {
                    Vector3 vector2Data;
                    if (Vector2.TryParse(d.Value, out vector2Data))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, vector2Data);
                        return;
                    }
                }
                if (data is Vector3d)
                {
                    Vector3d vector3DData;
                    if (Vector3d.TryParse(d.Value, out vector3DData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, vector3DData);
                        return;
                    }
                }
                if (data is Vector4)
                {
                    Vector4 vector4Data;
                    if (Vector4.TryParse(d.Value, out vector4Data))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, vector4Data);
                        return;
                    }
                }
                if (data is Quaternion)
                {
                    Quaternion quaternionData;
                    if (Quaternion.TryParse(d.Value, out quaternionData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, quaternionData);
                        return;
                    }
                }
                // Primitive types.
                if (data is bool)
                {
                    bool boolData;
                    if (bool.TryParse(d.Value, out boolData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, boolData);
                        return;
                    }
                }
                if (data is char)
                {
                    char charData;
                    if (char.TryParse(d.Value, out charData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, charData);
                        return;
                    }
                }
                if (data is decimal)
                {
                    decimal decimalData;
                    if (decimal.TryParse(d.Value, out decimalData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, decimalData);
                        return;
                    }
                }
                if (data is byte)
                {
                    byte byteData;
                    if (byte.TryParse(d.Value, out byteData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, byteData);
                        return;
                    }
                }
                if (data is int)
                {
                    int intData;
                    if (int.TryParse(d.Value, out intData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, intData);
                        return;
                    }
                }
                if (data is uint)
                {
                    uint uintData;
                    if (uint.TryParse(d.Value, out uintData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, uintData);
                        return;
                    }
                }
                if (data is float)
                {
                    float floatData;
                    if (float.TryParse(d.Value, out floatData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, floatData);
                        return;
                    }
                }
                if (data is long)
                {
                    long longData;
                    if (long.TryParse(d.Value, out longData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, longData);
                        return;
                    }
                }
                if (data is float)
                {
                    float singleData;
                    if (float.TryParse(d.Value, out singleData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, singleData);
                        return;
                    }
                }
                if (data is DateTime)
                {
                    DateTime dateTimeData;
                    if (DateTime.TryParse(d.Value, out dateTimeData))
                    {
                        wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, dateTimeData);
                        return;
                    }
                }
                if (data is string)
                {
                    wasSharpNET.Reflection.wasSetInfoValue(info, ref structure, d.Value);
                }
            });

            return structure;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the values from structures as strings.
        /// </summary>
        /// <typeparam name="T">the type of the structure</typeparam>
        /// <param name="structure">the structure</param>
        /// <param name="query">a CSV list of fields or properties to get</param>
        /// <returns>value strings</returns>
        public static IEnumerable<string> GetStructuredData<T>(this T structure, string query)
        {
            var result = new HashSet<string[]>();
            if (structure.Equals(default(T)))
                return result.SelectMany(o => o);

            var LockObject = new object();
            CSV.ToEnumerable(query).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(name =>
            {
                var data = new List<string> {name};
                var vals = new List<string>(wasSerializeObject(structure.GetFP(name)));
                switch (vals.Any())
                {
                    case true:
                        data.AddRange(vals);
                        break;
                    default:
                        data.Add(string.Empty);
                        break;
                }
                lock (LockObject)
                {
                    result.Add(data.ToArray());
                }
            });
            return result.SelectMany(o => o);
        }
    }
}