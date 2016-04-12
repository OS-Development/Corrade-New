///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace wasSharp
{
    public static class Collections
    {
        /// <summary>
        ///     A serializable dictionary class.
        /// </summary>
        /// <typeparam name="TKey">the key</typeparam>
        /// <typeparam name="TValue">the value</typeparam>
        [XmlRoot("Dictionary")]
        public class SerializableDictionary<TKey, TValue>
            : Dictionary<TKey, TValue>, IXmlSerializable
        {
            #region IXmlSerializable Members

            /// <summary>
            ///     Deep-clones the serializable dictionary.
            /// </summary>
            /// <returns>a deep clone of the original dictionary</returns>
            public SerializableDictionary<TKey, TValue> Clone()
            {
                SerializableDictionary<TKey, TValue> clone;
                try
                {
                    using (MemoryStream writer = new MemoryStream())
                    {
                        XmlSerializer serializer =
                            new XmlSerializer(
                                typeof (SerializableDictionary<TKey, TValue>));
                        serializer.Serialize(writer, this);
                        writer.Seek(0, SeekOrigin.Begin);
                        clone = (SerializableDictionary<TKey, TValue>)
                            new XmlSerializer(
                                typeof (SerializableDictionary<TKey, TValue>))
                                .Deserialize(writer);
                    }
                }
                    /* cloning failed so return an empty dictionary */
                catch (Exception)
                {
                    clone = new SerializableDictionary<TKey, TValue>();
                }
                return clone;
            }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                XmlSerializer keySerializer = new XmlSerializer(typeof (TKey));
                XmlSerializer valueSerializer = new XmlSerializer(typeof (TValue));

                bool wasEmpty = reader.IsEmptyElement;
                reader.Read();

                if (wasEmpty)
                    return;

                while (!reader.NodeType.Equals(XmlNodeType.EndElement))
                {
                    reader.ReadStartElement("Item");

                    reader.ReadStartElement("Key");
                    TKey key = (TKey) keySerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    reader.ReadStartElement("Value");
                    TValue value = (TValue) valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    Add(key, value);

                    reader.ReadEndElement();
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }

            public void WriteXml(XmlWriter writer)
            {
                XmlSerializer keySerializer = new XmlSerializer(typeof (TKey));
                XmlSerializer valueSerializer = new XmlSerializer(typeof (TValue));

                foreach (TKey key in Keys)
                {
                    writer.WriteStartElement("Item");

                    writer.WriteStartElement("Key");
                    keySerializer.Serialize(writer, key);
                    writer.WriteEndElement();

                    writer.WriteStartElement("Value");
                    TValue value = this[key];
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }
            }

            #endregion
        }
    }
}