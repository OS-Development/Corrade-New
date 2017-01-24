///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp.Collections.Generic;

namespace Corrade.Structures
{
    [Serializable]
    public class SerializedNotification
    {
        public enum ResolveDestination
        {
            NONE = 0,
            UUID,
            NAME
        }

        public enum ResolveType
        {
            NONE = 0,
            AGENT,
            GROUP,
            ROLE
        }

        [XmlElement(ElementName = "Notification")]
        public Configuration.Notifications Notification { get; set; }

        [XmlElement(ElementName = "NotificationParameters")]
        public SerializableDictionary<string, HashSet<Parameter>> NotificationParameters { get; set; }

        [XmlIgnore]
        public string XML
        {
            get
            {
                using (var writer = new StringWriter())
                {
                    var serializer = new XmlSerializer(GetType());
                    serializer.Serialize(writer, this);
                    return writer.ToString();
                }
            }
        }

        /// <summary>
        ///     Serializes to a file.
        /// </summary>
        /// <param name="FileName">File path of the new xml file</param>
        public void SaveToFile(string FileName)
        {
            using (
                var fileStream = new FileStream(FileName, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true)
                )
            {
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.Write(XML);
                    writer.Flush();
                }
            }
        }

        /// <summary>
        ///     Load an object from an xml file
        /// </summary>
        /// <param name="FileName">Xml file name</param>
        /// <param name="o">the object to load to</param>
        /// <returns>The object created from the xml file</returns>
        public static SerializedNotification LoadFromFile(string FileName)
        {
            if (!File.Exists(FileName))
                return null;

            using (
                var fileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    var serializer = new XmlSerializer(typeof(SerializedNotification));
                    return serializer.Deserialize(streamReader) as SerializedNotification;
                }
            }
        }

        [XmlRoot(ElementName = "BayesClassify")]
        public class BayesClassify
        {
            [XmlElement(ElementName = "Name")]
            public string Name { get; set; }
        }

        [XmlRoot(ElementName = "IdentifyLanguage")]
        public class IdentifyLanguage
        {
            [XmlElement(ElementName = "Name")]
            public string Name { get; set; }
        }

        [XmlRoot(ElementName = "Condition")]
        public class Condition
        {
            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }

            [XmlElement(Type = typeof(ChatSourceType)),
             XmlElement(Type = typeof(int)),
             XmlElement(Type = typeof(uint)),
             XmlElement(Type = typeof(bool)),
             XmlElement(Type = typeof(string)),
             XmlElement(Type = typeof(InstantMessageDialog))]
            public object Value { get; set; }
        }

        [XmlRoot(ElementName = "Resolve")]
        public class Resolve
        {
            [XmlElement(ElementName = "UUID")]
            public string UUID { get; set; }

            [XmlElement(ElementName = "ResolveType")]
            public ResolveType ResolveType { get; set; }

            [XmlElement(ElementName = "Type")]
            public ResolveDestination ResolveDestination { get; set; }
        }

        [XmlRoot(ElementName = "ToEnumMemberName")]
        public class ToEnumMemberName
        {
            [XmlElement(ElementName = "Assembly")]
            public string Assembly { get; set; }

            [XmlElement(ElementName = "Type")]
            public string Type { get; set; }
        }

        [XmlRoot(ElementName = "NameSplit")]
        public class NameSplit
        {
            [XmlElement(ElementName = "First")]
            public string First { get; set; }

            [XmlElement(ElementName = "Last")]
            public string Last { get; set; }

            [XmlElement(ElementName = "Condition")]
            public Condition Condition { get; set; }
        }

        [XmlRoot(ElementName = "ConditionalSubstitution")]
        public class ConditionalSubstitution
        {
            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }

            [XmlElement(Type = typeof(bool)),
             XmlElement(Type = typeof(int)),
             XmlElement(Type = typeof(uint)),
             XmlElement(Type = typeof(InstantMessageDialog))]
            public object Check { get; set; }

            [XmlElement(ElementName = "Value")]
            public string Value { get; set; }

            [XmlElement(ElementName = "Type")]
            public string Type { get; set; }
        }

        [XmlRoot(ElementName = "TernarySubstitution")]
        public class TernarySubstitution
        {
            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }

            [XmlElement(Type = typeof(bool)),
             XmlElement(Type = typeof(int)),
             XmlElement(Type = typeof(uint)),
             XmlElement(Type = typeof(string))]
            public object Value { get; set; }

            [XmlElement(ElementName = "Left")]
            public string Left { get; set; }

            [XmlElement(ElementName = "Right")]
            public string Right { get; set; }

            [XmlElement(ElementName = "Type")]
            public string Type { get; set; }

            [XmlElement(ElementName = "Source")]
            public string Source { get; set; }
        }

        [XmlRoot(ElementName = "Method")]
        public class Method
        {
            [XmlElement(ElementName = "Type")]
            public string Type { get; set; }

            [XmlElement(ElementName = "Name")]
            public string Name { get; set; }

            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }

            [XmlElement(ElementName = "Parameters")]
            public SerializableDictionary<string, string> Parameters { get; set; }

            [XmlElement(ElementName = "Assembly")]
            public string Assembly { get; set; }
        }

        [XmlRoot(ElementName = "GetValue")]
        public class GetValue
        {
            [XmlElement(ElementName = "Get")]
            public string Get { get; set; }

            [XmlElement(ElementName = "Value")]
            public string Value { get; set; }

            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }
        }

        [XmlRoot(ElementName = "ToLower")]
        public class ToLower
        {
            [XmlElement(ElementName = "Culture")]
            public string Culture { get; set; }
        }

        [XmlRoot(ElementName = "Processing")]
        public class Processing
        {
            [XmlElement(ElementName = "NameSplit")]
            public NameSplit NameSplit { get; set; }

            [XmlElement(ElementName = "IdentifyLanguage")]
            public IdentifyLanguage IdentifyLanguage { get; set; }

            [XmlElement(ElementName = "BayesClassify")]
            public BayesClassify BayesClassify { get; set; }

            [XmlElement(ElementName = "Resolve")]
            public Resolve Resolve { get; set; }

            [XmlElement(ElementName = "Method")]
            public Method Method { get; set; }

            [XmlElement(ElementName = "ToEnumMemberName")]
            public ToEnumMemberName ToEnumMemberName { get; set; }

            [XmlElement(ElementName = "TernarySubstitution")]
            public TernarySubstitution TernarySubstitution { get; set; }

            [XmlElement(ElementName = "ConditionalSubstitution")]
            public ConditionalSubstitution ConditionalSubstitution { get; set; }

            [XmlElement(ElementName = "GetValue")]
            public GetValue GetValue { get; set; }

            [XmlElement(ElementName = "ToLower")]
            public ToLower ToLower { get; set; }
        }

        [XmlRoot(ElementName = "Parameter")]
        public class Parameter
        {
            [XmlElement(ElementName = "Path")]
            public string Path { get; set; }

            [XmlElement(ElementName = "Name")]
            public string Name { get; set; }

            [XmlElement(ElementName = "Processing")]
            public List<Processing> Processing { get; set; }

            [XmlElement(ElementName = "Value")]
            public string Value { get; set; }

            [XmlElement(ElementName = "Type")]
            public string Type { get; set; }

            [XmlElement(ElementName = "Condition")]
            public List<Condition> Condition { get; set; }
        }
    }
}