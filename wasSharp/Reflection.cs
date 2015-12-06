///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace wasSharp
{
    public static class Reflection
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Retrieves an attribute of type T from an enumeration.
        /// </summary>
        /// <returns>an attribute of type T</returns>
        public static T GetAttributeFromEnumValue<T>(Enum value) where T : Attribute
        {
            return (T) value.GetType()
                .GetRuntimeField(value.ToString())
                .GetCustomAttributes(typeof (T), false)
                .SingleOrDefault();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the attributes of type T of an enumeration.
        /// </summary>
        /// <typeparam name="T">the attribute to retrieve</typeparam>
        /// <returns>a list of attributes</returns>
        public static IEnumerable<T> GetEnumAttributes<T>(Enum e) where T : Attribute
        {
            return e.GetType().GetRuntimeFields()
                .AsParallel()
                .Select(o => GetAttributeFromEnumValue<T>((Enum) o.GetValue(Activator.CreateInstance<T>())));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the field names of an enumeration.
        /// </summary>
        /// <returns>the field names</returns>
        public static IEnumerable<string> GetEnumNames<T>()
        {
            return
                typeof (T).GetRuntimeFields()
                    .AsParallel()
                    .Select(o => o.GetCustomAttribute(typeof (NameAttribute), false))
                    .Select(o => (o as NameAttribute)?.Name)
                    .Where(o => !string.IsNullOrEmpty(o))
                    .Select(o => o);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the values of an enumeration.
        /// </summary>
        /// <returns>the values of the enumeration</returns>
        public static IEnumerable<T> GetEnumValues<T>()
        {
            return Enum.GetValues(typeof (T)).Cast<object>().Select(value => (T) value);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description from an enumeration value.
        /// </summary>
        /// <param name="value">an enumeration value</param>
        /// <returns>the description or the empty string</returns>
        public static string GetNameFromEnumValue(Enum value)
        {
            NameAttribute attribute = value.GetType()
                .GetRuntimeField(value.ToString())
                .GetCustomAttributes(typeof (NameAttribute), false)
                .SingleOrDefault() as NameAttribute;
            return attribute?.Name;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description from an enumeration value.
        /// </summary>
        /// <param name="value">an enumeration value</param>
        /// <returns>the description or the empty string</returns>
        public static string GetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetRuntimeField(value.ToString())
                .GetCustomAttributes(typeof (DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute?.Description;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get enumeration value from its name attribute.
        /// </summary>
        /// <typeparam name="T">the enumeration type</typeparam>
        /// <param name="name">the description of a member</param>
        /// <returns>the value or the default of T if case no name attribute found</returns>
        public static T GetEnumValueFromName<T>(string name)
        {
            var field = typeof (T).GetRuntimeFields()
                .AsParallel().SelectMany(f => f.GetCustomAttributes(
                    typeof (NameAttribute), false), (
                        f, a) => new {Field = f, Att = a}).SingleOrDefault(a => ((NameAttribute) a.Att)
                            .Name.Equals(name));
            return field != null ? (T) field.Field.GetValue(Activator.CreateInstance<T>()) : default(T);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description of structure member.
        /// </summary>
        /// <typeparam name="T">the type of the structure to search</typeparam>
        /// <param name="structure">the structure to search</param>
        /// <param name="item">the value of the item to search</param>
        /// <returns>the description or the empty string</returns>
        public static string GetStructureMemberName<T>(T structure, object item) where T : struct
        {
            var field = typeof (T).GetRuntimeFields()
                .AsParallel().SelectMany(f => f.GetCustomAttributes(typeof (NameAttribute), false),
                    (f, a) => new {Field = f, Att = a}).SingleOrDefault(f => f.Field.GetValue(structure).Equals(item));
            return field != null ? ((NameAttribute) field.Att).Name : string.Empty;
        }

        /// <summary>
        ///     A generic name attribute.
        /// </summary>
        public class NameAttribute : Attribute
        {
            protected readonly string name;

            public NameAttribute(string name)
            {
                this.name = name;
            }

            public string Name => name;
        }

        /// <summary>
        ///     A generic description attribute.
        /// </summary>
        public class DescriptionAttribute : Attribute
        {
            protected readonly string description;

            public DescriptionAttribute(string description)
            {
                this.description = description;
            }

            public string Description => description;
        }
    }
}