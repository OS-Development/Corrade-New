///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace wasSharpNET
{
    public static class Reflection
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Enumerates the fields of an object along with the child objects,
        ///     provided that all child objects are part of a specified namespace.
        /// </summary>
        /// <param name="object">the object to enumerate</param>
        /// <param name="namespace">the namespace to enumerate in</param>
        /// <returns>child objects of the object</returns>
        public static IEnumerable<KeyValuePair<FieldInfo, object>> wasGetFields(object @object, string @namespace)
        {
            if (@object == null) yield break;

            foreach (var fi in @object.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (fi.FieldType.FullName.Split('.', '+')
                    .Contains(@namespace, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var sf in wasGetFields(fi.GetValue(@object), @namespace))
                    {
                        yield return sf;
                    }
                }
                yield return new KeyValuePair<FieldInfo, object>(fi, @object);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Enumerates the properties of an object along with the child objects,
        ///     provided that all child objects are part of a specified namespace.
        /// </summary>
        /// <param name="object">the object to enumerate</param>
        /// <param name="namespace">the namespace to enumerate in</param>
        /// <returns>child objects of the object</returns>
        public static IEnumerable<KeyValuePair<PropertyInfo, object>> wasGetProperties(object @object,
            string @namespace)
        {
            if (@object == null) yield break;

            foreach (var pi in @object.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (pi.PropertyType.FullName.Split('.', '+')
                    .Contains(@namespace, StringComparer.OrdinalIgnoreCase))
                {
                    var getMethod = pi.GetGetMethod();
                    if (getMethod.ReturnType.IsArray)
                    {
                        var array = (Array) getMethod.Invoke(@object, null);
                        foreach (var sp in
                            array.Cast<object>().SelectMany(element => wasGetProperties(element, @namespace)))
                        {
                            yield return sp;
                        }
                    }
                    foreach (
                        var sp in
                            wasGetProperties(pi.GetValue(@object, null), @namespace))
                    {
                        yield return sp;
                    }
                }
                yield return new KeyValuePair<PropertyInfo, object>(pi, @object);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     This is a wrapper for both FieldInfo and PropertyInfo SetValue.
        /// </summary>
        /// <param name="info">either a FieldInfo or PropertyInfo</param>
        /// <param name="object">the object to set the value on</param>
        /// <param name="value">the value to set</param>
        public static void wasSetInfoValue<TK, TV>(TK info, ref TV @object, object value)
        {
            object o = @object;
            var fi = (object) info as FieldInfo;
            if (fi != null)
            {
                fi.SetValue(o, value);
                @object = (TV) o;
                return;
            }
            var pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                pi.SetValue(o, value, null);
                @object = (TV) o;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     This is a wrapper for both FieldInfo and PropertyInfo GetValue.
        /// </summary>
        /// <param name="info">either a FieldInfo or PropertyInfo</param>
        /// <param name="value">the object to get from</param>
        /// <returns>the value of the field or property</returns>
        public static object wasGetInfoValue<T>(T info, object value)
        {
            var fi = (object) info as FieldInfo;
            if (fi != null)
            {
                return fi.GetValue(value);
            }
            var pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                if (pi.GetIndexParameters().Any())
                {
                    return value;
                }
                return pi.GetValue(value, null);
            }
            return null;
        }
    }
}