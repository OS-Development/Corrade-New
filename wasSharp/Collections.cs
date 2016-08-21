///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace wasSharp
{
    public static class Collections
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     An implementation of an observable HashSet.
        /// </summary>
        /// <typeparam name="T">the object type</typeparam>
        public class ObservableHashSet<T> : ICollection<T>, INotifyCollectionChanged
        {
            private readonly HashSet<T> store = new HashSet<T>();

            public ObservableHashSet(HashSet<T> set)
            {
                UnionWith(set);
            }

            public ObservableHashSet()
            {
            }

            public ObservableHashSet(T item)
            {
                Add(item);
            }

            public ObservableHashSet(ObservableHashSet<T> other)
            {
                UnionWith(other);
            }

            public bool IsVirgin { get; private set; } = true;

            public IEnumerator<T> GetEnumerator()
            {
                return store.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(T item)
            {
                store.Add(item);
                IsVirgin = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            }

            public void Clear()
            {
                store.Clear();
                if (!IsVirgin)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                IsVirgin = false;
            }

            public bool Contains(T item)
            {
                return store.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                store.CopyTo(array, arrayIndex);
            }

            public bool Remove(T item)
            {
                var removed = store.Remove(item);
                IsVirgin = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                return removed;
            }

            public int Count => store.Count;

            public bool IsReadOnly => false;

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            public void AddRange(IEnumerable<T> list)
            {
                foreach (var item in list)
                    store.Add(item);
                if (!IsVirgin)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                IsVirgin = false;
            }

            public void UnionWith(IEnumerable<T> list)
            {
                store.UnionWith(list);
                if (!IsVirgin)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                IsVirgin = false;
            }

            private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
            {
                CollectionChanged?.Invoke(this, args);
            }

            public void ExceptWith(HashSet<T> other)
            {
                store.ExceptWith(other);
                if (!IsVirgin)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                IsVirgin = false;
            }
        }

        /// <summary>
        ///     An observable collection allowing the add of a range of items.
        /// </summary>
        /// <typeparam name="T">the collection type</typeparam>
        public class ExtendedObservableCollection<T> : ObservableCollection<T>
        {
            private bool _suppressNotification;

            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                if (!_suppressNotification)
                    base.OnCollectionChanged(e);
            }

            public void AddRange(IEnumerable<T> list)
            {
                if (list == null)
                    throw new ArgumentNullException(nameof(list));

                _suppressNotification = true;

                foreach (var item in list)
                {
                    Add(item);
                }
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

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

            public SerializableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> kvp)
            {
                foreach (var i in kvp)
                {
                    Add(i.Key, i.Value);
                }
            }

            public SerializableDictionary()
            {
            }

            /// <summary>
            ///     Deep-clones the serializable dictionary.
            /// </summary>
            /// <returns>a deep clone of the original dictionary</returns>
            public SerializableDictionary<TKey, TValue> Clone()
            {
                SerializableDictionary<TKey, TValue> clone;
                try
                {
                    using (var writer = new MemoryStream())
                    {
                        var serializer =
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
                var keySerializer = new XmlSerializer(typeof (TKey));
                var valueSerializer = new XmlSerializer(typeof (TValue));

                var wasEmpty = reader.IsEmptyElement;
                reader.Read();

                if (wasEmpty)
                    return;

                while (!reader.NodeType.Equals(XmlNodeType.EndElement))
                {
                    reader.ReadStartElement("Item");

                    reader.ReadStartElement("Key");
                    var key = (TKey) keySerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    reader.ReadStartElement("Value");
                    var value = (TValue) valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    Add(key, value);

                    reader.ReadEndElement();
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }

            public void WriteXml(XmlWriter writer)
            {
                var keySerializer = new XmlSerializer(typeof (TKey));
                var valueSerializer = new XmlSerializer(typeof (TValue));

                foreach (var key in Keys)
                {
                    writer.WriteStartElement("Item");

                    writer.WriteStartElement("Key");
                    keySerializer.Serialize(writer, key);
                    writer.WriteEndElement();

                    writer.WriteStartElement("Value");
                    var value = this[key];
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }
            }

            #endregion
        }
    }
}