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
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace wasSharp
{
    public static class Collections
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     A circular queue implementation based on linked lists.
        /// </summary>
        /// <typeparam name="T">the type of value to store</typeparam>
        public class CircularQueue<T>
        {
            private readonly LinkedList<T> Store = new LinkedList<T>();
            private LinkedListNode<T> CurrentNode = null;

            object SyncRoot = new object();

            public int Count
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return Store.Count;
                    }
                }
            }

            private T GetNext
            {
                get
                {
                    lock (SyncRoot)
                    {

                        if (CurrentNode == null)
                            return default(T);

                        T value = CurrentNode.Value;

                        switch (CurrentNode.Next != null)
                        {
                            case true:
                                CurrentNode = CurrentNode.Next;
                                break;
                            default:
                                CurrentNode = Store.First;
                                break;
                        }

                        return value;
                    }
                }
            }

            public IEnumerable<T> Items
            {
                get
                {
                    lock (SyncRoot)
                    {

                        if (CurrentNode == null)
                            yield break;

                        var node = CurrentNode;
                        do
                        {
                            yield return node.Value;
                            node = node.Next;
                        } while (node != null);
                    }
                }
            }

            public CircularQueue()
            {
                
            }

            public CircularQueue(IEnumerable<T> items)
            {
                Enqueue(items);
            }

            public CircularQueue(CircularQueue<T> queue)
            {
                lock (SyncRoot)
                {
                    lock (queue.SyncRoot)
                    {

                        foreach (var item in queue.Items)
                        {
                            Store.AddLast(item);
                        }

                        if (CurrentNode == null)
                            CurrentNode = Store.First;
                    }
                }
            }

            public void Enqueue(IEnumerable<T> items)
            {
                lock (SyncRoot)
                {
                    foreach (var i in items)
                        Store.AddLast(i);

                    if (CurrentNode == null)
                        CurrentNode = Store.First;
                }
            }

            public void Enqueue(T item)
            {
                lock (SyncRoot)
                {

                    Store.AddLast(item);

                    if (CurrentNode == null)
                        CurrentNode = Store.First;
                }
            }

            public T Dequeue()
            {
                lock (SyncRoot)
                {
                    return GetNext;
                }
            }

            public IEnumerable<T> Dequeue(int count = 1)
            {
                if (count <= 0)
                    yield break;

                lock (SyncRoot)
                {
                    if(CurrentNode == null)
                        yield break;

                    do
                    {
                        yield return GetNext;
                    } while (--count != 0);
                }
            }

            public void Clear()
            {
                lock (SyncRoot)
                {
                    Store.Clear();

                    CurrentNode = null;
                }
            }

            public bool Contains(T item)
            {
                lock (SyncRoot)
                {
                    return Store.Contains(item);
                }
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                lock (SyncRoot)
                {
                    Store.CopyTo(array, arrayIndex);
                }
            }

            public bool Remove(T item)
            {
                lock (SyncRoot)
                {
                    var node = Store.Find(item);
                    if (node == null)
                        return false;
                    if (CurrentNode.Equals(node))
                    {
                        switch (node.Next != null)
                        {
                            case true:
                                CurrentNode = node.Next;
                                break;
                            default:
                                CurrentNode = Store.First;
                                break;
                        }
                    }   
                    Store.Remove(node);
                    return true;
                }
            }

            public void RemoveAll(IEnumerable<T> items)
            {
                var itemSet = new HashSet<T>(items);
                lock (SyncRoot)
                {
                    var node = CurrentNode;
                    do
                    {
                        var next = node.Next;
                        if (itemSet.Contains(node.Value))
                        {
                            switch (next != null)
                            {
                                case true:
                                    CurrentNode = next;
                                    break;
                                default:
                                    CurrentNode = Store.First;
                                    break;
                            }
                            Store.Remove(node);
                        }
                        node = next;
                    } while (node != null);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     A collection that maps ranges to values with O(1) complexity
        ///     lookups and O(n) insertions.
        /// </summary>
        /// <typeparam name="T">the type of value to store</typeparam>
        public class RangeCollection<T> : IEnumerable
        {
            private Dictionary<int, T> map = null;

            public RangeCollection(int min, int max)
            {
                map = new Dictionary<int, T>(max - min);
            }

            /// <summary>
            ///     Map a value to a range.
            /// </summary>
            /// <param name="Value">the value for the range</param>
            /// <param name="min">the minimal range</param>
            /// <param name="max">the maximal range</param>
            public void Add(T Value, int min, int max)
            {
                foreach (var i in Enumerable.Range(min, max - min + 1))
                {
                    map.Add(i, Value);
                }
            }

            public IEnumerator GetEnumerator()
            {
                return ((IEnumerable)map).GetEnumerator();
            }

            public T this[int x]
            {
                get
                {
                    T value;
                    return map.TryGetValue(x, out value) ? value : default(T);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns true of an enumerable contains more than one element.
        /// </summary>
        /// <typeparam name="T">the type of the enumeration</typeparam>
        /// <param name="e">the enumeration</param>
        /// <returns>true if enumeration contains more than one element</returns>
        /// <remarks>O(2) worst case</remarks>
        public static bool Some<T>(this IEnumerable<T> e)
        {
            int i = 0;
            using (var iter = e.GetEnumerator())
            {
                while (iter.MoveNext())
                {
                    if (++i > 1)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        ///     Compares two dictionaries for equality.
        /// </summary>
        /// <typeparam name="TKey">key type</typeparam>
        /// <typeparam name="TValue">value type</typeparam>
        /// <param name="dictionary">dictionary to compare</param>
        /// <param name="otherDictionary">dictionary to compare to</param>
        /// <returns>true if the dictionaries contain the same elements</returns>
        public static bool ContentEquals<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            IDictionary<TKey, TValue> otherDictionary)
        {
            return
                (dictionary ?? new Dictionary<TKey, TValue>()).Count.Equals(
                    (otherDictionary ?? new Dictionary<TKey, TValue>()).Count) &&
                (otherDictionary ?? new Dictionary<TKey, TValue>())
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual((dictionary ?? new Dictionary<TKey, TValue>())
                        .OrderBy(kvp => kvp.Key));
        }

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

            public ObservableHashSet(IEnumerable<T> list)
            {
                UnionWith(list);
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
                if (removed)
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                return removed;
            }

            public int Count => store.Count;

            public bool IsReadOnly => false;

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            public void UnionWith(IEnumerable<T> list)
            {
                var added = new List<T>(list.Except(store));
                store.UnionWith(added);
                if (!IsVirgin && added.Any())
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added));
                IsVirgin = false;
            }

            private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
            {
                CollectionChanged?.Invoke(this, args);
            }

            public void ExceptWith(IEnumerable<T> list)
            {
                var removed = new List<T>(list.Intersect(store));
                store.ExceptWith(removed);
                if (!IsVirgin && removed.Any())
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                        removed));
                IsVirgin = false;
            }

            public void RemoveWhere(Func<T, bool> func)
            {
                var removed = new List<T>(store.Where(func));
                store.ExceptWith(removed);
                if (!IsVirgin && removed.Any())
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                        removed));
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

        /// <summary>
        ///     A serializable sorted dictionary class.
        /// </summary>
        /// <typeparam name="TKey">the key</typeparam>
        /// <typeparam name="TValue">the value</typeparam>
        [XmlRoot("SortedDictionary")]
        public class SerializableSortedDictionary<TKey, TValue>
            : SortedDictionary<TKey, TValue>, IXmlSerializable
        {
            #region IXmlSerializable Members

            public SerializableSortedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> kvp)
            {
                foreach (var i in kvp)
                {
                    Add(i.Key, i.Value);
                }
            }

            public SerializableSortedDictionary()
            {
            }

            /// <summary>
            ///     Deep-clones the serializable dictionary.
            /// </summary>
            /// <returns>a deep clone of the original dictionary</returns>
            public SerializableSortedDictionary<TKey, TValue> Clone()
            {
                SerializableSortedDictionary<TKey, TValue> clone;
                try
                {
                    using (var writer = new MemoryStream())
                    {
                        var serializer =
                            new XmlSerializer(
                                typeof(SerializableDictionary<TKey, TValue>));
                        serializer.Serialize(writer, this);
                        writer.Seek(0, SeekOrigin.Begin);
                        clone = (SerializableSortedDictionary<TKey, TValue>)
                            new XmlSerializer(
                                typeof(SerializableSortedDictionary<TKey, TValue>))
                                .Deserialize(writer);
                    }
                }
                /* cloning failed so return an empty dictionary */
                catch (Exception)
                {
                    clone = new SerializableSortedDictionary<TKey, TValue>();
                }
                return clone;
            }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                var keySerializer = new XmlSerializer(typeof(TKey));
                var valueSerializer = new XmlSerializer(typeof(TValue));

                var wasEmpty = reader.IsEmptyElement;
                reader.Read();

                if (wasEmpty)
                    return;

                while (!reader.NodeType.Equals(XmlNodeType.EndElement))
                {
                    reader.ReadStartElement("Item");

                    reader.ReadStartElement("Key");
                    var key = (TKey)keySerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    reader.ReadStartElement("Value");
                    var value = (TValue)valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    Add(key, value);

                    reader.ReadEndElement();
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }

            public void WriteXml(XmlWriter writer)
            {
                var keySerializer = new XmlSerializer(typeof(TKey));
                var valueSerializer = new XmlSerializer(typeof(TValue));

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