///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Mono.Unix;
using Mono.Unix.Native;
using OpenMetaverse;

#endregion

namespace Corrade
{
    public partial class Corrade : ServiceBase
    {
        public delegate bool EventHandler(NativeMethods.CtrlType ctrlType);

        /// <summary>
        ///     Corrade version sent to the simulator.
        /// </summary>
        private static readonly string CORRADE_VERSION = Assembly.GetEntryAssembly().GetName().Version.ToString();

        /// <summary>
        ///     Corrade compile date.
        /// </summary>
        private static readonly string CORRADE_COMPILE_DATE = new DateTime(2000, 1, 1).Add(new TimeSpan(
            TimeSpan.TicksPerDay*Assembly.GetEntryAssembly().GetName().Version.Build + // days since 1 January 2000
            TimeSpan.TicksPerSecond*2*Assembly.GetEntryAssembly().GetName().Version.Revision)).ToLongDateString();

        /// <summary>
        ///     Semaphores that sense the state of the connection. When any of these semaphores fail,
        ///     Corrade does not consider itself connected anymore and terminates.
        /// </summary>
        private static readonly Dictionary<char, ManualResetEvent> ConnectionSemaphores = new Dictionary
            <char, ManualResetEvent>
        {
            {'l', new ManualResetEvent(false)},
            {'s', new ManualResetEvent(false)},
            {'u', new ManualResetEvent(false)}
        };

        private static Thread programThread;

        private static readonly EventLog CorradeLog = new EventLog();

        private static readonly GridClient Client = new GridClient();

        private static readonly object FileLock = new object();

        private static readonly object DatabaseLock = new object();

        private static readonly Dictionary<string, object> DatabaseLocks = new Dictionary<string, object>();

        private static readonly object GroupNotificationsLock = new object();

        private static readonly HashSet<Notification> GroupNotifications =
            new HashSet<Notification>();

        private static readonly object TeleportLock = new object();

        public static EventHandler ConsoleEventHandler;

        private static readonly System.Action ActivateCurrentLandGroup = () => new Thread(() =>
        {
            Thread.Sleep(Client.Network.CurrentSim.Stats.LastLag);
            Parcel parcel = null;
            if (!GetParcelAtPosition(Client.Network.CurrentSim, Client.Self.SimPosition, ref parcel)) return;
            UUID groupUUID = Configuration.GROUPS.FirstOrDefault(o => o.UUID.Equals(parcel.GroupID)).UUID;
            if (!groupUUID.Equals(UUID.Zero))
            {
                Client.Groups.ActivateGroup(groupUUID);
            }
        }).Start();

        public Corrade()
        {
            if (!Environment.UserInteractive)
            {
                ServiceName = "Corrade";
                CorradeLog.Source = ServiceName;
                CorradeLog.Log = "Application";
                ((ISupportInitialize) (CorradeLog)).BeginInit();
                if (!EventLog.SourceExists(CorradeLog.Source))
                {
                    EventLog.CreateEventSource(CorradeLog.Source, CorradeLog.Log);
                }
                ((ISupportInitialize) (CorradeLog)).EndInit();
            }
        }

        private static bool ConsoleCtrlCheck(NativeMethods.CtrlType ctrlType)
        {
            KeyValuePair<char, ManualResetEvent> semaphore = ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('u'));
            if (semaphore.Value != null)
            {
                semaphore.Value.Set();
            }

            // Wait for threads to finish.
            Thread.Sleep(Configuration.SERVICES_TIMEOUT);
            return true;
        }

        // This extension method is broken out so you can use a similar pattern with
        // other MetaData elements in the future. This is your base method for each.
        private static T GetAttribute<T>(Enum value) where T : Attribute
        {
            System.Type type = value.GetType();
            MemberInfo[] memberInfo = type.GetMember(value.ToString());
            object[] attributes = memberInfo[0].GetCustomAttributes(typeof (T), false);
            return (T) attributes[0];
        }

        // This method creates a specific call to the above method, requesting the
        // Description MetaData attribute.
        private static string GetEnumDescription(Enum value)
        {
            DescriptionAttribute attribute = GetAttribute<DescriptionAttribute>(value);
            return attribute == null ? value.ToString() : attribute.Description;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets an enumeration value from a description.
        /// </summary>
        /// <typeparam name="T">the enumeration to search in</typeparam>
        /// <param name="description">the description to search for</param>
        /// <returns>the value of the field corresponding to the description</returns>
        private static uint wasGetEnumValueFromDescription<T>(string description)
        {
            return (from fi in typeof (T).GetFields(BindingFlags.Static | BindingFlags.Public)
                where GetEnumDescription((Enum) fi.GetValue(null)).Equals(description)
                select (uint) fi.GetValue(null)).FirstOrDefault();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the field descriptions of an enumeration.
        /// </summary>
        /// <returns>the field descriptions</returns>
        private static IEnumerable<string> wasGetEnumDescriptions<T>()
        {
            return typeof (T).GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(o => GetEnumDescription((Enum) o.GetValue(null)));
        }

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
        private static IEnumerable<KeyValuePair<FieldInfo, object>> wasGetFields(object @object, string @namespace)
        {
            if (@object == null) yield break;

            foreach (FieldInfo fi in @object.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (fi.FieldType.FullName.Split(new[] {'.', '+'})
                    .Contains(@namespace, StringComparer.InvariantCultureIgnoreCase))
                {
                    foreach (KeyValuePair<FieldInfo, object> sf in wasGetFields(fi.GetValue(@object), @namespace))
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
        private static IEnumerable<KeyValuePair<PropertyInfo, object>> wasGetProperties(object @object,
            string @namespace)
        {
            if (@object == null) yield break;

            foreach (PropertyInfo pi in @object.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (pi.PropertyType.FullName.Split(new[] {'.', '+'})
                    .Contains(@namespace, StringComparer.InvariantCultureIgnoreCase))
                {
                    foreach (
                        KeyValuePair<PropertyInfo, object> sp in
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
        private static void wasSetInfoValue<I, T>(I info, ref T @object, object value)
        {
            object o = @object;
            FieldInfo fi = (object) info as FieldInfo;
            if (fi != null)
            {
                fi.SetValue(o, value);
                @object = (T) o;
                return;
            }
            PropertyInfo pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                pi.SetValue(o, value, null);
                @object = (T) o;
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
        private static object wasGetInfoValue<T>(T info, object value)
        {
            FieldInfo fi = (object) info as FieldInfo;
            if (fi != null)
            {
                return fi.GetValue(value);
            }
            PropertyInfo pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                return pi.GetValue(value, null);
            }
            return null;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     The function gets the value from FieldInfo or PropertyInfo.
        /// </summary>
        /// <param name="info">a FieldInfo or PropertyInfo structure</param>
        /// <param name="value">the value to get</param>
        /// <returns>the value or values as a string</returns>
        private static IEnumerable<string> wasGetInfo(object info, object value)
        {
            if (info == null) yield break;
            object data = wasGetInfoValue(info, value);
            // Handle arrays
            Array list = data as Array;
            if (list != null)
            {
                IList array = (IList) data;
                if (array.Count.Equals(0)) yield break;
                foreach (object item in array)
                {
                    string itemValue = item.ToString();
                    if (string.IsNullOrEmpty(itemValue)) continue;
                    yield return itemValue;
                }
                yield break;
            }
            string @string = data.ToString();
            if (string.IsNullOrEmpty(@string)) yield break;
            yield return @string;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sets the value of FieldInfo or PropertyInfo.
        /// </summary>
        /// <typeparam name="T">the type to set</typeparam>
        /// <param name="info">a FieldInfo or PropertyInfo object</param>
        /// <param name="value">the object's value</param>
        /// <param name="setting">the value to set to</param>
        /// <param name="object">the object to set the values for</param>
        private static void wasSetInfo<T>(object info, object value, string setting, ref T @object)
        {
            if (info != null)
            {
                if (wasGetInfoValue(info, value) is string)
                {
                    wasSetInfoValue(info, ref @object, setting);
                }
                if (wasGetInfoValue(info, value) is UUID)
                {
                    UUID UUIDData;
                    if (!UUID.TryParse(setting, out UUIDData))
                    {
                        InventoryItem item = SearchInventoryItem(Client.Inventory.Store.RootFolder,
                            setting,
                            Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        UUIDData = item.UUID;
                    }
                    if (UUIDData.Equals(UUID.Zero))
                    {
                        throw new Exception(
                            GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                    }
                    wasSetInfoValue(info, ref @object, UUIDData);
                }
                if (wasGetInfoValue(info, value) is bool)
                {
                    bool boolData;
                    if (bool.TryParse(setting, out boolData))
                    {
                        wasSetInfoValue(info, ref @object, boolData);
                    }
                }
                if (wasGetInfoValue(info, value) is int)
                {
                    int intData;
                    if (int.TryParse(setting, out intData))
                    {
                        wasSetInfoValue(info, ref @object, intData);
                    }
                }
                if (wasGetInfoValue(info, value) is uint)
                {
                    uint uintData;
                    if (uint.TryParse(setting, out uintData))
                    {
                        wasSetInfoValue(info, ref @object, uintData);
                    }
                }
                if (wasGetInfoValue(info, value) is DateTime)
                {
                    DateTime dateTimeData;
                    if (DateTime.TryParse(setting, out dateTimeData))
                    {
                        wasSetInfoValue(info, ref @object, dateTimeData);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent has a set of powers for a group.
        /// </summary>
        /// <param name="agentUUID">the agent UUID</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="powers">a GroupPowers structure</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <returns>true if the agent has the powers</returns>
        private static bool HasGroupPowers(UUID agentUUID, UUID groupUUID, GroupPowers powers, int millisecondsTimeout)
        {
            bool hasPowers = false;
            ManualResetEvent avatarGroupsEvent = new ManualResetEvent(false);
            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
            {
                hasPowers =
                    args.Groups.Any(o => o.GroupID.Equals(groupUUID) && (o.GroupPowers & powers) != GroupPowers.None);
                avatarGroupsEvent.Set();
            };
            Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
            Client.Avatars.RequestAvatarProperties(agentUUID);
            if (!avatarGroupsEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                return false;
            }
            Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
            return hasPowers;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent referenced by an UUID is in a group
        ///     referenced by an UUID.
        /// </summary>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="groupUUID">the UUID of the groupt</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <returns>true if the agent is in the group</returns>
        private static bool AgentInGroup(UUID agentUUID, UUID groupUUID, int millisecondsTimeout)
        {
            bool agentInGroup = false;
            ManualResetEvent agentInGroupEvent = new ManualResetEvent(false);
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                agentInGroup = args.Members.Any(o => o.Value.ID.Equals(agentUUID));
                agentInGroupEvent.Set();
            };
            Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
            Client.Groups.RequestGroupMembers(groupUUID);
            if (!agentInGroupEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                return false;
            }
            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
            return agentInGroup;
        }

        /// <summary>
        ///     Used to check whether a group name matches a group password.
        /// </summary>
        /// <param name="groupName">the name of the group</param>
        /// <param name="password">the password for the group</param>
        /// <returns>true if the agent has authenticated</returns>
        private static bool Authenticate(string groupName, string password)
        {
            return Configuration.GROUPS.Any(
                o =>
                    o.Name.Equals(groupName, StringComparison.Ordinal) &&
                    password.Equals(o.Password, StringComparison.Ordinal));
        }

        /// <summary>
        ///     Used to check whether a group has certain permissions for Corrade.
        /// </summary>
        /// <param name="groupName">the name of the group</param>
        /// <param name="permission">the numeric Corrade permission</param>
        /// <returns>true if the group has permission</returns>
        private static bool HasCorradePermission(string groupName, int permission)
        {
            return permission != 0 &&
                   Configuration.GROUPS.Any(
                       o =>
                           o.Name.Equals(groupName, StringComparison.Ordinal) &&
                           (o.PermissionMask & permission) != 0);
        }

        /// <summary>
        ///     Used to check whether a group has a certain notification for Corrade.
        /// </summary>
        /// <param name="groupName">the name of the group</param>
        /// <param name="notification">the numeric Corrade notification</param>
        /// <returns>true if the group has the notification</returns>
        private static bool HasCorradeNotification(string groupName, int notification)
        {
            return notification != 0 &&
                   Configuration.GROUPS.Any(
                       o => o.Name.Equals(groupName, StringComparison.Ordinal) &&
                            (o.NotificationMask & notification) != 0);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches a group.
        /// </summary>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="group">a group object to store the group profile</param>
        /// <returns>true if the group was found and false otherwise</returns>
        private static bool RequestGroup(UUID groupUUID, int millisecondsTimeout, ref OpenMetaverse.Group group)
        {
            OpenMetaverse.Group localGroup = new OpenMetaverse.Group();
            ManualResetEvent GroupProfileEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (sender, args) =>
            {
                localGroup = args.Group;
                GroupProfileEvent.Set();
            };
            Client.Groups.GroupProfile += GroupProfileDelegate;
            Client.Groups.RequestGroupProfile(groupUUID);
            if (!GroupProfileEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Groups.GroupProfile -= GroupProfileDelegate;
                return false;
            }
            Client.Groups.GroupProfile -= GroupProfileDelegate;
            group = localGroup;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the parcel of a simulator given a position.
        /// </summary>
        /// <param name="simulator">the simulator containing the parcel</param>
        /// <param name="position">a position within the parcel</param>
        /// <param name="parcel">a parcel object where to store the found parcel</param>
        /// <returns>true if the parcel could be found</returns>
        private static bool GetParcelAtPosition(Simulator simulator, Vector3 position,
            ref Parcel parcel)
        {
            Parcel localParcel = null;
            ManualResetEvent RequestAllSimParcelsEvent = new ManualResetEvent(false);
            EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedDelegate =
                (sender, args) => RequestAllSimParcelsEvent.Set();
            Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedDelegate;
            int delay = simulator.Stats.LastLag != 0 ? simulator.Stats.LastLag : 100;
            Client.Parcels.RequestAllSimParcels(simulator, true, delay);
            // 65536 1x1 parcels in 256x256 region times the last lag 
            if (!RequestAllSimParcelsEvent.WaitOne(65536*delay, false))
            {
                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
                return false;
            }
            Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
            Client.Network.CurrentSim.Parcels.ForEach(currentParcel =>
            {
                if (!(position.X >= currentParcel.AABBMin.X) || !(position.X <= currentParcel.AABBMax.X) ||
                    !(position.Y >= currentParcel.AABBMin.Y) || !(position.Y <= currentParcel.AABBMax.Y))
                    return;
                localParcel = currentParcel;
            });
            if (localParcel == null)
                return false;
            parcel = localParcel;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named primitive in range (whether attachment or in-world).
        /// </summary>
        /// <param name="item">the name or UUID of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <returns>true if the primitive could be found</returns>
        private static bool FindPrimitive(string item, float range, int millisecondsTimeout, ref Primitive primitive)
        {
            UUID itemUUID;
            if (!UUID.TryParse(item, out itemUUID))
            {
                itemUUID = UUID.Zero;
            }
            Hashtable queue = new Hashtable();
            Client.Network.CurrentSim.ObjectsPrimitives.ForEach(o =>
            {
                switch (o.ParentID)
                {
                        // primitive is a parent and it is in range
                    case 0:
                        if (Vector3.Distance(o.Position, Client.Self.SimPosition) < range)
                        {
                            if (itemUUID.Equals(UUID.Zero))
                            {
                                queue.Add(o.ID, o.LocalID);
                                break;
                            }
                            if (!itemUUID.Equals(UUID.Zero) && o.ID.Equals(itemUUID))
                            {
                                queue.Add(o.ID, o.LocalID);
                            }
                        }
                        break;
                        // primitive is a child
                    default:
                        // find the parent of the primitive
                        Primitive parent = o;
                        do
                        {
                            Primitive closure = parent;
                            Primitive ancestor =
                                Client.Network.CurrentSim.ObjectsPrimitives.Find(p => p.LocalID.Equals(closure.ParentID));
                            if (ancestor == null) break;
                            parent = ancestor;
                        } while (!parent.ParentID.Equals(0));
                        // the parent primitive has no other parent
                        if (parent.ParentID.Equals(0))
                        {
                            // if the parent is in range, add the child
                            if (Vector3.Distance(parent.Position, Client.Self.SimPosition) < range)
                            {
                                if (itemUUID.Equals(UUID.Zero))
                                {
                                    queue.Add(o.ID, o.LocalID);
                                    break;
                                }
                                if (!itemUUID.Equals(UUID.Zero) && o.ID.Equals(itemUUID))
                                {
                                    queue.Add(o.ID, o.LocalID);
                                }
                                break;
                            }
                        }
                        // check if an avatar is the parent of the parent primitive
                        Avatar parentAvatar =
                            Client.Network.CurrentSim.ObjectsAvatars.Find(p => p.LocalID.Equals(parent.ParentID));
                        // parent avatar not found, this should not happen
                        if (parentAvatar == null) break;
                        // check if the avatar is in range
                        if (Vector3.Distance(parentAvatar.Position, Client.Self.SimPosition) < range)
                        {
                            if (itemUUID.Equals(UUID.Zero))
                            {
                                queue.Add(o.ID, o.LocalID);
                                break;
                            }
                            if (!itemUUID.Equals(UUID.Zero) && o.ID.Equals(itemUUID))
                            {
                                queue.Add(o.ID, o.LocalID);
                            }
                        }
                        break;
                }
            });
            if (queue.Count.Equals(0))
                return false;
            ManualResetEvent ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler = (sender, args) =>
            {
                queue.Remove(args.Properties.ObjectID);
                if (!args.Properties.Name.Equals(item, StringComparison.Ordinal) &&
                    (itemUUID.Equals(UUID.Zero) || !args.Properties.ItemID.Equals(itemUUID)) && !queue.Count.Equals(0))
                    return;
                ObjectPropertiesEvent.Set();
            };
            Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
            Client.Objects.SelectObjects(Client.Network.CurrentSim, queue.Values.Cast<uint>().ToArray(), true);
            if (
                !ObjectPropertiesEvent.WaitOne(
                    millisecondsTimeout, false))
            {
                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                return false;
            }
            Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
            primitive =
                Client.Network.CurrentSim.ObjectsPrimitives.Find(
                    o =>
                        o.ID.Equals(itemUUID) ||
                        (o.Properties != null && o.Properties.Name.Equals(item, StringComparison.Ordinal)));
            return primitive != null;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get all worn attachments.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <returns>attachment points by primitives</returns>
        private static IEnumerable<KeyValuePair<AttachmentPoint, Primitive>> GetAttachments(int millisecondsTimeout)
        {
            HashSet<Primitive> primitives = new HashSet<Primitive>(Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
                o => o.ParentID.Equals(Client.Self.LocalID)));
            Hashtable primitiveQueue = new Hashtable(primitives.ToDictionary(o => o.ID, o => o.LocalID));
            ManualResetEvent ObjectPropertiesEvent = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler = (sender, args) =>
            {
                primitiveQueue.Remove(args.Properties.ObjectID);
                if (!primitiveQueue.Count.Equals(0)) return;
                ObjectPropertiesEvent.Set();
            };
            Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
            Client.Objects.SelectObjects(Client.Network.CurrentSim, primitiveQueue.Values.Cast<uint>().ToArray(), true);
            if (ObjectPropertiesEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                foreach (Primitive primitive in primitives)
                {
                    yield return new KeyValuePair<AttachmentPoint, Primitive>(
                        (AttachmentPoint) (((primitive.PrimData.State & 0xF0) >> 4) |
                                           ((primitive.PrimData.State & ~0xF0) << 4)),
                        primitive);
                }
            }
            Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the inventory wearables that are currently being worn.
        /// </summary>
        /// <param name="rootFolder">the folder to start the search from</param>
        /// <param name="millisecondsTimeout">the timeout for searching the wearables</param>
        /// <returns>key value pairs of wearables by name</returns>
        private static IEnumerable<KeyValuePair<WearableType, string>> GetWearables(InventoryBase rootFolder,
            int millisecondsTimeout)
        {
            HashSet<InventoryBase> contents =
                new HashSet<InventoryBase>(Client.Inventory.FolderContents(rootFolder.UUID, Client.Self.AgentID,
                    true, true, InventorySortOrder.ByName, millisecondsTimeout));
            foreach (InventoryBase inventory in contents)
            {
                InventoryFolder inventoryFolder = inventory as InventoryFolder;
                if (inventoryFolder == null)
                {
                    InventoryItem i = inventory as InventoryItem;
                    if (i == null) continue;

                    WearableType wearable = Client.Appearance.IsItemWorn(i);
                    switch (wearable)
                    {
                        case WearableType.Invalid:
                            break;
                        default:
                            yield return new KeyValuePair<WearableType, string>(wearable, i.Name);
                            break;
                    }
                    continue;
                }
                foreach (
                    KeyValuePair<WearableType, string> wearable in GetWearables(inventoryFolder, millisecondsTimeout))
                {
                    yield return new KeyValuePair<WearableType, string>(wearable.Key, wearable.Value);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Searches the current inventory for an item by name or UUID and
        ///     returns all the items that match the item name.
        /// </summary>
        /// <param name="rootFolder">a folder from which to search</param>
        /// <param name="item">the name  or UUID of the item to be found</param>
        /// <param name="millisecondsTimeout">timeout for the search</param>
        /// <returns>a list of items matching the item name</returns>
        private static IEnumerable<InventoryItem> SearchInventoryItem(InventoryBase rootFolder, string item,
            int millisecondsTimeout)
        {
            HashSet<InventoryBase> contents =
                new HashSet<InventoryBase>(Client.Inventory.FolderContents(rootFolder.UUID, Client.Self.AgentID,
                    true, true, InventorySortOrder.ByName, millisecondsTimeout));
            UUID itemUUID;
            UUID.TryParse(item, out itemUUID);
            foreach (InventoryBase inventory in contents)
            {
                InventoryItem i = inventory as InventoryItem;
                if (i != null)
                {
                    if (inventory.Name.Equals(item, StringComparison.Ordinal) ||
                        (!itemUUID.Equals(UUID.Zero) &&
                         (inventory.UUID.Equals(itemUUID) || i.AssetUUID.Equals(itemUUID))))
                    {
                        yield return i;
                    }
                }
                if (contents.Count == 0)
                    continue;
                InventoryFolder inventoryFolder = inventory as InventoryFolder;
                if (inventoryFolder == null)
                    continue;
                foreach (InventoryItem inventoryItem in SearchInventoryItem(inventoryFolder, item, millisecondsTimeout))
                {
                    yield return inventoryItem;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets all the items from an inventory folder and returns the items.
        /// </summary>
        /// <param name="rootFolder">a folder from which to search</param>
        /// <param name="folder">the folder to search for</param>
        /// <param name="millisecondsTimeout">timeout for the search</param>
        /// <returns>a list of items from folder</returns>
        private static IEnumerable<InventoryItem> GetInventoryFolderContents(InventoryBase rootFolder, string folder,
            int millisecondsTimeout)
        {
            HashSet<InventoryBase> contents =
                new HashSet<InventoryBase>(Client.Inventory.FolderContents(rootFolder.UUID, Client.Self.AgentID,
                    true, true, InventorySortOrder.ByName, millisecondsTimeout));
            foreach (InventoryBase inventory in contents)
            {
                InventoryFolder inventoryFolder = inventory as InventoryFolder;
                if (inventoryFolder == null)
                {
                    if (rootFolder.Name.Equals(folder, StringComparison.Ordinal))
                    {
                        yield return inventory as InventoryItem;
                    }
                    continue;
                }
                foreach (
                    InventoryItem inventoryItem in
                        GetInventoryFolderContents(inventoryFolder, folder, millisecondsTimeout))
                {
                    yield return inventoryItem;
                }
            }
        }

        /// <summary>
        ///     Posts messages to console or log-files.
        /// </summary>
        /// <param name="messages">a list of messages</param>
        private static void Feedback(params string[] messages)
        {
            List<string> output = new List<string>
            {
                "Corrade",
                string.Format(CultureInfo.InvariantCulture, "[{0}]",
                    DateTime.Now.ToString("dd-MM-yyyy HH:mm", DateTimeFormatInfo.InvariantInfo)),
            };

            output.AddRange(messages.Select(message => message));

            // Attempt to write to log file,
            try
            {
                lock (FileLock)
                {
                    using (
                        StreamWriter logWriter =
                            File.AppendText(Configuration.LOG_FILE))
                    {
                        logWriter.WriteLine(string.Join(" : ", output.ToArray()));
                        logWriter.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                // or fail and append the fail message.
                output.Add(string.Format(CultureInfo.InvariantCulture,
                    "The request could not be logged to {0} and returned the error message {1}.",
                    Configuration.LOG_FILE, e.Message));
            }

            if (!Environment.UserInteractive)
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        CorradeLog.WriteEntry(string.Join(" : ", output.ToArray()), EventLogEntryType.Information);
                        break;
                    case PlatformID.Unix:
                        Syscall.syslog(SyslogFacility.LOG_DAEMON, SyslogLevel.LOG_INFO,
                            string.Join(" : ", output.ToArray()));
                        break;
                }
                return;
            }

            Console.WriteLine(string.Join(" : ", output.ToArray()));
        }

        /// <summary>
        ///     Writes the logo and the version.
        /// </summary>
        private static void WriteLogo()
        {
            List<string> logo = new List<string>
            {
                Environment.NewLine,
                Environment.NewLine,
                @"       _..--=--..._  " + Environment.NewLine,
                @"    .-'            '-.  .-.  " + Environment.NewLine,
                @"   /.'              '.\/  /  " + Environment.NewLine,
                @"  |=-     Corrade    -=| (  " + Environment.NewLine,
                @"   \'.              .'/\  \  " + Environment.NewLine,
                @"    '-.,_____ _____.-'  '-'  " + Environment.NewLine,
                @"          [_____]=8  " + Environment.NewLine,
                @"               \  " + Environment.NewLine,
                @"                 Good day!  ",
                Environment.NewLine,
                Environment.NewLine,
                string.Format(CultureInfo.InvariantCulture,
                    Environment.NewLine + "Version: {0} Compiled: {1}" + Environment.NewLine, CORRADE_VERSION,
                    CORRADE_COMPILE_DATE),
                string.Format(CultureInfo.InvariantCulture,
                    "(c) Copyright 2013 Wizardry and Steamworks" + Environment.NewLine),
            };
            foreach (string line in logo)
            {
                Console.Write(line);
            }
            Console.WriteLine();
        }

        public static int Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length != 0)
                {
                    switch (args[0].ToLowerInvariant())
                    {
                        case "/install": // install Corrade as a service
                            return InstallService();

                        case "/uninstall": // uninstall the Corrade service
                            return UninstallService();

                        default: // unknown option
                            Console.WriteLine("Argument not recognized: {0}", args[0]);
                            return 1;
                    }
                }
                // run interactively and log to console
                Corrade corrade = new Corrade();
                corrade.OnStart(null);
                return 0;
            }

            // run as a standard service
            Run(new Corrade());
            return 0;
        }

        private static int InstallService()
        {
            try
            {
                // install the service with the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new[] {Assembly.GetExecutingAssembly().Location});
            }
            catch (Exception e)
            {
                if (e.InnerException != null && e.InnerException.GetType() == typeof (Win32Exception))
                {
                    Win32Exception wex = (Win32Exception) e.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service already installed!", wex.ErrorCode);
                    return wex.ErrorCode;
                }
                Console.WriteLine(e.ToString());
                return -1;
            }

            return 0;
        }

        private static int UninstallService()
        {
            try
            {
                // uninstall the service from the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new[] {"/u", Assembly.GetExecutingAssembly().Location});
            }
            catch (Exception e)
            {
                if (e.InnerException.GetType() == typeof (Win32Exception))
                {
                    Win32Exception wex = (Win32Exception) e.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service not installed!", wex.ErrorCode);
                    return wex.ErrorCode;
                }
                Console.WriteLine(e.ToString());
                return -1;
            }

            return 0;
        }

        protected override void OnStop()
        {
            base.OnStop();
            ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('u')).Value.Set();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            //Debugger.Break();
            programThread = new Thread(new Corrade().Program);
            programThread.Start();
        }

        // Main entry point.
        public void Program()
        {
            // Create a thread for signals.
            Thread TrapSignalsThread = null;
            // Branch on platform and set-up termination handlers.
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    if (Environment.UserInteractive)
                    {
                        // Setup console handler.
                        ConsoleEventHandler += ConsoleCtrlCheck;
                        NativeMethods.SetConsoleCtrlHandler(ConsoleEventHandler, true);
                        if (Environment.UserInteractive)
                        {
                            Console.CancelKeyPress +=
                                (sender, args) =>
                                    ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('u')).Value.Set();
                        }
                    }
                    break;
                case PlatformID.Unix:
                    TrapSignalsThread = new Thread(() =>
                    {
                        UnixSignal[] signals =
                        {
                            new UnixSignal(Signum.SIGTERM),
                            new UnixSignal(Signum.SIGINT)
                        };
                        UnixSignal.WaitAny(signals, -1);
                        ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('u')).Value.Set();
                    });
                    TrapSignalsThread.Start();
                    break;
            }
            // Set the current directory to the service directory.
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            // Load the configuration file.
            Configuration.Load("Corrade.ini");
            // Set-up watcher for dynamically reading the configuration file.
            FileSystemWatcher configurationWatcher = new FileSystemWatcher
            {
                Path = Directory.GetCurrentDirectory(),
                Filter = "Corrade.ini",
                NotifyFilter = NotifyFilters.LastWrite
            };
            configurationWatcher.Changed += HandleConfigurationFileChanged;
            configurationWatcher.EnableRaisingEvents = true;
            // Suppress standard OpenMetaverse logs, we have better ones.
            Settings.LOG_LEVEL = Helpers.LogLevel.None;
            Client.Settings.STORE_LAND_PATCHES = true;
            Client.Settings.ALWAYS_REQUEST_PARCEL_ACL = true;
            Client.Settings.ALWAYS_DECODE_OBJECTS = true;
            Client.Settings.ALWAYS_REQUEST_OBJECTS = true;
            Client.Settings.SEND_AGENT_APPEARANCE = true;
            Client.Settings.AVATAR_TRACKING = true;
            Client.Settings.OBJECT_TRACKING = true;
            Client.Settings.PARCEL_TRACKING = true;
            Client.Settings.POOL_PARCEL_DATA = true;
            Client.Settings.SEND_AGENT_UPDATES = true;
            Client.Settings.ENABLE_CAPS = true;
            Client.Settings.USE_ASSET_CACHE = true;
            Client.Settings.USE_INTERPOLATION_TIMER = true;
            Client.Settings.FETCH_MISSING_INVENTORY = true;
            // Install global event handlers.
            Client.Inventory.InventoryObjectOffered += HandleInventoryObjectOffered;
            Client.Appearance.AppearanceSet += HandleAppearanceSet;
            Client.Network.LoginProgress += HandleLoginProgress;
            Client.Network.SimConnected += HandleSimulatorConnected;
            Client.Network.Disconnected += HandleDisconnected;
            Client.Network.SimDisconnected += HandleSimulatorDisconnected;
            Client.Network.EventQueueRunning += HandleEventQueueRunning;
            Client.Network.SimChanged += HandleSimChanged;
            Client.Friends.FriendshipOffered += HandleFriendshipOffered;
            Client.Friends.FriendshipResponse += HandleFriendShipResponse;
            Client.Friends.FriendOnline += HandleFriendOnlineStatus;
            Client.Friends.FriendOffline += HandleFriendOnlineStatus;
            Client.Friends.FriendRightsUpdate += HandleFriendRightsUpdate;
            Client.Self.TeleportProgress += HandleTeleportProgress;
            Client.Self.ScriptQuestion += HandleScriptQuestion;
            Client.Self.AlertMessage += HandleAlertMessage;
            Client.Self.MoneyBalance += HandleMoneyBalance;
            Client.Self.ChatFromSimulator += HandleChatFromSimulator;
            Client.Self.ScriptDialog += HandleScriptDialog;
            Client.Objects.AvatarUpdate += HandleAvatarUpdate;
            Client.Objects.TerseObjectUpdate += HandleTerseObjectUpdate;
            // Each Instant Message is processed in its own thread.
            Client.Self.IM += HandleSelfIM;
            // Write the logo in interactive mode.
            if (Environment.UserInteractive)
            {
                WriteLogo();
            }
            // Check TOS
            if (!Configuration.TOS_ACCEPTED)
            {
                Feedback(GetEnumDescription(ConsoleError.TOS_NOT_ACCEPTED));
                Environment.Exit(1);
            }
            // Proceed to log-in.
            LoginParams login = new LoginParams(
                Client,
                Configuration.FIRST_NAME,
                Configuration.LAST_NAME,
                Configuration.PASSWORD,
                CORRADE_CONSTANTS.CLIENT_CHANNEL,
                CORRADE_VERSION.ToString(CultureInfo.InvariantCulture),
                Configuration.LOGIN_URL)
            {
                Author = @"Wizardry and Steamworks",
                AgreeToTos = Configuration.TOS_ACCEPTED,
                Start = Configuration.START_LOCATION,
                UserAgent = @"libopenmetaverse"
            };
            // Set the MAC if specified in the configuration file.
            if (!string.IsNullOrEmpty(Configuration.NETWORK_CARD_MAC))
            {
                login.MAC = Utils.MD5String(Configuration.NETWORK_CARD_MAC);
            }
            Feedback(GetEnumDescription(ConsoleError.LOGGING_IN));
            Client.Network.Login(login);
            // Start the HTTP Server if it is supported
            Thread HTTPListenerThread = null;
            HttpListener httpListener = null;
            if (Configuration.HTTP_SERVER && !HttpListener.IsSupported)
            {
                Feedback(GetEnumDescription(ConsoleError.HTTP_SERVER_ERROR),
                    GetEnumDescription(ConsoleError.HTTP_SERVER_NOT_SUPPORTED));
            }
            if (Configuration.HTTP_SERVER && HttpListener.IsSupported)
            {
                Feedback(GetEnumDescription(ConsoleError.STARTING_HTTP_SERVER));
                HTTPListenerThread = new Thread(() =>
                {
                    try
                    {
                        httpListener = new HttpListener();
                        httpListener.Prefixes.Add(Configuration.HTTP_SERVER_PREFIX);
                        httpListener.Start();
                        while (httpListener.IsListening)
                        {
                            IAsyncResult result = httpListener.BeginGetContext(ProcesHTTPRequest, httpListener);
                            result.AsyncWaitHandle.WaitOne(Configuration.CALLBACK_TIMEOUT, false);
                        }
                    }
                    catch (Exception e)
                    {
                        Feedback(GetEnumDescription(ConsoleError.HTTP_SERVER_ERROR), e.Message);
                    }
                });
                HTTPListenerThread.Start();
            }
            /*
             * The main thread spins around waiting for the semaphores to become invalidated,
             * at which point Corrade will consider its connection to the grid severed and
             * will terminate.
             *
             */
            WaitHandle.WaitAny(ConnectionSemaphores.Values.Select(o => (WaitHandle) o).ToArray());
            // Now log-out.
            Feedback(GetEnumDescription(ConsoleError.LOGGING_OUT));
            // Disable the watcher.
            configurationWatcher.EnableRaisingEvents = false;
            configurationWatcher.Dispose();
            // Close HTTP server
            if (Configuration.HTTP_SERVER && HttpListener.IsSupported)
            {
                Feedback(GetEnumDescription(ConsoleError.STOPPING_HTTP_SERVER));
                if (httpListener != null && httpListener.IsListening)
                {
                    httpListener.Stop();
                }
                if (httpListener != null && httpListener.IsListening)
                {
                    httpListener.Abort();
                }
                if (HTTPListenerThread != null)
                {
                    HTTPListenerThread.Join(Configuration.SERVICES_TIMEOUT);
                }
            }
            // Logout
            Client.Network.Logout();
            Client.Network.Shutdown(NetworkManager.DisconnectType.ClientInitiated);
            // Close signals
            if (Environment.OSVersion.Platform.Equals(PlatformID.Unix) && TrapSignalsThread != null)
            {
                TrapSignalsThread.Abort();
            }
            Environment.Exit(0);
        }

        private static void ProcesHTTPRequest(IAsyncResult ar)
        {
            HttpListener httpListener = ar.AsyncState as HttpListener;
            // bail if we are not listening
            if (httpListener == null || !httpListener.IsListening) return;
            HttpListenerContext httpContext = httpListener.EndGetContext(ar);
            HttpListenerRequest httpRequest = httpContext.Request;
            // only accept POST requests
            if (!httpRequest.HttpMethod.Equals(CORRADE_CONSTANTS.POST, StringComparison.OrdinalIgnoreCase)) return;
            Stream body = httpRequest.InputStream;
            Encoding encoding = httpRequest.ContentEncoding;
            StreamReader reader = new StreamReader(body, encoding);
            Dictionary<string, string> result = HandleCorradeCommand(reader.ReadToEnd(), CORRADE_CONSTANTS.WEB_REQUEST,
                httpRequest.RemoteEndPoint.ToString());
            if (result == null) return;
            HttpListenerResponse response = httpContext.Response;
            response.ContentType = CORRADE_CONSTANTS.TEXT_HTML;
            byte[] data = Encoding.UTF8.GetBytes(wasKeyValueEncode(wasKeyValueEscape(result)));
            response.ContentLength64 = data.Length;
            response.StatusCode = CORRADE_CONSTANTS.HTTP_CODES.OK; // HTTP OK
            Stream responseStream = response.OutputStream;
            if (responseStream == null) return;
            responseStream.Write(data, 0, data.Length);
            responseStream.Close();
        }

        private static void SendNotification(Notifications notification, object e)
        {
            // First check if the group is able to receive dialog notifications.
            Parallel.ForEach(
                GroupNotifications.Where(
                    o => HasCorradeNotification(o.GROUP, (int) notification)), p =>
                    {
                        // Next, check if the group has registered to receive dialog notifications.
                        if ((p.NOTIFICATION_MASK & (int) notification) == 0)
                        {
                            return;
                        }
                        Dictionary<string, string> notificationData = new Dictionary<string, string>
                        {
                            {
                                GetEnumDescription(ScriptKeys.TYPE),
                                GetEnumDescription(notification)
                            }
                        };
                        new Thread(() =>
                        {
                            switch (notification)
                            {
                                case Notifications.NOTIFICATION_SCRIPT_DIALOG:
                                    ScriptDialogEventArgs scriptDialogEventArgs = (ScriptDialogEventArgs) e;
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE),
                                        scriptDialogEventArgs.Message);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        scriptDialogEventArgs.FirstName);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        scriptDialogEventArgs.LastName);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.CHANNEL),
                                        scriptDialogEventArgs.Channel.ToString(CultureInfo.InvariantCulture));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.NAME),
                                        scriptDialogEventArgs.ObjectName);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.ITEM),
                                        scriptDialogEventArgs.ObjectID.ToString());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.OWNER),
                                        scriptDialogEventArgs.OwnerID.ToString());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.BUTTON),
                                        string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                            scriptDialogEventArgs.ButtonLabels.ToArray()));
                                    break;
                                case Notifications.NOTIFICATION_LOCAL_CHAT:
                                    ChatEventArgs chatEventArgs = (ChatEventArgs) e;
                                    List<string> chatName =
                                        new List<string>(chatEventArgs.FromName.Split(new[] {' ', '.'},
                                            StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE), chatEventArgs.Message);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME), chatName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME), chatName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.OWNER),
                                        chatEventArgs.OwnerID.ToString());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.ITEM),
                                        chatEventArgs.SourceID.ToString());
                                    break;
                                case Notifications.NOTIFICATION_BALANCE:
                                    BalanceEventArgs balanceEventArgs = (BalanceEventArgs) e;
                                    notificationData.Add(GetEnumDescription(ScriptKeys.BALANCE),
                                        balanceEventArgs.Balance.ToString(CultureInfo.InvariantCulture));
                                    break;
                                case Notifications.NOTIFICATION_ALERT_MESSAGE:
                                    AlertMessageEventArgs alertMessageEventArgs = (AlertMessageEventArgs) e;
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE),
                                        alertMessageEventArgs.Message);
                                    break;
                                case Notifications.NOTIFICATION_INVENTORY_OFFER:
                                    InventoryObjectOfferedEventArgs inventoryObjectOfferedEventArgs =
                                        (InventoryObjectOfferedEventArgs) e;
                                    List<string> inventoryObjectOfferedName =
                                        new List<string>(
                                            inventoryObjectOfferedEventArgs.Offer.FromAgentName.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        inventoryObjectOfferedName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        inventoryObjectOfferedName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.ASSET),
                                        inventoryObjectOfferedEventArgs.AssetType.ToString());
                                    break;
                                case Notifications.NOTIFICATION_SCRIPT_PERMISSION:
                                    ScriptQuestionEventArgs scriptQuestionEventArgs = (ScriptQuestionEventArgs) e;
                                    notificationData.Add(GetEnumDescription(ScriptKeys.ITEM),
                                        scriptQuestionEventArgs.ItemID.ToString());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.TASK),
                                        scriptQuestionEventArgs.TaskID.ToString());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.PERMISSIONS),
                                        string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                            typeof (ScriptPermission).GetFields(BindingFlags.Public |
                                                                                BindingFlags.Static)
                                                .Where(
                                                    o =>
                                                        ((int) o.GetValue(null) &
                                                         (int) scriptQuestionEventArgs.Questions) != 0)
                                                .Select(o => o.Name).ToArray()));
                                    break;
                                case Notifications.NOTIFICATION_FRIENDSHIP:
                                    System.Type friendshipNotificationType = e.GetType();
                                    if (friendshipNotificationType == typeof (FriendInfoEventArgs))
                                    {
                                        FriendInfoEventArgs friendInfoEventArgs = (FriendInfoEventArgs) e;
                                        List<string> name =
                                            new List<string>(friendInfoEventArgs.Friend.Name.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                        notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME), name.First());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME), name.Last());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.STATUS),
                                            friendInfoEventArgs.Friend.IsOnline
                                                ? GetEnumDescription(Action.ONLINE)
                                                : GetEnumDescription(Action.OFFLINE));
                                        notificationData.Add(GetEnumDescription(ScriptKeys.RIGHTS),
                                            // Return the friend rights as a nice CSV string.
                                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                                typeof (FriendRights).GetFields(BindingFlags.Public |
                                                                                BindingFlags.Static)
                                                    .Where(
                                                        o =>
                                                            ((int) o.GetValue(null) &
                                                             (int) friendInfoEventArgs.Friend.MyFriendRights) !=
                                                            0)
                                                    .Select(o => o.Name)
                                                    .ToArray()));
                                        break;
                                    }
                                    if (friendshipNotificationType == typeof (FriendshipResponseEventArgs))
                                    {
                                        FriendshipResponseEventArgs friendshipResponseEventArgs =
                                            (FriendshipResponseEventArgs) e;
                                        List<string> friendshipResponseName =
                                            new List<string>(
                                                friendshipResponseEventArgs.AgentName.Split(new[] {' ', '.'},
                                                    StringSplitOptions.RemoveEmptyEntries));
                                        notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                            friendshipResponseName.First());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                            friendshipResponseName.Last());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.ACTION),
                                            GetEnumDescription(Action.RESPONSE));
                                        break;
                                    }
                                    if (friendshipNotificationType == typeof (FriendshipOfferedEventArgs))
                                    {
                                        FriendshipOfferedEventArgs friendshipOfferedEventArgs =
                                            (FriendshipOfferedEventArgs) e;
                                        List<string> friendshipOfferedName =
                                            new List<string>(friendshipOfferedEventArgs.AgentName.Split(
                                                new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                        notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                            friendshipOfferedName.First());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                            friendshipOfferedName.Last());
                                        notificationData.Add(GetEnumDescription(ScriptKeys.ACTION),
                                            GetEnumDescription(Action.REQUEST));
                                    }
                                    break;
                                case Notifications.NOTIFICATION_TELEPORT_LURE:
                                    InstantMessageEventArgs teleportLureEventArgs = (InstantMessageEventArgs) e;
                                    List<string> teleportLureName =
                                        new List<string>(teleportLureEventArgs.IM.FromAgentName.Split(new[] {' ', '.'},
                                            StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        teleportLureName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        teleportLureName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.SESSION),
                                        teleportLureEventArgs.IM.IMSessionID.ToString());
                                    break;
                                case Notifications.NOTIFICATION_GROUP_NOTICE:
                                    InstantMessageEventArgs notificationGroupNoticEventArgs =
                                        (InstantMessageEventArgs) e;
                                    List<string> notificationGroupNoticeName =
                                        new List<string>(
                                            notificationGroupNoticEventArgs.IM.FromAgentName.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        notificationGroupNoticeName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        notificationGroupNoticeName.Last());
                                    string[] noticeData = notificationGroupNoticEventArgs.IM.Message.Split('|');
                                    if (noticeData.Length > 0 && !string.IsNullOrEmpty(noticeData[0]))
                                    {
                                        notificationData.Add(GetEnumDescription(ScriptKeys.SUBJECT), noticeData[0]);
                                    }
                                    if (noticeData.Length > 1 && !string.IsNullOrEmpty(noticeData[1]))
                                    {
                                        notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE), noticeData[1]);
                                    }
                                    break;
                                case Notifications.NOTIFICATION_INSTANT_MESSAGE:
                                    InstantMessageEventArgs notificationInstantMessage = (InstantMessageEventArgs) e;
                                    List<string> notificationInstantMessageName =
                                        new List<string>(
                                            notificationInstantMessage.IM.FromAgentName.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        notificationInstantMessageName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        notificationInstantMessageName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE),
                                        notificationInstantMessage.IM.Message);
                                    break;
                                case Notifications.NOTIFICATION_REGION_MESSAGE:
                                    InstantMessageEventArgs notificationRegionMessage = (InstantMessageEventArgs) e;
                                    List<string> notificationRegionMessageName =
                                        new List<string>(
                                            notificationRegionMessage.IM.FromAgentName.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        notificationRegionMessageName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        notificationRegionMessageName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE),
                                        notificationRegionMessage.IM.Message);
                                    break;
                                case Notifications.NOTIFICATION_GROUP_MESSAGE:
                                    InstantMessageEventArgs notificationGroupMessage = (InstantMessageEventArgs) e;
                                    List<string> notificationGroupMessageName =
                                        new List<string>(
                                            notificationGroupMessage.IM.FromAgentName.Split(new[] {' ', '.'},
                                                StringSplitOptions.RemoveEmptyEntries));
                                    notificationData.Add(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                        notificationGroupMessageName.First());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.LASTNAME),
                                        notificationGroupMessageName.Last());
                                    notificationData.Add(GetEnumDescription(ScriptKeys.GROUP), p.GROUP);
                                    notificationData.Add(GetEnumDescription(ScriptKeys.MESSAGE),
                                        notificationGroupMessage.IM.Message);
                                    break;
                            }
                            string error = wasPOST(p.URL, wasKeyValueEscape(notificationData));
                            if (!string.IsNullOrEmpty(error))
                            {
                                Feedback(GetEnumDescription(ConsoleError.NOTIFICATION_COULD_NOT_BE_SENT), error);
                            }
                        }).Start();
                    });
        }

        private static void HandleScriptDialog(object sender, ScriptDialogEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_SCRIPT_DIALOG, e);
        }

        private static void HandleChatFromSimulator(object sender, ChatEventArgs e)
        {
            // Ignore self
            if (e.SourceID.Equals(Client.Self.AgentID)) return;
            // Ignore chat with no message (ie: start / stop typing)
            if (string.IsNullOrEmpty(e.Message)) return;
            switch (e.Type)
            {
                case ChatType.Debug:
                case ChatType.Normal:
                case ChatType.OwnerSay:
                case ChatType.Shout:
                case ChatType.Whisper:
                    // Send chat notifications.
                    SendNotification(Notifications.NOTIFICATION_LOCAL_CHAT, e);
                    break;
                case (ChatType) 9:
                    HandleCorradeCommand(e.Message, e.FromName, e.OwnerID.ToString());
                    break;
            }
        }

        private static void HandleMoneyBalance(object sender, BalanceEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_BALANCE, e);
        }

        private static void HandleAlertMessage(object sender, AlertMessageEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_ALERT_MESSAGE, e);
        }

        private static void HandleInventoryObjectOffered(object sender, InventoryObjectOfferedEventArgs e)
        {
            // Send notification
            SendNotification(Notifications.NOTIFICATION_INVENTORY_OFFER, e);
            // Accept inventory only from masters (for the time being)
            if (
                !Configuration.MASTERS.Select(
                    o => string.Format(CultureInfo.InvariantCulture, "{0} {1}", o.FirstName, o.LastName))
                    .
                    Any(p => p.Equals(e.Offer.FromAgentName, StringComparison.OrdinalIgnoreCase)))
                return;
            e.Accept = true;
        }

        private static void HandleScriptQuestion(object sender, ScriptQuestionEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_SCRIPT_PERMISSION, e);
        }

        private static void HandleConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            Feedback(GetEnumDescription(ConsoleError.CONFIGURATION_FILE_MODIFIED));
            Configuration.Load(e.Name);
        }

        private static void HandleDisconnected(object sender, DisconnectedEventArgs e)
        {
            Feedback(GetEnumDescription(ConsoleError.DISCONNECTED));
            ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('l')).Value.Set();
        }

        private static void HandleEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            Feedback(GetEnumDescription(ConsoleError.EVENT_QUEUE_STARTED));
        }

        private static void HandleSimulatorConnected(object sender, SimConnectedEventArgs e)
        {
            Feedback(GetEnumDescription(ConsoleError.SIMULATOR_CONNECTED));
        }

        private static void HandleSimulatorDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            // if any simulators are still connected, we are not disconnected
            if (Client.Network.Simulators.Any())
                return;
            Feedback(GetEnumDescription(ConsoleError.ALL_SIMULATORS_DISCONNECTED));
            ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('s')).Value.Set();
        }

        private static void HandleAppearanceSet(object sender, AppearanceSetEventArgs e)
        {
            if (e.Success)
            {
                Feedback(GetEnumDescription(ConsoleError.APPEARANCE_SET_SUCCEEDED));
                return;
            }
            Feedback(GetEnumDescription(ConsoleError.APPEARANCE_SET_FAILED));
        }

        private static void HandleLoginProgress(object sender, LoginProgressEventArgs e)
        {
            switch (e.Status)
            {
                case LoginStatus.Success:
                    Feedback(GetEnumDescription(ConsoleError.LOGIN_SUCCEEDED));
                    if (Configuration.AUTO_ACTIVATE_GROUP)
                    {
                        ActivateCurrentLandGroup.Invoke();
                    }
                    break;
                case LoginStatus.Failed:
                    Feedback(GetEnumDescription(ConsoleError.LOGIN_FAILED), e.FailReason);
                    ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('l')).Value.Set();
                    break;
            }
        }

        private static void HandleFriendOnlineStatus(object sender, FriendInfoEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_FRIENDSHIP, e);
        }

        private static void HandleFriendRightsUpdate(object sender, FriendInfoEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_FRIENDSHIP, e);
        }

        private static void HandleFriendShipResponse(object sender, FriendshipResponseEventArgs e)
        {
            SendNotification(Notifications.NOTIFICATION_FRIENDSHIP, e);
        }

        private static void HandleFriendshipOffered(object sender, FriendshipOfferedEventArgs e)
        {
            // Send friendship notifications
            SendNotification(Notifications.NOTIFICATION_FRIENDSHIP, e);
            // Accept friendships only from masters (for the time being)
            if (
                !Configuration.MASTERS.Select(
                    o => string.Format(CultureInfo.InvariantCulture, "{0} {1}", o.FirstName, o.LastName))
                    .Any(p => p.Equals(e.AgentName, StringComparison.CurrentCultureIgnoreCase)))
                return;
            Feedback(GetEnumDescription(ConsoleError.ACCEPTED_FRIENDSHIP), e.AgentName);
            Client.Friends.AcceptFriendship(e.AgentID, e.SessionID);
        }

        private static void HandleTeleportProgress(object sender, TeleportEventArgs e)
        {
            switch (e.Status)
            {
                case TeleportStatus.Finished:
                    Feedback(GetEnumDescription(ConsoleError.TELEPORT_SUCCEEDED));
                    if (Configuration.AUTO_ACTIVATE_GROUP)
                    {
                        ActivateCurrentLandGroup.Invoke();
                    }
                    break;
                case TeleportStatus.Failed:
                    Feedback(GetEnumDescription(ConsoleError.TELEPORT_FAILED));
                    break;
            }
        }

        private static void HandleSelfIM(object sender, InstantMessageEventArgs args)
        {
            // Ignore self.
            if (args.IM.FromAgentName.Equals(string.Join(" ", new[] {Client.Self.FirstName, Client.Self.LastName}),
                StringComparison.Ordinal))
                return;
            // Create a copy of the message.
            string message = args.IM.Message;
            // Process dialog messages.
            switch (args.IM.Dialog)
            {
                    // Ignore typing messages.
                case InstantMessageDialog.StartTyping:
                case InstantMessageDialog.StopTyping:
                    return;
                case InstantMessageDialog.InventoryOffered:
                    Feedback(GetEnumDescription(ConsoleError.GOT_INVENTORY_OFFER),
                        message.Replace(Environment.NewLine, " : "));
                    return;
                case InstantMessageDialog.MessageBox:
                    Feedback(GetEnumDescription(ConsoleError.GOT_SERVER_MESSAGE),
                        message.Replace(Environment.NewLine, " : "));
                    return;
                case InstantMessageDialog.RequestTeleport:
                    Feedback(GetEnumDescription(ConsoleError.GOT_TELEPORT_LURE),
                        message.Replace(Environment.NewLine, " : "));
                    // Send teleport lure notification.
                    SendNotification(Notifications.NOTIFICATION_TELEPORT_LURE, args);
                    // If we got a teleport request from a master, then accept it (for the moment).
                    if (Configuration.MASTERS.Select(
                        o =>
                            string.Format(CultureInfo.InvariantCulture, "{0} {1}", o.FirstName, o.LastName))
                        .
                        Any(p => p.Equals(args.IM.FromAgentName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Feedback(GetEnumDescription(ConsoleError.ACCEPTING_TELEPORT_LURE), args.IM.FromAgentName);
                        if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                        {
                            Client.Self.Stand();
                        }
                        Client.Self.SignaledAnimations.ForEach(
                            animation => Client.Self.AnimationStop(animation.Key, true));
                        Client.Self.TeleportLureRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                        return;
                    }
                    return;
                case InstantMessageDialog.GroupInvitation:
                    Feedback(GetEnumDescription(ConsoleError.GOT_GROUP_INVITE),
                        message.Replace(Environment.NewLine, " : "));
                    if (
                        !Configuration.MASTERS.Select(
                            o =>
                                string.Format(CultureInfo.InvariantCulture, "{0}.{1}", o.FirstName, o.LastName))
                            .
                            Any(p => p.Equals(args.IM.FromAgentName, StringComparison.OrdinalIgnoreCase)))
                        return;
                    Feedback(GetEnumDescription(ConsoleError.ACCEPTING_GROUP_INVITE), args.IM.FromAgentName);
                    Client.Self.GroupInviteRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                    return;
                case InstantMessageDialog.GroupNotice:
                    Feedback(GetEnumDescription(ConsoleError.GOT_GROUP_NOTICE),
                        message.Replace(Environment.NewLine, " : "));
                    SendNotification(Notifications.NOTIFICATION_GROUP_NOTICE, args);
                    return;
                case InstantMessageDialog.SessionSend:
                case InstantMessageDialog.MessageFromAgent:
                    // Check if this is a group message.
                    // Note that this is a lousy way of doing it but libomv does not properly set the GroupIM field
                    // such that the only way to determine if we have a group message is to check that the UUID
                    // of the session is actually the UUID of a current group. Furthermore, what's worse is that 
                    // group mesages can appear both through SessionSend and from MessageFromAgent. Hence the problem.
                    OpenMetaverse.Group messageGroup = new OpenMetaverse.Group();
                    bool messageFromGroup = false;
                    ManualResetEvent CurrentGroupsEvent = new ManualResetEvent(false);
                    EventHandler<CurrentGroupsEventArgs> CurrentGroupsEventHandler = (s, a) =>
                    {
                        messageFromGroup = a.Groups.Any(o => o.Key.Equals(args.IM.IMSessionID));
                        messageGroup = a.Groups.FirstOrDefault(o => o.Key.Equals(args.IM.IMSessionID)).Value;
                        CurrentGroupsEvent.Set();
                    };
                    Client.Groups.CurrentGroups += CurrentGroupsEventHandler;
                    Client.Groups.RequestCurrentGroups();
                    if (!CurrentGroupsEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                    {
                        Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
                        return;
                    }
                    Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
                    if (messageFromGroup)
                    {
                        Feedback(GetEnumDescription(ConsoleError.GOT_GROUP_MESSAGE),
                            message.Replace(Environment.NewLine, " : "));
                        // Send group notice notifications.
                        SendNotification(Notifications.NOTIFICATION_GROUP_MESSAGE, args);
                        // Log group messages
                        Parallel.ForEach(
                            Configuration.GROUPS.Where(o => o.Name.Equals(messageGroup.Name, StringComparison.Ordinal)),
                            o =>
                            {
                                // Attempt to write to log file,
                                try
                                {
                                    lock (FileLock)
                                    {
                                        using (StreamWriter logWriter = File.AppendText(o.ChatLog))
                                        {
                                            logWriter.WriteLine("[{0}] {1} : {2}",
                                                DateTime.Now.ToString("MM-dd-yyyy HH:mm",
                                                    DateTimeFormatInfo.InvariantInfo), args.IM.FromAgentName, message);
                                            logWriter.Flush();
                                            //logWriter.Close();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    // or fail and append the fail message.
                                    Feedback(GetEnumDescription(ConsoleError.COULD_NOT_WRITE_TO_GROUP_CHAT_LOGFILE),
                                        e.Message);
                                }
                            });
                        return;
                    }
                    // Check if this is an instant message.
                    if (args.IM.ToAgentID.Equals(Client.Self.AgentID))
                    {
                        Feedback(GetEnumDescription(ConsoleError.GOT_INSTANT_MESSAGE),
                            message.Replace(Environment.NewLine, " : "));
                        SendNotification(Notifications.NOTIFICATION_INSTANT_MESSAGE, args);
                        return;
                    }
                    // Check if this is a region message.
                    if (args.IM.IMSessionID.Equals(UUID.Zero))
                    {
                        Feedback(GetEnumDescription(ConsoleError.GOT_REGION_MESSAGE),
                            message.Replace(Environment.NewLine, " : "));
                        SendNotification(Notifications.NOTIFICATION_REGION_MESSAGE, args);
                        return;
                    }
                    break;
            }
            HandleCorradeCommand(args.IM.Message, args.IM.FromAgentName, args.IM.FromAgentID.ToString());
        }

        private static Dictionary<string, string> HandleCorradeCommand(string message, string sender, string identifier)
        {
            // Now we can start processing commands.
            // Get group and password.
            string group = wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.GROUP), message));
            // Bail if no group set.
            if (string.IsNullOrEmpty(group)) return null;
            string password = wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PASSWORD), message));
            // Bail if no password set.
            if (string.IsNullOrEmpty(password)) return null;
            // Authenticate the request against the group password.
            if (!Authenticate(group, password))
            {
                Feedback(group, GetEnumDescription(ConsoleError.ACCESS_DENIED));
                return null;
            }
            // Censor password.
            message = wasKeyValueSet(GetEnumDescription(ScriptKeys.PASSWORD), CORRADE_CONSTANTS.PASSWORD_CENSOR, message);
            /*
             * OpenSim sends the primitive UUID through args.IM.FromAgentID while Second Life properly sends 
             * the agent UUID - which just shows how crap OpenSim really is. This tries to resolve 
             * args.IM.FromAgentID to a name, which is what Second Life does, otherwise it just sets the name 
             * to the name of the primitive sending the message.
             */
            if (Client.Network.CurrentSim.SimVersion.Contains(LINDEN_CONSTANTS.GRID.SECOND_LIFE))
            {
                UUID fromAgentID;
                if (UUID.TryParse(identifier, out fromAgentID))
                {
                    if (!AgentUUIDToName(fromAgentID, Configuration.SERVICES_TIMEOUT, ref sender))
                    {
                        Feedback(GetEnumDescription(ConsoleError.AGENT_NOT_FOUND), fromAgentID.ToString());
                        return null;
                    }
                }
            }
            Feedback(string.Format(CultureInfo.InvariantCulture, "{0} ({1}) : {2}", sender,
                identifier,
                message));
            return ProcessCommand(message);
        }

        /// <summary>
        ///     This function is responsible for processing commands.
        /// </summary>
        /// <param name="message">the message</param>
        /// <returns>a dictionary of key-value pairs representing the results of the command</returns>
        private static Dictionary<string, string> ProcessCommand(string message)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string command = wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.COMMAND), message));
            if (!string.IsNullOrEmpty(command))
            {
                result.Add(GetEnumDescription(ScriptKeys.COMMAND), command);
            }
            string group = wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.GROUP), message));
            if (!string.IsNullOrEmpty(group))
            {
                result.Add(GetEnumDescription(ScriptKeys.GROUP), group);
            }

            System.Action execute;

            switch ((ScriptKeys) wasGetEnumValueFromDescription<ScriptKeys>(command))
            {
                case ScriptKeys.JOIN:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.ALREADY_IN_GROUP));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref commandGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!commandGroup.OpenEnrollment)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_OPEN));
                        }
                        ManualResetEvent GroupJoinedReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                            (sender, args) => GroupJoinedReplyEvent.Set();
                        Client.Groups.GroupJoinedReply += GroupOperationEventHandler;
                        Client.Groups.RequestJoinGroup(groupUUID);
                        if (!GroupJoinedReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_JOINING_GROUP));
                        }
                        Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_JOIN_GROUP));
                        }
                    };
                    break;
                case ScriptKeys.CREATEGROUP:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group, (int) Permissions.PERMISSION_ECONOMY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
                        EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceEventHandler =
                            (sender, args) => MoneyBalanceEvent.Set();
                        Client.Self.MoneyBalanceReply += MoneyBalanceEventHandler;
                        Client.Self.RequestBalance();
                        if (!MoneyBalanceEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_WAITING_FOR_BALANCE));
                        }
                        Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
                        if (Client.Self.Balance < Configuration.GROUP_CREATE_FEE)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INSUFFICIENT_FUNDS));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group
                        {
                            Name = group
                        };
                        wasCSVToStructure(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                            ref commandGroup);
                        bool succeeded = false;
                        ManualResetEvent GroupCreatedReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupCreatedReplyEventArgs> GroupCreatedEventHandler = (sender, args) =>
                        {
                            if (args.Success)
                            {
                                succeeded = true;
                            }
                            GroupCreatedReplyEvent.Set();
                        };
                        Client.Groups.GroupCreatedReply += GroupCreatedEventHandler;
                        Client.Groups.RequestCreateGroup(commandGroup);
                        if (!GroupCreatedReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_CREATING_GROUP));
                        }
                        Client.Groups.GroupCreatedReply -= GroupCreatedEventHandler;
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_CREATE_GROUP));
                        }
                    };
                    break;
                case ScriptKeys.INVITE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.Invite,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        if (AgentInGroup(agentUUID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.ALREADY_IN_GROUP));
                        }
                        // role UUID is optional
                        UUID roleUUID = UUID.Zero;
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        if (!string.IsNullOrEmpty(role))
                        {
                            if (!RoleNameToRoleUUID(role, groupUUID, Configuration.SERVICES_TIMEOUT, ref roleUUID))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.ROLE_NOT_FOUND));
                            }
                        }
                        Client.Groups.Invite(groupUUID, new List<UUID> {roleUUID}, agentUUID);
                    };
                    break;
                case ScriptKeys.EJECT:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.Eject,
                                Configuration.SERVICES_TIMEOUT) ||
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.RemoveMember,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        if (!AgentInGroup(agentUUID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref commandGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                        {
                            if (args.RolesMembers.Any(
                                o => o.Key.Equals(commandGroup.OwnerRole) && o.Value.Equals(agentUUID)))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.CANNOT_EJECT_OWNERS));
                            }
                            foreach (KeyValuePair<UUID, UUID> pair in args.RolesMembers)
                            {
                                if (pair.Value.Equals(agentUUID))
                                {
                                    Client.Groups.RemoveFromRole(groupUUID, pair.Key, agentUUID);
                                }
                            }
                            GroupRoleMembersReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS));
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                        ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                        bool succeeded = false;
                        EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (sender, args) =>
                        {
                            if (args.Success)
                            {
                                succeeded = true;
                            }
                            GroupEjectEvent.Set();
                        };
                        Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                        Client.Groups.EjectUser(groupUUID, agentUUID);
                        if (!GroupEjectEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_EJECTING_AGENT));
                        }
                        Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_EJECT_AGENT));
                        }
                    };
                    break;
                case ScriptKeys.GETGROUPACCOUNTSUMMARYDATA:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        int days;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DAYS), message)),
                                out days))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_DAYS));
                        }
                        int interval;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.INTERVAL), message)),
                                out interval))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_INTERVAL));
                        }
                        ManualResetEvent RequestGroupAccountSummaryEvent = new ManualResetEvent(false);
                        GroupAccountSummary summary = new GroupAccountSummary();
                        EventHandler<GroupAccountSummaryReplyEventArgs> RequestGroupAccountSummaryEventHandler =
                            (sender, args) =>
                            {
                                summary = args.Summary;
                                RequestGroupAccountSummaryEvent.Set();
                            };
                        Client.Groups.GroupAccountSummaryReply += RequestGroupAccountSummaryEventHandler;
                        Client.Groups.RequestGroupAccountSummary(groupUUID, days, interval);
                        if (!RequestGroupAccountSummaryEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY));
                        }
                        Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(summary,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.UPDATEGROUPDATA:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.ChangeIdentity,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref commandGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        wasCSVToStructure(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                            ref commandGroup);
                        Client.Groups.UpdateGroup(groupUUID, commandGroup);
                    };
                    break;
                case ScriptKeys.LEAVE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        ManualResetEvent GroupLeaveReplyEvent = new ManualResetEvent(false);
                        bool succeeded = false;
                        EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (sender, args) =>
                        {
                            if (args.Success)
                            {
                                succeeded = true;
                            }
                            GroupLeaveReplyEvent.Set();
                        };
                        Client.Groups.GroupLeaveReply += GroupOperationEventHandler;
                        Client.Groups.LeaveGroup(groupUUID);
                        if (!GroupLeaveReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupLeaveReply -= GroupOperationEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_LEAVING_GROUP));
                        }
                        Client.Groups.GroupLeaveReply -= GroupOperationEventHandler;
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_LEAVE_GROUP));
                        }
                    };
                    break;
                case ScriptKeys.CREATEROLE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.CreateRole,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                        int roleCount = 0;
                        EventHandler<GroupRolesDataReplyEventArgs> GroupRolesDataEventHandler = (sender, args) =>
                        {
                            roleCount = args.Roles.Count;
                            GroupRoleDataReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleDataReply += GroupRolesDataEventHandler;
                        Client.Groups.RequestGroupRoles(groupUUID);
                        if (!GroupRoleDataReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_ROLES));
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                        if (roleCount >= LINDEN_CONSTANTS.GROUPS.MAXIMUM_NUMBER_OF_ROLES)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.MAXIMUM_NUMBER_OF_ROLES_EXCEEDED));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        if (string.IsNullOrEmpty(role))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ROLE_NAME_SPECIFIED));
                        }
                        ulong powers = 0;
                        Parallel.ForEach(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POWERS), message))
                                .Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER}, StringSplitOptions.RemoveEmptyEntries),
                            o =>
                                Parallel.ForEach(
                                    typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    q => { powers |= ((ulong) q.GetValue(null)); }));
                        if (powers != 0 &&
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.ChangeActions,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        Client.Groups.CreateRole(groupUUID, new GroupRole
                        {
                            Name = role,
                            Description =
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DESCRIPTION),
                                    message)),
                            GroupID = groupUUID,
                            ID = UUID.Random(),
                            Powers = (GroupPowers) powers,
                            Title =
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TITLE), message))
                        });
                        UUID roleUUID = UUID.Zero;
                        if (
                            !RoleNameToRoleUUID(role, groupUUID,
                                Configuration.SERVICES_TIMEOUT, ref roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_CREATE_ROLE));
                        }
                    };
                    break;
                case ScriptKeys.GETROLES:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                        List<string> csv = new List<string>();
                        EventHandler<GroupRolesDataReplyEventArgs> GroupRolesDataEventHandler = (sender, args) =>
                        {
                            csv.AddRange(args.Roles.Select(o => new[]
                            {
                                o.Value.Name,
                                o.Value.ID.ToString(),
                                o.Value.Title,
                                o.Value.Description
                            }).SelectMany(o => o));
                            GroupRoleDataReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleDataReply += GroupRolesDataEventHandler;
                        Client.Groups.RequestGroupRoles(groupUUID);
                        if (!GroupRoleDataReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_ROLES));
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.ROLES),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETMEMBERS:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(o => o.Name.Equals(group, StringComparison.Ordinal))
                                .UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !AgentInGroup(Client.Self.AgentID, groupUUID,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        ManualResetEvent agentInGroupEvent = new ManualResetEvent(false);
                        List<string> csv = new List<string>();
                        EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                        {
                            foreach (KeyValuePair<UUID, GroupMember> pair in args.Members)
                            {
                                string agentName = string.Empty;
                                if (!AgentUUIDToName(pair.Value.ID, Configuration.SERVICES_TIMEOUT, ref agentName))
                                    continue;
                                csv.Add(agentName);
                                csv.Add(pair.Key.ToString());
                            }
                            agentInGroupEvent.Set();
                        };
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        Client.Groups.RequestGroupMembers(groupUUID);
                        if (!agentInGroupEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS));
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.MEMBERS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETROLEMEMBERS:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(o => o.Name.Equals(group, StringComparison.Ordinal))
                                .UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !AgentInGroup(Client.Self.AgentID, groupUUID,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        if (string.IsNullOrEmpty(role))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ROLE_NAME_SPECIFIED));
                        }
                        List<string> csv = new List<string>();
                        ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler =
                            (sender, args) =>
                            {
                                foreach (KeyValuePair<UUID, UUID> pair in args.RolesMembers)
                                {
                                    string roleName = string.Empty;
                                    if (
                                        !RoleUUIDToName(pair.Key, groupUUID, Configuration.SERVICES_TIMEOUT,
                                            ref roleName))
                                        continue;
                                    if (!roleName.Equals(role))
                                        continue;
                                    string agentName = string.Empty;
                                    if (!AgentUUIDToName(pair.Value, Configuration.SERVICES_TIMEOUT, ref agentName))
                                        continue;
                                    csv.Add(agentName);
                                    csv.Add(pair.Value.ToString());
                                }
                                GroupRoleMembersReplyEvent.Set();
                            };
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS));
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.MEMBERS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETROLESMEMBERS:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !AgentInGroup(Client.Self.AgentID, groupUUID,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        List<string> csv = new List<string>();
                        ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler =
                            (sender, args) =>
                            {
                                foreach (KeyValuePair<UUID, UUID> pair in args.RolesMembers)
                                {
                                    string roleName = string.Empty;
                                    if (
                                        !RoleUUIDToName(pair.Key, groupUUID, Configuration.SERVICES_TIMEOUT,
                                            ref roleName))
                                        continue;
                                    string agentName = string.Empty;
                                    if (!AgentUUIDToName(pair.Value, Configuration.SERVICES_TIMEOUT, ref agentName))
                                        continue;
                                    csv.Add(roleName);
                                    csv.Add(agentName);
                                    csv.Add(pair.Value.ToString());
                                }
                                GroupRoleMembersReplyEvent.Set();
                            };
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS));
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.MEMBERS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETROLEPOWERS:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.RoleProperties,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        if (
                            !AgentInGroup(Client.Self.AgentID, groupUUID,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        UUID roleUUID;
                        if (!UUID.TryParse(role, out roleUUID))
                        {
                            if (
                                !RoleNameToRoleUUID(role, groupUUID,
                                    Configuration.SERVICES_TIMEOUT, ref roleUUID))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.ROLE_NOT_FOUND));
                            }
                        }
                        List<string> csv = new List<string>();
                        ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataEventHandler = (sender, args) =>
                        {
                            GroupRole queryRole = args.Roles.Values.FirstOrDefault(o => o.ID.Equals(roleUUID));
                            csv.AddRange(typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .Where(
                                    o =>
                                        ((ulong) o.GetValue(null) &
                                         (ulong) queryRole.Powers) != 0)
                                .Select(o => o.Name));
                            GroupRoleDataReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleDataReply += GroupRoleDataEventHandler;
                        Client.Groups.RequestGroupRoles(groupUUID);
                        if (!GroupRoleDataReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_ROLE_POWERS));
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.POWERS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.DELETEROLE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.DeleteRole,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        UUID roleUUID;
                        if (!UUID.TryParse(role, out roleUUID) && !RoleNameToRoleUUID(role, groupUUID,
                            Configuration.SERVICES_TIMEOUT, ref roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.ROLE_NOT_FOUND));
                        }
                        if (roleUUID.Equals(UUID.Zero))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.CANNOT_DELETE_THE_EVERYONE_ROLE));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref commandGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (commandGroup.OwnerRole.Equals(roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.CANNOT_REMOVE_OWNER_ROLE));
                        }
                        // remove member from role
                        ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler = (sender, args) =>
                        {
                            Parallel.ForEach(args.RolesMembers.Where(o => o.Key.Equals(roleUUID)),
                                o => Client.Groups.RemoveFromRole(groupUUID, roleUUID, o.Value));
                            GroupRoleMembersReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_EJECTING_AGENT));
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                        Client.Groups.DeleteRole(groupUUID, roleUUID);
                    };
                    break;
                case ScriptKeys.ADDTOROLE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.AssignMember,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        UUID roleUUID;
                        if (!UUID.TryParse(role, out roleUUID) && !RoleNameToRoleUUID(role, groupUUID,
                            Configuration.SERVICES_TIMEOUT, ref roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.ROLE_NOT_FOUND));
                        }
                        if (roleUUID.Equals(UUID.Zero))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.GROUP_MEMBERS_ARE_BY_DEFAULT_IN_THE_EVERYONE_ROLE));
                        }
                        Client.Groups.AddToRole(groupUUID, roleUUID, agentUUID);
                    };
                    break;
                case ScriptKeys.DELETEFROMROLE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.RemoveMember,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        string role =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROLE), message));
                        UUID roleUUID;
                        if (!UUID.TryParse(role, out roleUUID) && !RoleNameToRoleUUID(role, groupUUID,
                            Configuration.SERVICES_TIMEOUT, ref roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.ROLE_NOT_FOUND));
                        }
                        if (roleUUID.Equals(UUID.Zero))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.CANNOT_DELETE_A_GROUP_MEMBER_FROM_THE_EVERYONE_ROLE));
                        }
                        OpenMetaverse.Group commandGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref commandGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (commandGroup.OwnerRole.Equals(roleUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.CANNOT_REMOVE_USER_FROM_OWNER_ROLE));
                        }
                        Client.Groups.RemoveFromRole(groupUUID, roleUUID,
                            agentUUID);
                    };
                    break;
                case ScriptKeys.TELL:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_TALK))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        switch (
                            (Entity)
                                wasGetEnumValueFromDescription<Entity>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Entity.AVATAR:
                                UUID agentUUID = UUID.Zero;
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref agentUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                Client.Self.InstantMessage(agentUUID,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE),
                                        message)));
                                break;
                            case Entity.GROUP:
                                UUID groupUUID =
                                    Configuration.GROUPS.FirstOrDefault(
                                        o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                                if (groupUUID.Equals(UUID.Zero) &&
                                    !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                                }
                                if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                                }
                                if (!Client.Self.GroupChatSessions.ContainsKey(groupUUID))
                                {
                                    if (
                                        !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.JoinChat,
                                            Configuration.SERVICES_TIMEOUT))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    }
                                    bool succeeded = false;
                                    ManualResetEvent GroupChatJoinedEvent = new ManualResetEvent(false);
                                    EventHandler<GroupChatJoinedEventArgs> GroupChatJoinedEventHandler =
                                        (sender, args) =>
                                        {
                                            if (args.Success)
                                            {
                                                succeeded = true;
                                            }
                                            GroupChatJoinedEvent.Set();
                                        };
                                    Client.Self.GroupChatJoined += GroupChatJoinedEventHandler;
                                    Client.Self.RequestJoinGroupChat(groupUUID);
                                    if (!GroupChatJoinedEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                    {
                                        Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                                        throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_JOINING_GROUP_CHAT));
                                    }
                                    Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                                    if (!succeeded)
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.UNABLE_TO_JOIN_GROUP_CHAT));
                                    }
                                }
                                Client.Self.InstantMessageGroup(groupUUID,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE),
                                        message)));
                                break;
                            case Entity.LOCAL:
                                int chatChannel;
                                if (
                                    !int.TryParse(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.CHANNEL),
                                            message)),
                                        out chatChannel))
                                {
                                    chatChannel = 0;
                                }
                                FieldInfo chatTypeInfo = typeof (ChatType).GetFields(BindingFlags.Public |
                                                                                     BindingFlags.Static)
                                    .FirstOrDefault(
                                        o =>
                                            o.Name.Equals(
                                                wasUriUnescapeDataString(
                                                    wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message)),
                                                StringComparison.Ordinal));
                                Client.Self.Chat(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE),
                                        message)),
                                    chatChannel,
                                    chatTypeInfo != null
                                        ? (ChatType)
                                            chatTypeInfo
                                                .GetValue(null)
                                        : ChatType.Normal);
                                break;
                            case Entity.ESTATE:
                                Client.Estate.EstateMessage(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE),
                                        message)));
                                break;
                            case Entity.REGION:
                                Client.Estate.SimulatorMessage(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE),
                                        message)));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                    };
                    break;
                case ScriptKeys.NOTICE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.SendNotices,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        GroupNotice notice = new GroupNotice
                        {
                            Message =
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE), message)),
                            Subject =
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.SUBJECT), message))
                        };
                        string item =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message));
                        if (!string.IsNullOrEmpty(item) && !UUID.TryParse(item, out notice.AttachmentID))
                        {
                            InventoryItem inventoryItem =
                                SearchInventoryItem(Client.Inventory.Store.RootFolder, item,
                                    Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                            if (inventoryItem == null)
                            {
                                throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                            }
                            notice.OwnerID = Client.Self.AgentID;
                            notice.AttachmentID = inventoryItem.UUID;
                        }
                        Client.Groups.SendGroupNotice(groupUUID, notice);
                    };
                    break;
                case ScriptKeys.PAY:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group, (int) Permissions.PERMISSION_ECONOMY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        int amount;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.AMOUNT), message)),
                                out amount))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PAY_AMOUNT));
                        }
                        if (amount.Equals(0))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PAY_AMOUNT));
                        }
                        ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
                        EventHandler<BalanceEventArgs> MoneyBalanceEventHandler =
                            (sender, args) => MoneyBalanceEvent.Set();
                        Client.Self.MoneyBalance += MoneyBalanceEventHandler;
                        Client.Self.RequestBalance();
                        if (!MoneyBalanceEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_WAITING_FOR_BALANCE));
                        }
                        Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                        if (Client.Self.Balance < amount)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INSUFFICIENT_FUNDS));
                        }
                        UUID targetUUID = UUID.Zero;
                        switch (
                            (Entity)
                                wasGetEnumValueFromDescription<Entity>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Entity.GROUP:
                                targetUUID =
                                    Configuration.GROUPS.FirstOrDefault(
                                        o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                                if (targetUUID.Equals(UUID.Zero) &&
                                    !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                                }
                                Client.Self.GiveGroupMoney(targetUUID, amount,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REASON),
                                        message)));
                                break;
                            case Entity.AVATAR:
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                Client.Self.GiveAvatarMoney(targetUUID, amount,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REASON),
                                        message)));
                                break;
                            case Entity.OBJECT:
                                if (
                                    !UUID.TryParse(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TARGET),
                                            message)),
                                        out targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.INVALID_PAY_TARGET));
                                }
                                Client.Self.GiveObjectMoney(targetUUID, amount,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REASON),
                                        message)));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                    };
                    break;
                case ScriptKeys.GETBALANCE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group, (int) Permissions.PERMISSION_ECONOMY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
                        EventHandler<BalanceEventArgs> MoneyBalanceEventHandler =
                            (sender, args) => MoneyBalanceEvent.Set();
                        Client.Self.MoneyBalance += MoneyBalanceEventHandler;
                        Client.Self.RequestBalance();
                        if (!MoneyBalanceEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_WAITING_FOR_BALANCE));
                        }
                        Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                        result.Add(GetEnumDescription(ResultKeys.BALANCE),
                            Client.Self.Balance.ToString(CultureInfo.InvariantCulture));
                    };
                    break;
                case ScriptKeys.TELEPORT:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string region =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REGION), message));
                        if (string.IsNullOrEmpty(region))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_REGION_SPECIFIED));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        ManualResetEvent TeleportEvent = new ManualResetEvent(false);
                        bool succeeded = false;
                        EventHandler<TeleportEventArgs> TeleportEventHandler = (sender, args) =>
                        {
                            if (!args.Status.Equals(TeleportStatus.Finished))
                                return;
                            if (Client.Network.CurrentSim.Name.Equals(region, StringComparison.Ordinal))
                            {
                                succeeded = true;
                            }
                            TeleportEvent.Set();
                        };
                        if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                        {
                            Client.Self.Stand();
                        }
                        Client.Self.SignaledAnimations.ForEach(
                            animation => Client.Self.AnimationStop(animation.Key, true));
                        lock (TeleportLock)
                        {
                            Client.Self.TeleportProgress += TeleportEventHandler;
                            Client.Self.Teleport(region, position);

                            if (!TeleportEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                            {
                                Client.Self.TeleportProgress -= TeleportEventHandler;
                                throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_DURING_TELEPORT));
                            }
                            Client.Self.TeleportProgress -= TeleportEventHandler;
                        }
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.TELEPORT_FAILED));
                        }
                    };
                    break;
                case ScriptKeys.LURE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        Client.Self.SendTeleportLure(agentUUID,
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE), message)));
                    };
                    break;
                case ScriptKeys.SETHOME:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        bool succeeded = true;
                        EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                        {
                            if (args.Message.Equals(LINDEN_CONSTANTS.ALERTS.UNABLE_TO_SET_HOME))
                            {
                                succeeded = false;
                            }
                        };
                        Client.Self.AlertMessage += AlertMessageEventHandler;
                        Client.Self.SetHome();
                        if (!succeeded)
                        {
                            Client.Self.AlertMessage -= AlertMessageEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.UNABLE_TO_SET_HOME));
                        }
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                    };
                    break;
                case ScriptKeys.GOHOME:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                        {
                            Client.Self.Stand();
                        }
                        Client.Self.SignaledAnimations.ForEach(
                            animation => Client.Self.AnimationStop(animation.Key, true));
                        if (!Client.Self.GoHome())
                        {
                            throw new Exception(GetEnumDescription(ScriptError.UNABLE_TO_GO_HOME));
                        }
                    };
                    break;
                case ScriptKeys.GETREGIONDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(Client.Network.CurrentSim,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.SIT:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        ManualResetEvent SitEvent = new ManualResetEvent(false);
                        bool succeeded = false;
                        EventHandler<AvatarSitResponseEventArgs> AvatarSitEventHandler = (sender, args) =>
                        {
                            if (!args.ObjectID.Equals(UUID.Zero))
                            {
                                succeeded = true;
                            }
                            SitEvent.Set();
                        };
                        EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                        {
                            if (args.Message.Equals(LINDEN_CONSTANTS.ALERTS.NO_ROOM_TO_SIT_HERE))
                            {
                                succeeded = false;
                            }
                            SitEvent.Set();
                        };
                        if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                        {
                            Client.Self.Stand();
                        }
                        Client.Self.SignaledAnimations.ForEach(
                            animation => Client.Self.AnimationStop(animation.Key, true));
                        Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                        Client.Self.AlertMessage += AlertMessageEventHandler;
                        Client.Self.RequestSit(primitive.ID, Vector3.Zero);
                        if (!SitEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                            Client.Self.AlertMessage -= AlertMessageEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_REQUESTING_SIT));
                        }
                        Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_SIT));
                        }
                        Client.Self.Sit();
                    };
                    break;
                case ScriptKeys.STAND:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                        {
                            Client.Self.Stand();
                        }
                        Client.Self.SignaledAnimations.ForEach(
                            animation => Client.Self.AnimationStop(animation.Key, true));
                    };
                    break;
                case ScriptKeys.GETPARCELLIST:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(o => o.Name.Equals(group, StringComparison.Ordinal))
                                .UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        FieldInfo accessField = typeof (AccessList).GetFields(
                            BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE),
                                            message)),
                                        StringComparison.Ordinal));
                        if (accessField == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ACCESS_LIST_TYPE));
                        }
                        AccessList accessType = (AccessList) accessField.GetValue(null);
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                                switch (accessType)
                                {
                                    case AccessList.Access:
                                        if (
                                            !HasGroupPowers(Client.Self.AgentID, groupUUID,
                                                GroupPowers.LandManageAllowed, Configuration.SERVICES_TIMEOUT))
                                        {
                                            throw new Exception(
                                                GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                        }
                                        break;
                                    case AccessList.Ban:
                                        if (
                                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandManageBanned,
                                                Configuration.SERVICES_TIMEOUT))
                                        {
                                            throw new Exception(
                                                GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                        }
                                        break;
                                    case AccessList.Both:
                                        if (
                                            !HasGroupPowers(Client.Self.AgentID, groupUUID,
                                                GroupPowers.LandManageAllowed, Configuration.SERVICES_TIMEOUT))
                                        {
                                            throw new Exception(
                                                GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                        }
                                        if (
                                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandManageBanned,
                                                Configuration.SERVICES_TIMEOUT))
                                        {
                                            throw new Exception(
                                                GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                        }
                                        break;
                                }
                            }
                        }
                        List<string> csv = new List<string>();
                        ManualResetEvent ParcelAccessListEvent = new ManualResetEvent(false);
                        EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                        {
                            foreach (ParcelManager.ParcelAccessEntry parcelAccess in args.AccessList)
                            {
                                string agent = string.Empty;
                                if (!AgentUUIDToName(parcelAccess.AgentID, Configuration.SERVICES_TIMEOUT, ref agent))
                                    continue;
                                csv.Add(agent);
                                csv.Add(parcelAccess.AgentID.ToString());
                                csv.Add(parcelAccess.Flags.ToString());
                                csv.Add(parcelAccess.Time.ToString(CultureInfo.InvariantCulture));
                            }
                            ParcelAccessListEvent.Set();
                        };
                        Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                        Client.Parcels.RequestParcelAccessList(Client.Network.CurrentSim, parcel.LocalID, accessType, 0);
                        if (!ParcelAccessListEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                        }
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.LIST),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                    csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.PARCELRECLAIM:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        Client.Parcels.Reclaim(Client.Network.CurrentSim, parcel.LocalID);
                    };
                    break;
                case ScriptKeys.PARCELRELEASE:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(o => o.Name.Equals(group, StringComparison.Ordinal))
                                .UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                                if (
                                    !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandRelease,
                                        Configuration.SERVICES_TIMEOUT))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                            }
                        }
                        Client.Parcels.ReleaseParcel(Client.Network.CurrentSim, parcel.LocalID);
                    };
                    break;
                case ScriptKeys.PARCELDEED:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(o => o.Name.Equals(group, StringComparison.Ordinal))
                                .UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                            }
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandDeed,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        Client.Parcels.DeedToGroup(Client.Network.CurrentSim, parcel.LocalID, groupUUID);
                    };
                    break;
                case ScriptKeys.PARCELBUY:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        bool forGroup;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FORGROUP), message)),
                                out forGroup))
                        {
                            if (
                                !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandDeed,
                                    Configuration.SERVICES_TIMEOUT))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                            }
                            forGroup = true;
                        }
                        bool removeContribution;
                        if (!bool.TryParse(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REMOVECONTRIBUTION),
                                message)),
                            out removeContribution))
                        {
                            removeContribution = true;
                        }
                        ManualResetEvent ParcelInfoEvent = new ManualResetEvent(false);
                        UUID parcelUUID = UUID.Zero;
                        EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                        {
                            parcelUUID = args.Parcel.ID;
                            ParcelInfoEvent.Set();
                        };
                        Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                        Client.Parcels.RequestParcelInfo(parcelUUID);
                        if (!ParcelInfoEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                        bool forSale = false;
                        int handledEvents = 0;
                        int counter = 1;
                        ManualResetEvent DirLandReplyEvent = new ManualResetEvent(false);
                        EventHandler<DirLandReplyEventArgs> DirLandReplyEventArgs =
                            (sender, args) =>
                            {
                                handledEvents += args.DirParcels.Count;
                                Parallel.ForEach(args.DirParcels, o =>
                                {
                                    if (o.ID.Equals(parcelUUID))
                                    {
                                        forSale = o.ForSale;
                                        DirLandReplyEvent.Set();
                                    }
                                });
                                if ((handledEvents - counter)%
                                    LINDEN_CONSTANTS.DIRECTORY.LAND.SEARCH_RESULTS_COUNT == 0)
                                {
                                    ++counter;
                                    Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                        DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue,
                                        handledEvents);
                                }
                                DirLandReplyEvent.Set();
                            };
                        Client.Directory.DirLandReply += DirLandReplyEventArgs;
                        Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                            DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue, handledEvents);
                        if (!DirLandReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                        }
                        Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                        if (!forSale)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PARCEL_NOT_FOR_SALE));
                        }
                        ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
                        EventHandler<BalanceEventArgs> MoneyBalanceEventHandler =
                            (sender, args) => MoneyBalanceEvent.Set();
                        Client.Self.MoneyBalance += MoneyBalanceEventHandler;
                        Client.Self.RequestBalance();
                        if (!MoneyBalanceEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_WAITING_FOR_BALANCE));
                        }
                        Client.Self.MoneyBalance -= MoneyBalanceEventHandler;
                        if (Client.Self.Balance < parcel.SalePrice)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INSUFFICIENT_FUNDS));
                        }
                        Client.Parcels.Buy(Client.Network.CurrentSim, parcel.LocalID, forGroup, groupUUID,
                            removeContribution, parcel.Area, parcel.SalePrice);
                    };
                    break;
                case ScriptKeys.PARCELEJECT:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                                if (!HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandEjectAndFreeze,
                                    Configuration.SERVICES_TIMEOUT))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                            }
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        bool alsoban;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.BAN), message)),
                                out alsoban))
                        {
                            alsoban = false;
                        }
                        Client.Parcels.EjectUser(agentUUID, alsoban);
                    };
                    break;
                case ScriptKeys.PARCELFREEZE:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                                if (!HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.LandEjectAndFreeze,
                                    Configuration.SERVICES_TIMEOUT))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                            }
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        bool freeze;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FREEZE), message)),
                                out freeze))
                        {
                            freeze = false;
                        }
                        Client.Parcels.FreezeUser(agentUUID, freeze);
                    };
                    break;
                case ScriptKeys.PARCELMUSIC:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                                if (!HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.ChangeMedia,
                                    Configuration.SERVICES_TIMEOUT))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                }
                            }
                        }
                        parcel.MusicURL =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.URL), message));
                        parcel.Update(Client.Network.CurrentSim, true);
                    };
                    break;
                case ScriptKeys.SETPROFILEDATA:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        ManualResetEvent[] AvatarProfileDataEvent =
                        {
                            new ManualResetEvent(false),
                            new ManualResetEvent(false)
                        };
                        Avatar.AvatarProperties properties = new Avatar.AvatarProperties();
                        Avatar.Interests interests = new Avatar.Interests();
                        EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesEventHandler = (sender, args) =>
                        {
                            properties = args.Properties;
                            AvatarProfileDataEvent[0].Set();
                        };
                        EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsEventHandler = (sender, args) =>
                        {
                            interests = args.Interests;
                            AvatarProfileDataEvent[1].Set();
                        };
                        Client.Avatars.AvatarPropertiesReply += AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply += AvatarInterestsEventHandler;
                        Client.Avatars.RequestAvatarProperties(Client.Self.AgentID);
                        if (
                            !WaitHandle.WaitAll(AvatarProfileDataEvent.Select(o => (WaitHandle) o).ToArray(),
                                Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                            Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PROFILE));
                        }
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                        string fields =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message));
                        wasCSVToStructure(fields, ref properties);
                        wasCSVToStructure(fields, ref interests);
                        Client.Self.UpdateProfile(properties);
                        Client.Self.UpdateInterests(interests);
                    };
                    break;
                case ScriptKeys.GETPROFILEDATA:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        ManualResetEvent[] AvatarProfileDataEvent =
                        {
                            new ManualResetEvent(false),
                            new ManualResetEvent(false)
                        };
                        Avatar.AvatarProperties properties = new Avatar.AvatarProperties();
                        Avatar.Interests interests = new Avatar.Interests();
                        EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesEventHandler = (sender, args) =>
                        {
                            properties = args.Properties;
                            AvatarProfileDataEvent[0].Set();
                        };
                        EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsEventHandler = (sender, args) =>
                        {
                            interests = args.Interests;
                            AvatarProfileDataEvent[1].Set();
                        };
                        Client.Avatars.AvatarPropertiesReply += AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply += AvatarInterestsEventHandler;
                        Client.Avatars.RequestAvatarProperties(agentUUID);
                        if (
                            !WaitHandle.WaitAll(AvatarProfileDataEvent.Select(o => (WaitHandle) o).ToArray(),
                                Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                            Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PROFILE));
                        }
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                        string fields =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message));
                        List<string> csv = new List<string>();
                        csv.AddRange(GetStructuredData(properties, fields));
                        csv.AddRange(GetStructuredData(interests, fields));
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.DATA),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GIVE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        InventoryItem item =
                            SearchInventoryItem(Client.Inventory.Store.RootFolder,
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        switch (
                            (Entity)
                                wasGetEnumValueFromDescription<Entity>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Entity.AVATAR:
                                UUID agentUUID = UUID.Zero;
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref agentUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                Client.Inventory.GiveItem(item.UUID, item.Name, item.AssetType, agentUUID, true);
                                break;
                            case Entity.OBJECT:
                                float range;
                                if (
                                    !float.TryParse(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE),
                                            message)),
                                        out range))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                                }
                                Primitive primitive = null;
                                if (
                                    !FindPrimitive(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TARGET),
                                            message)),
                                        range,
                                        Configuration.SERVICES_TIMEOUT,
                                        ref primitive))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                                }
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID, item);
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                    };
                    break;
                case ScriptKeys.DELETEITEM:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        HashSet<InventoryItem> items =
                            new HashSet<InventoryItem>(SearchInventoryItem(Client.Inventory.Store.RootFolder,
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                Configuration.SERVICES_TIMEOUT));
                        if (items.Count == 0)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        Parallel.ForEach(items, o =>
                        {
                            switch (o.AssetType)
                            {
                                case AssetType.Folder:
                                    Client.Inventory.MoveFolder(o.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    break;
                                default:
                                    Client.Inventory.MoveItem(o.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    break;
                            }
                        });
                    };
                    break;
                case ScriptKeys.EMPTYTRASH:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Client.Inventory.EmptyTrash();
                    };
                    break;
                case ScriptKeys.FLY:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        uint action =
                            wasGetEnumValueFromDescription<Action>(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                    .ToLower(CultureInfo.InvariantCulture));
                        switch ((Action) action)
                        {
                            case Action.START:
                            case Action.STOP:
                                if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                                {
                                    Client.Self.Stand();
                                }
                                Client.Self.SignaledAnimations.ForEach(
                                    o => Client.Self.AnimationStop(o.Key, true));
                                Client.Self.Fly(action.Equals((uint) Action.START));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.FLY_ACTION_START_OR_STOP));
                        }
                    };
                    break;
                case ScriptKeys.ADDPICK:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string item =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message));
                        UUID textureUUID = UUID.Zero;
                        if (!string.IsNullOrEmpty(item) && !UUID.TryParse(item, out textureUUID))
                        {
                            InventoryItem inventoryItem =
                                SearchInventoryItem(Client.Inventory.Store.RootFolder, item,
                                    Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                            if (inventoryItem == null)
                            {
                                throw new Exception(GetEnumDescription(ScriptError.TEXTURE_NOT_FOUND));
                            }
                            textureUUID = inventoryItem.UUID;
                        }
                        ManualResetEvent AvatarPicksReplyEvent = new ManualResetEvent(false);
                        UUID pickUUID = UUID.Zero;
                        string pickName =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        if (string.IsNullOrEmpty(pickName))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_PICK_NAME));
                        }
                        EventHandler<AvatarPicksReplyEventArgs> AvatarPicksEventHandler = (sender, args) =>
                        {
                            pickUUID =
                                args.Picks.FirstOrDefault(
                                    o => o.Value.Equals(pickName, StringComparison.Ordinal)).Key;
                            AvatarPicksReplyEvent.Set();
                        };
                        Client.Avatars.AvatarPicksReply += AvatarPicksEventHandler;
                        Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                        if (!AvatarPicksReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PICKS));
                        }
                        Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                        if (pickUUID.Equals(UUID.Zero))
                        {
                            pickUUID = UUID.Random();
                        }
                        Client.Self.PickInfoUpdate(pickUUID, false, UUID.Zero, pickName,
                            Client.Self.GlobalPosition, textureUUID,
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DESCRIPTION), message)));
                    };
                    break;
                case ScriptKeys.DELETEPICK:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        ManualResetEvent AvatarPicksReplyEvent = new ManualResetEvent(false);
                        string pickName =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        if (string.IsNullOrEmpty(pickName))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_PICK_NAME));
                        }
                        UUID pickUUID = UUID.Zero;
                        EventHandler<AvatarPicksReplyEventArgs> AvatarPicksEventHandler = (sender, args) =>
                        {
                            pickUUID =
                                args.Picks.FirstOrDefault(
                                    o => o.Value.Equals(pickName, StringComparison.Ordinal)).Key;
                            AvatarPicksReplyEvent.Set();
                        };
                        Client.Avatars.AvatarPicksReply += AvatarPicksEventHandler;
                        Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                        if (!AvatarPicksReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PICKS));
                        }
                        Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                        if (pickUUID.Equals(UUID.Zero))
                        {
                            pickUUID = UUID.Random();
                        }
                        Client.Self.PickDelete(pickUUID);
                    };
                    break;
                case ScriptKeys.ADDCLASSIFIED:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string item =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message));
                        UUID textureUUID = UUID.Zero;
                        if (!string.IsNullOrEmpty(item) && !UUID.TryParse(item, out textureUUID))
                        {
                            InventoryItem inventoryItem =
                                SearchInventoryItem(Client.Inventory.Store.RootFolder, item,
                                    Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                            if (inventoryItem == null)
                            {
                                throw new Exception(GetEnumDescription(ScriptError.TEXTURE_NOT_FOUND));
                            }
                            textureUUID = inventoryItem.UUID;
                        }
                        string classifiedName =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        if (string.IsNullOrEmpty(classifiedName))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_CLASSIFIED_NAME));
                        }
                        string classifiedDescription =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DESCRIPTION), message));
                        ManualResetEvent AvatarClassifiedReplyEvent = new ManualResetEvent(false);
                        UUID classifiedUUID = UUID.Zero;
                        EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                        {
                            classifiedUUID =
                                args.Classifieds.FirstOrDefault(
                                    o =>
                                        o.Value.Equals(classifiedName, StringComparison.Ordinal)).Key;
                            AvatarClassifiedReplyEvent.Set();
                        };
                        Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                        Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                        if (!AvatarClassifiedReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_CLASSIFIEDS));
                        }
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                        if (classifiedUUID.Equals(UUID.Zero))
                        {
                            classifiedUUID = UUID.Random();
                        }
                        int price;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PRICE), message)),
                                out price))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PRICE));
                        }
                        if (price < 0)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PRICE));
                        }
                        bool renew;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RENEW), message)),
                                out renew))
                        {
                            renew = false;
                        }
                        FieldInfo classifiedCategoriesField = typeof (DirectoryManager.ClassifiedCategories).GetFields(
                            BindingFlags.Public |
                            BindingFlags.Static)
                            .FirstOrDefault(o =>
                                o.Name.Equals(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message)),
                                    StringComparison.Ordinal));
                        Client.Self.UpdateClassifiedInfo(classifiedUUID, classifiedCategoriesField != null
                            ? (DirectoryManager.ClassifiedCategories)
                                classifiedCategoriesField.GetValue(null)
                            : DirectoryManager.ClassifiedCategories.Any, textureUUID, price,
                            classifiedName, classifiedDescription, renew);
                    };
                    break;
                case ScriptKeys.DELETECLASSIFIED:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string classifiedName =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        if (string.IsNullOrEmpty(classifiedName))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_CLASSIFIED_NAME));
                        }
                        ManualResetEvent AvatarClassifiedReplyEvent = new ManualResetEvent(false);
                        UUID classifiedUUID = UUID.Zero;
                        EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                        {
                            classifiedUUID =
                                args.Classifieds.FirstOrDefault(
                                    o =>
                                        o.Value.Equals(classifiedName, StringComparison.Ordinal)).Key;
                            AvatarClassifiedReplyEvent.Set();
                        };
                        Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                        Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                        if (!AvatarClassifiedReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_CLASSIFIEDS));
                        }
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                        if (classifiedUUID.Equals(UUID.Zero))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_CLASSIFIED));
                        }
                        Client.Self.DeleteClassfied(classifiedUUID);
                    };
                    break;
                case ScriptKeys.TOUCH:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Client.Self.Touch(primitive.LocalID);
                    };
                    break;
                case ScriptKeys.MODERATE:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.ModerateChat,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        if (!AgentInGroup(agentUUID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_IN_GROUP));
                        }
                        bool silence;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.SILENCE), message)),
                                out silence))
                        {
                            silence = false;
                        }
                        uint type =
                            wasGetEnumValueFromDescription<Type>(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                    .ToLower(CultureInfo.InvariantCulture));
                        switch ((Type) type)
                        {
                            case Type.TEXT:
                            case Type.VOICE:
                                Client.Self.ModerateChatSessions(groupUUID, agentUUID, GetEnumDescription((Type) type),
                                    silence);
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.TYPE_CAN_BE_VOICE_OR_TEXT));
                        }
                    };
                    break;
                case ScriptKeys.REBAKE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Client.Appearance.RequestSetAppearance(true);
                    };
                    break;
                case ScriptKeys.GETWEARABLES:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.WEARABLES),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetWearables(Client.Inventory.Store.RootFolder, Configuration.SERVICES_TIMEOUT)
                                    .Select(o => new[]
                                    {
                                        o.Key.ToString(),
                                        o.Value
                                    }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.WEAR:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string wearables =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.WEARABLES), message));
                        if (string.IsNullOrEmpty(wearables))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_WEARABLES));
                        }
                        Parallel.ForEach(Regex.Matches(wearables,
                            @"\s*(?<key>.+?)\s*,\s*(?<value>.+?)\s*(,|$)",
                            RegexOptions.Compiled)
                            .Cast<Match>()
                            .ToDictionary(o => o.Groups["key"].Value, o => o.Groups["value"].Value),
                            o => Parallel.ForEach(
                                typeof (WearableType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .Where(
                                        q =>
                                            q.Name.Equals(o.Key, StringComparison.Ordinal)),
                                q =>
                                {
                                    InventoryItem item =
                                        SearchInventoryItem(Client.Inventory.Store.RootFolder, o.Value,
                                            Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                                    if (item == null)
                                        return;
                                    InventoryWearable wearable = item as InventoryWearable;
                                    if (wearable == null)
                                        return;
                                    if (!wearable.WearableType.Equals((WearableType) q.GetValue(null)))
                                        return;
                                    Client.Appearance.AddToOutfit(item);
                                }));
                    };
                    break;
                case ScriptKeys.UNWEAR:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string wearables =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.WEARABLES), message));
                        if (string.IsNullOrEmpty(wearables))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_WEARABLES));
                        }
                        Parallel.ForEach(
                            wearables.Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER},
                                StringSplitOptions.RemoveEmptyEntries), o =>
                                {
                                    InventoryItem item =
                                        SearchInventoryItem(Client.Inventory.Store.RootFolder, o,
                                            Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                                    if (item == null)
                                        return;
                                    InventoryWearable wearable = item as InventoryWearable;
                                    if (wearable == null)
                                        return;
                                    Client.Appearance.RemoveFromOutfit(item);
                                });
                    };
                    break;
                case ScriptKeys.GETATTACHMENTS:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.ATTACHMENTS),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetAttachments(Configuration.SERVICES_TIMEOUT).Select(o => new[]
                                {
                                    o.Key.ToString(),
                                    o.Value.Properties.Name
                                }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.ATTACH:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string attachments =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ATTACHMENTS), message));
                        if (string.IsNullOrEmpty(attachments))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_ATTACHMENTS));
                        }
                        Parallel.ForEach(Regex.Matches(attachments, @"\s*(?<key>.+?)\s*,\s*(?<value>.+?)\s*(,|$)",
                            RegexOptions.Compiled)
                            .Cast<Match>()
                            .ToDictionary(o => o.Groups["key"].Value, o => o.Groups["value"].Value),
                            o =>
                                Parallel.ForEach(
                                    typeof (AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(
                                            p =>
                                                p.Name.Equals(o.Key, StringComparison.Ordinal)),
                                    q =>
                                    {
                                        InventoryItem item =
                                            SearchInventoryItem(Client.Inventory.Store.RootFolder, o.Value,
                                                Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                                        if (item == null)
                                            return;
                                        Client.Appearance.Attach(item, (AttachmentPoint) q.GetValue(null),
                                            true);
                                    }));
                    };
                    break;
                case ScriptKeys.DETACH:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string attachments =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ATTACHMENTS), message));
                        if (string.IsNullOrEmpty(attachments))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.EMPTY_ATTACHMENTS));
                        }
                        Parallel.ForEach(
                            attachments.Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER},
                                StringSplitOptions.RemoveEmptyEntries), o =>
                                {
                                    InventoryItem item =
                                        SearchInventoryItem(Client.Inventory.Store.RootFolder, o,
                                            Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                                    if (item != null)
                                    {
                                        Client.Appearance.Detach(item);
                                    }
                                });
                    };
                    break;
                case ScriptKeys.RETURNPRIMITIVES:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        string type =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message));
                        switch (
                            (Entity)
                                wasGetEnumValueFromDescription<Entity>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Entity.PARCEL:
                                Vector3 position;
                                HashSet<Parcel> parcels = new HashSet<Parcel>();
                                switch (Vector3.TryParse(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION),
                                        message)),
                                    out position))
                                {
                                    case false:
                                        // Get all sim parcels
                                        ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                                        EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                            (sender, args) => SimParcelsDownloadedEvent.Set();
                                        Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                                        Client.Parcels.RequestAllSimParcels(Client.Network.CurrentSim);
                                        if (Client.Network.CurrentSim.IsParcelMapFull())
                                        {
                                            SimParcelsDownloadedEvent.Set();
                                        }
                                        if (!SimParcelsDownloadedEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                        {
                                            Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                                        }
                                        Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                        Client.Network.CurrentSim.Parcels.ForEach(o => parcels.Add(o));
                                        break;
                                    case true:
                                        Parcel parcel = null;
                                        if (!GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                                        {
                                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                                        }
                                        parcels.Add(parcel);
                                        break;
                                }
                                FieldInfo objectReturnTypeField = typeof (ObjectReturnType).GetFields(
                                    BindingFlags.Public |
                                    BindingFlags.Static)
                                    .FirstOrDefault(
                                        o =>
                                            o.Name.Equals(type
                                                .ToLower(CultureInfo.InvariantCulture),
                                                StringComparison.Ordinal));
                                ObjectReturnType returnType = objectReturnTypeField != null
                                    ? (ObjectReturnType)
                                        objectReturnTypeField
                                            .GetValue(null)
                                    : ObjectReturnType.Other;
                                if (!Client.Network.CurrentSim.IsEstateManager)
                                {
                                    Parallel.ForEach(parcels, o =>
                                    {
                                        if (!o.OwnerID.Equals(Client.Self.AgentID))
                                        {
                                            if (!o.IsGroupOwned || !o.GroupID.Equals(groupUUID))
                                            {
                                                throw new Exception(
                                                    GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                            }
                                            GroupPowers power = new GroupPowers();
                                            switch (returnType)
                                            {
                                                case ObjectReturnType.Other:
                                                    power = GroupPowers.ReturnNonGroup;
                                                    break;
                                                case ObjectReturnType.Group:
                                                    power = GroupPowers.ReturnGroupSet;
                                                    break;
                                                case ObjectReturnType.Owner:
                                                    power = GroupPowers.ReturnGroupOwned;
                                                    break;
                                            }
                                            if (!HasGroupPowers(Client.Self.AgentID, groupUUID, power,
                                                Configuration.SERVICES_TIMEOUT))
                                            {
                                                throw new Exception(
                                                    GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                            }
                                        }
                                    });
                                    Parallel.ForEach(parcels,
                                        o =>
                                            Client.Parcels.ReturnObjects(Client.Network.CurrentSim, o.LocalID,
                                                returnType
                                                , new List<UUID> {agentUUID}));
                                }
                                break;
                            case Entity.ESTATE:
                                if (!Client.Network.CurrentSim.IsEstateManager)
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                                }
                                bool allEstates;
                                if (
                                    !bool.TryParse(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ALL),
                                            message)),
                                        out allEstates))
                                {
                                    allEstates = false;
                                }
                                FieldInfo estateReturnFlagsField = typeof (EstateTools.EstateReturnFlags).GetFields(
                                    BindingFlags.Public | BindingFlags.Static)
                                    .FirstOrDefault(
                                        o =>
                                            o.Name.Equals(type,
                                                StringComparison.Ordinal));
                                Client.Estate.SimWideReturn(agentUUID, estateReturnFlagsField != null
                                    ? (EstateTools.EstateReturnFlags)
                                        estateReturnFlagsField
                                            .GetValue(null)
                                    : EstateTools.EstateReturnFlags.ReturnScriptedAndOnOthers, allEstates);
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                    };
                    break;
                case ScriptKeys.GETPRIMITIVEOWNERS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        Vector3 position;
                        HashSet<Parcel> parcels = new HashSet<Parcel>();
                        switch (Vector3.TryParse(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                            out position))
                        {
                            case false:
                                // Get all sim parcels
                                ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                                EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                    (sender, args) => SimParcelsDownloadedEvent.Set();
                                Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                                Client.Parcels.RequestAllSimParcels(Client.Network.CurrentSim);
                                if (Client.Network.CurrentSim.IsParcelMapFull())
                                {
                                    SimParcelsDownloadedEvent.Set();
                                }
                                if (!SimParcelsDownloadedEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                {
                                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                    throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                                }
                                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                Client.Network.CurrentSim.Parcels.ForEach(o => parcels.Add(o));
                                break;
                            case true:
                                Parcel parcel = null;
                                if (!GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                                }
                                parcels.Add(parcel);
                                break;
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            Parallel.ForEach(parcels, o =>
                            {
                                if (!o.OwnerID.Equals(Client.Self.AgentID))
                                {
                                    if (!o.IsGroupOwned || !o.GroupID.Equals(groupUUID))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    }
                                    bool permissions = false;
                                    Parallel.ForEach(
                                        new HashSet<GroupPowers>
                                        {
                                            GroupPowers.ReturnGroupSet,
                                            GroupPowers.ReturnGroupOwned,
                                            GroupPowers.ReturnNonGroup
                                        }, p =>
                                        {
                                            if (HasGroupPowers(Client.Self.AgentID, groupUUID, p,
                                                Configuration.SERVICES_TIMEOUT))
                                            {
                                                permissions = true;
                                            }
                                        });
                                    if (!permissions)
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    }
                                }
                            });
                        }
                        ManualResetEvent ParcelObjectOwnersReplyEvent = new ManualResetEvent(false);
                        Dictionary<string, int> primitives = new Dictionary<string, int>();
                        EventHandler<ParcelObjectOwnersReplyEventArgs> ParcelObjectOwnersEventHandler =
                            (sender, args) =>
                            {
                                //object LockObject = new object();
                                foreach (ParcelManager.ParcelPrimOwners primowner in args.PrimOwners)
                                {
                                    string owner = string.Empty;
                                    if (!AgentUUIDToName(primowner.OwnerID, Configuration.SERVICES_TIMEOUT, ref owner))
                                        continue;
                                    if (!primitives.ContainsKey(owner))
                                    {
                                        primitives.Add(owner, primowner.Count);
                                        continue;
                                    }
                                    primitives[owner] += primowner.Count;
                                }
                                ParcelObjectOwnersReplyEvent.Set();
                            };
                        foreach (Parcel parcel in parcels)
                        {
                            Client.Parcels.ParcelObjectOwnersReply += ParcelObjectOwnersEventHandler;
                            Client.Parcels.RequestObjectOwners(Client.Network.CurrentSim, parcel.LocalID);
                            if (!ParcelObjectOwnersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                            {
                                Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                                throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_LAND_USERS));
                            }
                            Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                        }
                        if (primitives.Count == 0)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_GET_LAND_USERS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.OWNERS),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                primitives.Select(
                                    p =>
                                        string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                            new[] {p.Key, p.Value.ToString(CultureInfo.InvariantCulture)}))
                                    .ToArray()
                                ));
                    };
                    break;
                case ScriptKeys.GETGROUPDATA:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        OpenMetaverse.Group dataGroup = new OpenMetaverse.Group();
                        if (!RequestGroup(groupUUID, Configuration.SERVICES_TIMEOUT, ref dataGroup))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(dataGroup,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.GETPRIMITIVEDATA:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(primitive,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.GETPARCELDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Parcel parcel = null;
                        if (
                            !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(parcel,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.REZ:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        InventoryItem item =
                            SearchInventoryItem(Client.Inventory.Store.RootFolder,
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_POSITION));
                        }
                        Quaternion rotation;
                        if (
                            !Quaternion.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROTATION), message)),
                                out rotation))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_ROTATION));
                        }
                        Parcel parcel = null;
                        if (!GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                        }
                        if ((parcel.Flags & ParcelFlags.CreateObjects) == 0)
                        {
                            if (!Client.Network.CurrentSim.IsEstateManager)
                            {
                                if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                                {
                                    if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(groupUUID))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    }
                                    if (!HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.AllowRez,
                                        Configuration.SERVICES_TIMEOUT))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    }
                                }
                            }
                        }
                        Client.Inventory.RequestRezFromInventory(Client.Network.CurrentSim, rotation, position, item,
                            groupUUID);
                    };
                    break;
                case ScriptKeys.DEREZ:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Client.Inventory.RequestDeRezToInventory(primitive.LocalID);
                    };
                    break;
                case ScriptKeys.SETSCRIPTRUNNING:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        string entity =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message));
                        UUID entityUUID;
                        if (!UUID.TryParse(entity, out entityUUID))
                        {
                            if (string.IsNullOrEmpty(entity))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                            }
                            entityUUID = UUID.Zero;
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        List<InventoryBase> inventory =
                            Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                Configuration.SERVICES_TIMEOUT).ToList();
                        InventoryItem item = !entityUUID.Equals(UUID.Zero)
                            ? inventory.FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                            : inventory.FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        switch (item.AssetType)
                        {
                            case AssetType.LSLBytecode:
                            case AssetType.LSLText:
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.ITEM_IS_NOT_A_SCRIPT));
                        }
                        uint action =
                            wasGetEnumValueFromDescription<Action>(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                    .ToLower(CultureInfo.InvariantCulture));
                        switch ((Action) action)
                        {
                            case Action.START:
                            case Action.STOP:
                                Client.Inventory.RequestSetScriptRunning(primitive.ID, item.UUID,
                                    action.Equals((uint) Action.START));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ACTION));
                        }
                        ManualResetEvent ScriptRunningReplyEvent = new ManualResetEvent(false);
                        bool succeeded = false;
                        EventHandler<ScriptRunningReplyEventArgs> ScriptRunningEventHandler = (sender, args) =>
                        {
                            switch ((Action) action)
                            {
                                case Action.START:
                                    succeeded = args.IsRunning;
                                    break;
                                case Action.STOP:
                                    succeeded = !args.IsRunning;
                                    break;
                            }
                            ScriptRunningReplyEvent.Set();
                        };
                        Client.Inventory.ScriptRunningReply += ScriptRunningEventHandler;
                        Client.Inventory.RequestGetScriptRunning(primitive.ID, item.UUID);
                        if (!ScriptRunningReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_SCRIPT_STATE));
                        }
                        Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                        if (!succeeded)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_SET_SCRIPT_STATE));
                        }
                    };
                    break;
                case ScriptKeys.GETSCRIPTRUNNING:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        string entity =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message));
                        UUID entityUUID;
                        if (!UUID.TryParse(entity, out entityUUID))
                        {
                            if (string.IsNullOrEmpty(entity))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                            }
                            entityUUID = UUID.Zero;
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        List<InventoryBase> inventory =
                            Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                Configuration.SERVICES_TIMEOUT).ToList();
                        InventoryItem item = !entityUUID.Equals(UUID.Zero)
                            ? inventory.FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                            : inventory.FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        switch (item.AssetType)
                        {
                            case AssetType.LSLBytecode:
                            case AssetType.LSLText:
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.ITEM_IS_NOT_A_SCRIPT));
                        }
                        ManualResetEvent ScriptRunningReplyEvent = new ManualResetEvent(false);
                        bool running = false;
                        EventHandler<ScriptRunningReplyEventArgs> ScriptRunningEventHandler = (sender, args) =>
                        {
                            running = args.IsRunning;
                            ScriptRunningReplyEvent.Set();
                        };
                        Client.Inventory.ScriptRunningReply += ScriptRunningEventHandler;
                        Client.Inventory.RequestGetScriptRunning(primitive.ID, item.UUID);
                        if (!ScriptRunningReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_SCRIPT_STATE));
                        }
                        Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                        result.Add(GetEnumDescription(ResultKeys.RUNNING), running.ToString());
                    };
                    break;
                case ScriptKeys.GETPRIMITIVEINVENTORY:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.INVENTORY),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                    Configuration.SERVICES_TIMEOUT).Select(o => new[]
                                    {
                                        o.Name,
                                        o.UUID.ToString()
                                    }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.GETPRIMITIVEINVENTORYDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        string entity =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message));
                        UUID entityUUID;
                        if (!UUID.TryParse(entity, out entityUUID))
                        {
                            if (string.IsNullOrEmpty(entity))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                            }
                            entityUUID = UUID.Zero;
                        }
                        List<InventoryBase> inventory =
                            Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                Configuration.SERVICES_TIMEOUT).ToList();
                        InventoryItem item = !entityUUID.Equals(UUID.Zero)
                            ? inventory.FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                            : inventory.FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(item,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.UPDATEPRIMITIVEINVENTORY:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        string entity =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message));
                        UUID entityUUID;
                        if (!UUID.TryParse(entity, out entityUUID))
                        {
                            if (string.IsNullOrEmpty(entity))
                            {
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                            }
                            entityUUID = UUID.Zero;
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.ADD:
                                InventoryItem inventoryItem =
                                    SearchInventoryItem(Client.Inventory.Store.RootFolder,
                                        !entityUUID.Equals(UUID.Zero) ? entityUUID.ToString() : entity,
                                        Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                                if (inventoryItem == null)
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                                }
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID, inventoryItem);
                                break;
                            case Action.REMOVE:
                                if (entityUUID.Equals(UUID.Zero))
                                {
                                    entityUUID = Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                        Configuration.SERVICES_TIMEOUT).FirstOrDefault(o => o.Name.Equals(entity)).UUID;
                                    if (entityUUID.Equals(UUID.Zero))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                                    }
                                }
                                Client.Inventory.RemoveTaskInventory(primitive.LocalID, entityUUID,
                                    Client.Network.CurrentSim);
                                break;
                        }
                    };
                    break;
                case ScriptKeys.GETINVENTORYDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INVENTORY))
                        {
                            throw new Exception(
                                GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        InventoryItem item =
                            SearchInventoryItem(Client.Inventory.Store.RootFolder,
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                        if (item == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(item,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.GETPARTICLESYSTEM:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        StringBuilder particleSystem = new StringBuilder();
                        particleSystem.Append("PSYS_PART_FLAGS, 0");
                        if ((primitive.ParticleSys.PartDataFlags &
                             Primitive.ParticleSystem.ParticleDataFlags.InterpColor) != 0)
                            particleSystem.Append(" | PSYS_PART_INTERP_COLOR_MASK");
                        if ((primitive.ParticleSys.PartDataFlags &
                             Primitive.ParticleSystem.ParticleDataFlags.InterpScale) != 0)
                            particleSystem.Append(" | PSYS_PART_INTERP_SCALE_MASK");
                        if ((primitive.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Bounce) !=
                            0)
                            particleSystem.Append(" | PSYS_PART_BOUNCE_MASK");
                        if ((primitive.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Wind) != 0)
                            particleSystem.Append(" | PSYS_PART_WIND_MASK");
                        if ((primitive.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.FollowSrc) !=
                            0)
                            particleSystem.Append(" | PSYS_PART_FOLLOW_SRC_MASK");
                        if ((primitive.ParticleSys.PartDataFlags &
                             Primitive.ParticleSystem.ParticleDataFlags.FollowVelocity) != 0)
                            particleSystem.Append(" | PSYS_PART_FOLLOW_VELOCITY_MASK");
                        if ((primitive.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.TargetPos) !=
                            0)
                            particleSystem.Append(" | PSYS_PART_TARGET_POS_MASK");
                        if ((primitive.ParticleSys.PartDataFlags &
                             Primitive.ParticleSystem.ParticleDataFlags.TargetLinear) != 0)
                            particleSystem.Append(" | PSYS_PART_TARGET_LINEAR_MASK");
                        if ((primitive.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Emissive) !=
                            0)
                            particleSystem.Append(" | PSYS_PART_EMISSIVE_MASK");
                        particleSystem.Append(LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_PATTERN, 0");
                        if (((long) primitive.ParticleSys.Pattern & (long) Primitive.ParticleSystem.SourcePattern.Drop) !=
                            0)
                            particleSystem.Append(" | PSYS_SRC_PATTERN_DROP");
                        if (((long) primitive.ParticleSys.Pattern &
                             (long) Primitive.ParticleSystem.SourcePattern.Explode) != 0)
                            particleSystem.Append(" | PSYS_SRC_PATTERN_EXPLODE");
                        if (((long) primitive.ParticleSys.Pattern & (long) Primitive.ParticleSystem.SourcePattern.Angle) !=
                            0)
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE");
                        if (((long) primitive.ParticleSys.Pattern &
                             (long) Primitive.ParticleSystem.SourcePattern.AngleCone) != 0)
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE");
                        if (((long) primitive.ParticleSys.Pattern &
                             (long) Primitive.ParticleSystem.SourcePattern.AngleConeEmpty) != 0)
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY");
                        particleSystem.Append(LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_PART_START_ALPHA, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartColor.A) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_PART_END_ALPHA, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndColor.A) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_PART_START_COLOR, " +
                                              primitive.ParticleSys.PartStartColor.ToRGBString() +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_PART_END_COLOR, " + primitive.ParticleSys.PartEndColor.ToRGBString() +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_PART_START_SCALE, <" +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartScaleX) + ", " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartScaleY) +
                                              ", 0>, ");
                        particleSystem.Append("PSYS_PART_END_SCALE, <" +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndScaleX) + ", " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndScaleY) +
                                              ", 0>, ");
                        particleSystem.Append("PSYS_PART_MAX_AGE, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartMaxAge) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_MAX_AGE, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.MaxAge) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_ACCEL, " + primitive.ParticleSys.PartAcceleration +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_BURST_PART_COUNT, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0}",
                                                  primitive.ParticleSys.BurstPartCount) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_BURST_RADIUS, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstRadius) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_BURST_RATE, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstRate) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_BURST_SPEED_MIN, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstSpeedMin) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_BURST_SPEED_MAX, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstSpeedMax) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_INNERANGLE, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.InnerAngle) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_OUTERANGLE, " +
                                              string.Format(CultureInfo.InvariantCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.OuterAngle) +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_OMEGA, " + primitive.ParticleSys.AngularVelocity +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_TEXTURE, (key)\"" + primitive.ParticleSys.Texture + "\"" +
                                              LINDEN_CONSTANTS.LSL.CSV_DELIMITER);
                        particleSystem.Append("PSYS_SRC_TARGET_KEY, (key)\"" + primitive.ParticleSys.Target + "\"");
                        result.Add(GetEnumDescription(ResultKeys.PARTICLESYSTEM), particleSystem.ToString());
                    };
                    break;
                case ScriptKeys.ACTIVATE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_IN_GROUP));
                        }
                        Client.Groups.ActivateGroup(groupUUID);
                    };
                    break;
                case ScriptKeys.SETTITLE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_IN_GROUP));
                        }
                        ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                        Dictionary<string, UUID> roleData = new Dictionary<string, UUID>();
                        EventHandler<GroupRolesDataReplyEventArgs> Groups_GroupRoleDataReply = (sender, args) =>
                        {
                            roleData = args.Roles.ToDictionary(o => o.Value.Title, o => o.Value.ID);
                            GroupRoleDataReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleDataReply += Groups_GroupRoleDataReply;
                        Client.Groups.RequestGroupRoles(groupUUID);
                        if (!GroupRoleDataReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_GROUP_ROLES));
                        }
                        Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                        UUID roleUUID =
                            roleData.FirstOrDefault(
                                o =>
                                    o.Key.Equals(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TITLE),
                                            message)),
                                        StringComparison.Ordinal))
                                .Value;
                        if (roleUUID.Equals(UUID.Zero))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_TITLE));
                        }
                        Client.Groups.ActivateTitle(groupUUID, roleUUID);
                    };
                    break;
                case ScriptKeys.MOVE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_POSITION));
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.START:
                                uint moveRegionX, moveRegionY;
                                Utils.LongToUInts(Client.Network.CurrentSim.Handle, out moveRegionX, out moveRegionY);
                                if (Client.Self.Movement.SitOnGround || Client.Self.SittingOn != 0)
                                {
                                    Client.Self.Stand();
                                }
                                Client.Self.SignaledAnimations.ForEach(
                                    animation => Client.Self.AnimationStop(animation.Key, true));
                                Client.Self.AutoPilotCancel();
                                Client.Self.Movement.TurnToward(position, true);
                                Client.Self.AutoPilot(position.X + moveRegionX, position.Y + moveRegionY, position.Z);
                                break;
                            case Action.STOP:
                                Client.Self.AutoPilotCancel();
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_MOVE_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.STARTPROPOSAL:
                    execute = () =>
                    {
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        if (!AgentInGroup(Client.Self.AgentID, groupUUID, Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NOT_IN_GROUP));
                        }
                        if (
                            !HasGroupPowers(Client.Self.AgentID, groupUUID, GroupPowers.StartProposal,
                                Configuration.SERVICES_TIMEOUT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                        }
                        int duration;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DURATION), message)),
                                out duration))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PROPOSAL_DURATION));
                        }
                        float majority;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MAJORITY), message)),
                                out majority))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PROPOSAL_MAJORITY));
                        }
                        int quorum;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.QUORUM), message)),
                                out quorum))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PROPOSAL_QUORUM));
                        }
                        string text =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TEXT), message));
                        if (string.IsNullOrEmpty(text))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PROPOSAL_TEXT));
                        }
                        Client.Groups.StartProposal(groupUUID, new GroupProposal
                        {
                            Duration = duration,
                            Majority = majority,
                            Quorum = quorum,
                            VoteText = text
                        });
                    };
                    break;
                case ScriptKeys.MUTE:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group, (int) Permissions.PERMISSION_MUTE))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID targetUUID;
                        if (
                            !UUID.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TARGET), message)),
                                out targetUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_MUTE_TARGET));
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.MUTE:
                                FieldInfo muteTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                                     BindingFlags.Static)
                                    .FirstOrDefault(
                                        o =>
                                            o.Name.Equals(
                                                wasUriUnescapeDataString(
                                                    wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message)),
                                                StringComparison.Ordinal));
                                ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
                                EventHandler<EventArgs> MuteListUpdatedEventHandler =
                                    (sender, args) => MuteListUpdatedEvent.Set();
                                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                Client.Self.UpdateMuteListEntry(muteTypeInfo != null
                                    ? (MuteType)
                                        muteTypeInfo
                                            .GetValue(null)
                                    : MuteType.ByName, targetUUID,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message)));
                                if (!MuteListUpdatedEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                {
                                    Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_UPDATING_MUTE_LIST));
                                }
                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                break;
                            case Action.UNMUTE:
                                Client.Self.RemoveMuteListEntry(targetUUID,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message)));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.GETMUTES:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_MUTE))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.MUTES),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                Client.Self.MuteList.Copy().Select(o => new[]
                                {
                                    o.Value.Name,
                                    o.Value.ID.ToString()
                                }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.DATABASE:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_DATABASE))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string databaseFile =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).DatabaseFile;
                        if (string.IsNullOrEmpty(databaseFile))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_DATABASE_FILE_CONFIGURED));
                        }
                        if (!File.Exists(databaseFile))
                        {
                            // create the file and close it
                            File.Create(databaseFile).Close();
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.GET:
                                string databaseGetkey =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.KEY), message));
                                if (string.IsNullOrEmpty(databaseGetkey))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_DATABASE_KEY_SPECIFIED));
                                }
                                lock (DatabaseLock)
                                {
                                    if (!DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Add(group, new object());
                                    }
                                }
                                lock (DatabaseLocks[group])
                                {
                                    string databaseGetValue = wasKeyValueGet(databaseGetkey,
                                        File.ReadAllText(databaseFile));
                                    if (!string.IsNullOrEmpty(databaseGetValue))
                                    {
                                        result.Add(databaseGetkey,
                                            wasUriUnescapeDataString(databaseGetValue));
                                    }
                                }
                                lock (DatabaseLock)
                                {
                                    if (DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Remove(group);
                                    }
                                }
                                break;
                            case Action.SET:
                                string databaseSetKey =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.KEY), message));
                                if (string.IsNullOrEmpty(databaseSetKey))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_DATABASE_KEY_SPECIFIED));
                                }
                                string databaseSetValue =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.VALUE),
                                        message));
                                if (string.IsNullOrEmpty(databaseSetValue))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_DATABASE_VALUE_SPECIFIED));
                                }
                                lock (DatabaseLock)
                                {
                                    if (!DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Add(group, new object());
                                    }
                                }
                                lock (DatabaseLocks[group])
                                {
                                    string contents = File.ReadAllText(databaseFile);
                                    using (StreamWriter recreateDatabase = new StreamWriter(databaseFile, false))
                                    {
                                        recreateDatabase.Write(wasKeyValueSet(databaseSetKey,
                                            databaseSetValue, contents));
                                        recreateDatabase.Flush();
                                        //recreateDatabase.Close();
                                    }
                                }
                                lock (DatabaseLock)
                                {
                                    if (DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Remove(group);
                                    }
                                }
                                break;
                            case Action.DELETE:
                                string databaseDeleteKey =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.KEY), message));
                                if (string.IsNullOrEmpty(databaseDeleteKey))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_DATABASE_KEY_SPECIFIED));
                                }
                                lock (DatabaseLock)
                                {
                                    if (!DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Add(group, new object());
                                    }
                                }
                                lock (DatabaseLocks[group])
                                {
                                    string contents = File.ReadAllText(databaseFile);
                                    using (StreamWriter recreateDatabase = new StreamWriter(databaseFile, false))
                                    {
                                        recreateDatabase.Write(wasKeyValueDelete(databaseDeleteKey, contents));
                                        recreateDatabase.Flush();
                                        //recreateDatabase.Close();
                                    }
                                }
                                lock (DatabaseLock)
                                {
                                    if (DatabaseLocks.ContainsKey(group))
                                    {
                                        DatabaseLocks.Remove(group);
                                    }
                                }
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_DATABASE_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.NOTIFY:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_NOTIFICATIONS))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.SET:
                                string url =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.URL), message));
                                if (string.IsNullOrEmpty(url))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.INVALID_URL_PROVIDED));
                                }
                                Uri notifyURL;
                                if (!Uri.TryCreate(url, UriKind.Absolute, out notifyURL))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.INVALID_URL_PROVIDED));
                                }
                                string notificationTypes =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                        .ToLower(CultureInfo.InvariantCulture);
                                if (string.IsNullOrEmpty(notificationTypes))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.INVALID_NOTIFICATION_TYPES));
                                }
                                int notifications = 0;
                                Parallel.ForEach(
                                    notificationTypes.Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER},
                                        StringSplitOptions.RemoveEmptyEntries),
                                    o =>
                                    {
                                        int notificationValue =
                                            (int)
                                                wasGetEnumValueFromDescription<Notifications>(o);
                                        if (!HasCorradeNotification(group, notificationValue))
                                        {
                                            throw new Exception(GetEnumDescription(ScriptError.NOTIFICATION_NOT_ALLOWED));
                                        }
                                        notifications |= notificationValue;
                                    });
                                // Build the notification.
                                Notification notification = new Notification
                                {
                                    GROUP = group,
                                    URL = url,
                                    NOTIFICATION_MASK = notifications
                                };
                                lock (GroupNotificationsLock)
                                {
                                    // If we already have the same notification, bail
                                    if (GroupNotifications.Contains(notification)) break;
                                    // Otherwise, replace it.
                                    GroupNotifications.RemoveWhere(
                                        o => o.GROUP.Equals(group, StringComparison.Ordinal));
                                    GroupNotifications.Add(notification);
                                }
                                break;
                            case Action.GET:
                                // If the group has no insalled notifications, bail
                                if (!GroupNotifications.Any(o => o.GROUP.Equals(group)))
                                {
                                    break;
                                }
                                result.Add(GetEnumDescription(ResultKeys.NOTIFICATIONS),
                                    string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                        wasGetEnumDescriptions<Notifications>().Where(o => !GroupNotifications.Any(
                                            p =>
                                                p.GROUP.Equals(group, StringComparison.Ordinal) &&
                                                (p.NOTIFICATION_MASK &
                                                 wasGetEnumValueFromDescription<Notifications>(o)) == 0))
                                            .ToArray()));
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_NOTIFICATIONS_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.REPLYTOLURE:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_MOVEMENT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        UUID sessionUUID;
                        if (
                            !UUID.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.SESSION), message)),
                                out sessionUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ITEM_SPECIFIED));
                        }
                        Client.Self.TeleportLureRespond(agentUUID, sessionUUID, wasGetEnumValueFromDescription<Action>(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                .ToLower(CultureInfo.InvariantCulture)).Equals(Action.ACCEPT));
                    };
                    break;
                case ScriptKeys.REPLYTOPERMISSION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID itemUUID;
                        if (
                            !UUID.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                out itemUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ITEM_SPECIFIED));
                        }
                        UUID taskUUID;
                        if (
                            !UUID.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TASK), message)),
                                out taskUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_TASK_SPECIFIED));
                        }
                        int permissionMask = 0;
                        Parallel.ForEach(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PERMISSIONS), message))
                                .Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER}, StringSplitOptions.RemoveEmptyEntries),
                            o =>
                                Parallel.ForEach(
                                    typeof (ScriptPermission).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    q => { permissionMask |= ((int) q.GetValue(null)); }));
                        Client.Self.ScriptQuestionReply(Client.Network.CurrentSim, itemUUID, taskUUID,
                            (ScriptPermission) permissionMask);
                    };
                    break;
                case ScriptKeys.REPLYTODIALOG:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        int channel;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.CHANNEL), message)),
                                out channel))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CHANNEL_SPECIFIED));
                        }
                        int index;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.INDEX), message)),
                                out index))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_BUTTON_INDEX_SPECIFIED));
                        }
                        string label =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.BUTTON), message));
                        if (string.IsNullOrEmpty(label))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_BUTTON_SPECIFIED));
                        }
                        UUID itemUUID;
                        if (
                            !UUID.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                out itemUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ITEM_SPECIFIED));
                        }
                        Client.Self.ReplyToScriptDialog(channel, index, label, itemUUID);
                    };
                    break;
                case ScriptKeys.ANIMATION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string item =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message));
                        if (string.IsNullOrEmpty(item))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_ITEM_SPECIFIED));
                        }
                        UUID itemUUID;
                        if (!UUID.TryParse(item, out itemUUID))
                        {
                            InventoryItem inventoryItem =
                                SearchInventoryItem(Client.Inventory.Store.RootFolder, item,
                                    Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                            if (inventoryItem == null)
                            {
                                throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                            }
                            itemUUID = inventoryItem.UUID;
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.START:
                                Client.Self.AnimationStart(itemUUID, true);
                                break;
                            case Action.STOP:
                                Client.Self.AnimationStop(itemUUID, true);
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ANIMATION_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.GETANIMATIONS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        List<string> csv = new List<string>();
                        Client.Self.SignaledAnimations.ForEach(
                            o =>
                                csv.AddRange(new List<string>
                                {
                                    o.Key.ToString(),
                                    o.Value.ToString(CultureInfo.InvariantCulture)
                                }));
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.ANIMATIONS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.RESTARTREGION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        int delay;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DELAY), message))
                                    .ToLower(CultureInfo.InvariantCulture), out delay))
                        {
                            delay = LINDEN_CONSTANTS.ESTATE.REGION_RESTART_DELAY;
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.RESTART:
                                // Manually override Client.Estate.RestartRegion();
                                Client.Estate.EstateOwnerMessage(
                                    LINDEN_CONSTANTS.ESTATE.MESSAGES.REGION_RESTART_MESSAGE,
                                    delay.ToString(CultureInfo.InvariantCulture));
                                break;
                            case Action.CANCEL:
                                Client.Estate.CancelRestart();
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_RESTART_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.SETREGIONDEBUG:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        bool scripts;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.SCRIPTS), message))
                                    .ToLower(CultureInfo.InvariantCulture), out scripts))
                        {
                            scripts = false;
                        }
                        bool collisions;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.COLLISIONS),
                                    message))
                                    .ToLower(CultureInfo.InvariantCulture), out collisions))
                        {
                            collisions = false;
                        }
                        bool physics;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PHYSICS), message))
                                    .ToLower(CultureInfo.InvariantCulture), out physics))
                        {
                            physics = false;
                        }
                        Client.Estate.SetRegionDebug(!scripts, !collisions, !physics);
                    };
                    break;
                case ScriptKeys.GETREGIONTOP:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        int amount;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.AMOUNT), message)),
                                out amount))
                        {
                            amount = 5;
                        }
                        Dictionary<UUID, EstateTask> topTasks = new Dictionary<UUID, EstateTask>();
                        switch (
                            (Type)
                                wasGetEnumValueFromDescription<Type>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                        .ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Type.SCRIPTS:
                                ManualResetEvent TopScriptsReplyEvent = new ManualResetEvent(false);
                                EventHandler<TopScriptsReplyEventArgs> TopScriptsReplyEventHandler = (sender, args) =>
                                {
                                    topTasks =
                                        args.Tasks.OrderByDescending(o => o.Value.Score)
                                            .ToDictionary(o => o.Key, o => o.Value);
                                    TopScriptsReplyEvent.Set();
                                };
                                Client.Estate.TopScriptsReply += TopScriptsReplyEventHandler;
                                Client.Estate.RequestTopScripts();
                                if (!TopScriptsReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                {
                                    Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                                    throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_TOP_SCRIPTS));
                                }
                                Client.Estate.TopScriptsReply -= TopScriptsReplyEventHandler;
                                break;
                            case Type.COLLIDERS:
                                ManualResetEvent TopCollidersReplyEvent = new ManualResetEvent(false);
                                EventHandler<TopCollidersReplyEventArgs> TopCollidersReplyEventHandler =
                                    (sender, args) =>
                                    {
                                        topTasks =
                                            args.Tasks.OrderByDescending(o => o.Value.Score)
                                                .ToDictionary(o => o.Key, o => o.Value);
                                        TopCollidersReplyEvent.Set();
                                    };
                                Client.Estate.TopCollidersReply += TopCollidersReplyEventHandler;
                                Client.Estate.RequestTopScripts();
                                if (!TopCollidersReplyEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                {
                                    Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                                    throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_TOP_SCRIPTS));
                                }
                                Client.Estate.TopCollidersReply -= TopCollidersReplyEventHandler;
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_TOP_TYPE));
                        }
                        result.Add(GetEnumDescription(ResultKeys.TOP),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, topTasks.Take(amount).Select(o => new[]
                            {
                                o.Value.TaskName,
                                o.Key.ToString(),
                                o.Value.Score.ToString(CultureInfo.InvariantCulture),
                                o.Value.OwnerName,
                                o.Value.Position.ToString()
                            }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.SETESTATELIST:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        bool allEstates;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ALL), message)),
                                out allEstates))
                        {
                            allEstates = false;
                        }
                        UUID targetUUID = UUID.Zero;
                        switch (
                            (Type)
                                wasGetEnumValueFromDescription<Type>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                        .ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Type.BAN:
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                switch (
                                    (Action)
                                        wasGetEnumValueFromDescription<Action>(
                                            wasUriUnescapeDataString(
                                                wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                                .ToLower(CultureInfo.InvariantCulture)))
                                {
                                    case Action.ADD:
                                        Client.Estate.BanUser(targetUUID, allEstates);
                                        break;
                                    case Action.REMOVE:
                                        Client.Estate.UnbanUser(targetUUID, allEstates);
                                        break;
                                    default:
                                        throw new Exception(GetEnumDescription(ScriptError.UNKNWON_ESTATE_LIST_ACTION));
                                }
                                break;
                            case Type.GROUP:
                                if (
                                    !UUID.TryParse(
                                        wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TARGET),
                                            message)),
                                        out targetUUID))
                                {
                                    if (
                                        !GroupNameToUUID(
                                            wasUriUnescapeDataString(
                                                wasKeyValueGet(GetEnumDescription(ScriptKeys.TARGET), message)),
                                            Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                    {
                                        throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                                    }
                                }
                                switch (
                                    (Action)
                                        wasGetEnumValueFromDescription<Action>(
                                            wasUriUnescapeDataString(
                                                wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                                .ToLower(CultureInfo.InvariantCulture)))
                                {
                                    case Action.ADD:
                                        Client.Estate.AddAllowedGroup(targetUUID, allEstates);
                                        break;
                                    case Action.REMOVE:
                                        Client.Estate.RemoveAllowedGroup(targetUUID, allEstates);
                                        break;
                                    default:
                                        throw new Exception(GetEnumDescription(ScriptError.UNKNWON_ESTATE_LIST_ACTION));
                                }
                                break;
                            case Type.USER:
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                switch (
                                    (Action)
                                        wasGetEnumValueFromDescription<Action>(
                                            wasUriUnescapeDataString(
                                                wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                                .ToLower(CultureInfo.InvariantCulture)))
                                {
                                    case Action.ADD:
                                        Client.Estate.AddAllowedUser(targetUUID, allEstates);
                                        break;
                                    case Action.REMOVE:
                                        Client.Estate.RemoveAllowedUser(targetUUID, allEstates);
                                        break;
                                    default:
                                        throw new Exception(GetEnumDescription(ScriptError.UNKNWON_ESTATE_LIST_ACTION));
                                }
                                break;
                            case Type.MANAGER:
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref targetUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                switch (
                                    (Action)
                                        wasGetEnumValueFromDescription<Action>(
                                            wasUriUnescapeDataString(
                                                wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION), message))
                                                .ToLower(CultureInfo.InvariantCulture)))
                                {
                                    case Action.ADD:
                                        Client.Estate.AddEstateManager(targetUUID, allEstates);
                                        break;
                                    case Action.REMOVE:
                                        Client.Estate.RemoveEstateManager(targetUUID, allEstates);
                                        break;
                                    default:
                                        throw new Exception(GetEnumDescription(ScriptError.UNKNWON_ESTATE_LIST_ACTION));
                                }
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ESTATE_LIST));
                        }
                    };
                    break;
                case ScriptKeys.GETESTATELIST:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_LAND))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        if (!Client.Network.CurrentSim.IsEstateManager)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_LAND_RIGHTS));
                        }
                        int timeout;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TIMEOUT), message)),
                                out timeout))
                        {
                            timeout = Configuration.SERVICES_TIMEOUT;
                        }
                        List<UUID> estateList = new List<UUID>();
                        ManualResetEvent EstateListReplyEvent = new ManualResetEvent(false);
                        object LockObject = new object();
                        switch (
                            (Type)
                                wasGetEnumValueFromDescription<Type>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                        .ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Type.BAN:
                                EventHandler<EstateBansReplyEventArgs> EstateBansReplyEventHandler = (sender, args) =>
                                {
                                    if (args.Count == 0)
                                    {
                                        EstateListReplyEvent.Set();
                                        return;
                                    }
                                    lock (LockObject)
                                    {
                                        estateList.AddRange(args.Banned);
                                    }
                                };
                                Client.Estate.EstateBansReply += EstateBansReplyEventHandler;
                                Client.Estate.RequestInfo();
                                EstateListReplyEvent.WaitOne(timeout, false);
                                Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                break;
                            case Type.GROUP:
                                EventHandler<EstateGroupsReplyEventArgs> EstateGroupsReplyEvenHandler =
                                    (sender, args) =>
                                    {
                                        if (args.Count == 0)
                                        {
                                            EstateListReplyEvent.Set();
                                            return;
                                        }
                                        lock (LockObject)
                                        {
                                            estateList.AddRange(args.AllowedGroups);
                                        }
                                    };
                                Client.Estate.EstateGroupsReply += EstateGroupsReplyEvenHandler;
                                Client.Estate.RequestInfo();
                                EstateListReplyEvent.WaitOne(timeout, false);
                                Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                break;
                            case Type.MANAGER:
                                EventHandler<EstateManagersReplyEventArgs> EstateManagersReplyEventHandler =
                                    (sender, args) =>
                                    {
                                        if (args.Count == 0)
                                        {
                                            EstateListReplyEvent.Set();
                                            return;
                                        }
                                        lock (LockObject)
                                        {
                                            estateList.AddRange(args.Managers);
                                        }
                                    };
                                Client.Estate.EstateManagersReply += EstateManagersReplyEventHandler;
                                Client.Estate.RequestInfo();
                                EstateListReplyEvent.WaitOne(timeout, false);
                                Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                break;
                            case Type.USER:
                                EventHandler<EstateUsersReplyEventArgs> EstateUsersReplyEventHandler =
                                    (sender, args) =>
                                    {
                                        if (args.Count == 0)
                                        {
                                            EstateListReplyEvent.Set();
                                            return;
                                        }
                                        lock (LockObject)
                                        {
                                            estateList.AddRange(args.AllowedUsers);
                                        }
                                    };
                                Client.Estate.EstateUsersReply += EstateUsersReplyEventHandler;
                                Client.Estate.RequestInfo();
                                EstateListReplyEvent.WaitOne(timeout, false);
                                Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ESTATE_LIST));
                        }
                        if (estateList.Count == 0) return;
                        lock (LockObject)
                        {
                            result.Add(GetEnumDescription(ResultKeys.LIST),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                    estateList.ConvertAll(o => o.ToString()).ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETAVATARDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        Avatar avatar = Client.Network.CurrentSim.ObjectsAvatars.Find(o => o.ID.Equals(agentUUID));
                        if (avatar == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AVATAR_NOT_IN_RANGE));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(avatar,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.GETPRIMITIVES:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        uint entity =
                            wasGetEnumValueFromDescription<Entity>(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message))
                                    .ToLower(CultureInfo.InvariantCulture));
                        Parcel parcel = null;
                        UUID agentUUID = UUID.Zero;
                        switch ((Entity) entity)
                        {
                            case Entity.REGION:
                                break;
                            case Entity.AVATAR:
                                if (
                                    !AgentNameToUUID(
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.FIRSTNAME), message)),
                                        wasUriUnescapeDataString(wasKeyValueGet(
                                            GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                        Configuration.SERVICES_TIMEOUT, ref agentUUID))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                                }
                                break;
                            case Entity.PARCEL:
                                if (
                                    !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                                }
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                        List<string> csv = new List<string>();
                        object LockObject = new object();
                        HashSet<Primitive> primitives =
                            new HashSet<Primitive>(
                                Client.Network.CurrentSim.ObjectsPrimitives.FindAll(o => !o.ID.Equals(UUID.Zero)));
                        foreach (Primitive p in primitives)
                        {
                            ManualResetEvent ObjectPropertiesEvent = new ManualResetEvent(false);
                            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler =
                                (sender, args) => ObjectPropertiesEvent.Set();
                            Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                            Client.Objects.SelectObjects(Client.Network.CurrentSim, new[] {p.LocalID}, true);
                            if (
                                !ObjectPropertiesEvent.WaitOne(
                                    Configuration.SERVICES_TIMEOUT, false))
                            {
                                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                                throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                            }
                            Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                            if (p.Properties == null) continue;
                            switch ((Entity) entity)
                            {
                                case Entity.REGION:
                                    break;
                                case Entity.AVATAR:
                                    Primitive parent = p;
                                    do
                                    {
                                        Primitive closure = parent;
                                        Primitive ancestor =
                                            Client.Network.CurrentSim.ObjectsPrimitives.Find(
                                                o => o.LocalID.Equals(closure.ParentID));
                                        if (ancestor == null) break;
                                        parent = ancestor;
                                    } while (!parent.ParentID.Equals(0));
                                    // check if an avatar is the parent of the parent primitive
                                    Avatar parentAvatar =
                                        Client.Network.CurrentSim.ObjectsAvatars.Find(
                                            o => o.LocalID.Equals(parent.ParentID));
                                    // parent avatar not found, this should not happen
                                    if (parentAvatar == null || !parentAvatar.ID.Equals(agentUUID)) continue;
                                    break;
                                case Entity.PARCEL:
                                    if (parcel == null) continue;
                                    Parcel primitiveParcel = null;
                                    if (!GetParcelAtPosition(Client.Network.CurrentSim, p.Position, ref primitiveParcel))
                                        continue;
                                    if (!primitiveParcel.LocalID.Equals(parcel.LocalID)) continue;
                                    break;
                            }
                            lock (LockObject)
                            {
                                csv.Add(p.Properties.Name);
                                csv.Add(p.ID.ToString());
                            }
                        }
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.PRIMITIVES),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETAVATARPOSITIONS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        uint entity =
                            wasGetEnumValueFromDescription<Entity>(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ENTITY), message))
                                    .ToLower(CultureInfo.InvariantCulture));
                        Parcel parcel = null;
                        switch ((Entity) entity)
                        {
                            case Entity.REGION:
                                break;
                            case Entity.PARCEL:
                                if (
                                    !GetParcelAtPosition(Client.Network.CurrentSim, position, ref parcel))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_FIND_PARCEL));
                                }
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ENTITY));
                        }
                        List<string> csv = new List<string>();
                        Dictionary<UUID, Vector3> avatarPositions = new Dictionary<UUID, Vector3>();
                        Client.Network.CurrentSim.AvatarPositions.ForEach(o => avatarPositions.Add(o.Key, o.Value));
                        foreach (KeyValuePair<UUID, Vector3> p in avatarPositions)
                        {
                            string name = string.Empty;
                            if (!AgentUUIDToName(p.Key, Configuration.SERVICES_TIMEOUT, ref name))
                                continue;
                            switch ((Entity) entity)
                            {
                                case Entity.REGION:
                                    break;
                                case Entity.PARCEL:
                                    if (parcel == null) return;
                                    Parcel avatarParcel = null;
                                    if (!GetParcelAtPosition(Client.Network.CurrentSim, p.Value, ref avatarParcel))
                                        continue;
                                    if (!avatarParcel.LocalID.Equals(parcel.LocalID)) continue;
                                    break;
                            }
                            csv.Add(name);
                            csv.Add(p.Key.ToString());
                            csv.Add(p.Value.ToString());
                        }
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.POSITIONS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETMAPAVATARPOSITIONS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string region =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REGION), message));
                        if (string.IsNullOrEmpty(region))
                        {
                            region = Client.Network.CurrentSim.Name;
                        }
                        ManualResetEvent GridRegionEvent = new ManualResetEvent(false);
                        ulong regionHandle = 0;
                        EventHandler<GridRegionEventArgs> GridRegionEventHandler = (sender, args) =>
                        {
                            regionHandle = args.Region.RegionHandle;
                            GridRegionEvent.Set();
                        };
                        Client.Grid.GridRegion += GridRegionEventHandler;
                        Client.Grid.RequestMapRegion(region, GridLayerType.Objects);
                        if (!GridRegionEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Grid.GridRegion -= GridRegionEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_REGION));
                        }
                        Client.Grid.GridRegion -= GridRegionEventHandler;
                        if (regionHandle.Equals(0))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.REGION_NOT_FOUND));
                        }
                        HashSet<MapItem> mapItems =
                            new HashSet<MapItem>(Client.Grid.MapItems(regionHandle, GridItemType.AgentLocations,
                                GridLayerType.Objects, Configuration.SERVICES_TIMEOUT));
                        if (mapItems.Count.Equals(0))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_MAP_ITEMS_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.AVATARS),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                mapItems.Where(o => (o as MapAgentLocation) != null).Select(o => new[]
                                {
                                    ((MapAgentLocation) o).AvatarCount.ToString(CultureInfo.InvariantCulture),
                                    new Vector3(o.LocalX, o.LocalY, 0).ToString()
                                }).SelectMany(o => o).ToArray()));
                    };
                    break;
                case ScriptKeys.GETSELFDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(Client.Self,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.DISPLAYNAME:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string previous = string.Empty;
                        Client.Avatars.GetDisplayNames(new List<UUID> {Client.Self.AgentID},
                            (succeded, names, IDs) =>
                            {
                                if (!succeded || names.Length < 1)
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.FAILED_TO_GET_DISPLAY_NAME));
                                }
                                previous = names[0].DisplayName;
                            });
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.GET:
                                result.Add(GetEnumDescription(ResultKeys.DISPLAYNAME), previous);
                                break;
                            case Action.SET:
                                string name =
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message))
                                        .ToLower(CultureInfo.InvariantCulture);
                                if (string.IsNullOrEmpty(name))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_NAME_PROVIDED));
                                }
                                bool succeeded = true;
                                ManualResetEvent SetDisplayNameEvent = new ManualResetEvent(false);
                                EventHandler<SetDisplayNameReplyEventArgs> SetDisplayNameEventHandler =
                                    (sender, args) =>
                                    {
                                        if (!args.Status.Equals(LINDEN_CONSTANTS.AVATARS.SET_DISPLAY_NAME_SUCCESS))
                                        {
                                            succeeded = false;
                                        }
                                        SetDisplayNameEvent.Set();
                                    };
                                Client.Self.SetDisplayNameReply += SetDisplayNameEventHandler;
                                Client.Self.SetDisplayName(previous, name);
                                if (!SetDisplayNameEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                                {
                                    Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                                    throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_WAITING_FOR_ESTATE_LIST));
                                }
                                Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                                if (!succeeded)
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.COULD_NOT_SET_DISPLAY_NAME));
                                }
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.GETFRIENDSLIST:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        List<string> csv = new List<string>();
                        Client.Friends.FriendList.ForEach(o =>
                        {
                            csv.Add(o.Name);
                            csv.Add(o.UUID.ToString());
                        });
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.FRIENDS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.GETFRIENDSHIPREQUESTS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        List<string> csv = new List<string>();
                        Client.Friends.FriendRequests.ForEach(o =>
                        {
                            string name = string.Empty;
                            if (!AgentUUIDToName(o.Key, Configuration.SERVICES_TIMEOUT, ref name))
                            {
                                return;
                            }
                            csv.Add(name);
                            csv.Add(o.Key.ToString());
                        });
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.REQUESTS),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                case ScriptKeys.FRIENDSHIPREQUEST:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        UUID session = UUID.Zero;
                        Client.Friends.FriendRequests.ForEach(o =>
                        {
                            if (o.Key.Equals(agentUUID))
                            {
                                session = o.Value;
                            }
                        });
                        if (session.Equals(UUID.Zero))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_FRIENDSHIP_OFFER_FOUND));
                        }
                        switch (
                            (Action)
                                wasGetEnumValueFromDescription<Action>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ACTION),
                                        message)).ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Action.ACCEPT:
                                Client.Friends.AcceptFriendship(agentUUID, session);
                                break;
                            case Action.DECLINE:
                                Client.Friends.DeclineFriendship(agentUUID, session);
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_ACTION));
                        }
                    };
                    break;
                case ScriptKeys.GETFRIENDDATA:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                        if (friend == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_NOT_FOUND));
                        }
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER,
                                GetStructuredData(friend,
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)))
                                    .ToArray()));
                    };
                    break;
                case ScriptKeys.OFFERFRIENDSHIP:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                        if (friend != null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_ALREADY_FRIEND));
                        }
                        Client.Friends.OfferFriendship(agentUUID,
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.MESSAGE), message)));
                    };
                    break;
                case ScriptKeys.TERMINATEFRIENDSHIP:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                        if (friend == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_NOT_FOUND));
                        }
                        Client.Friends.TerminateFriendship(agentUUID);
                    };
                    break;
                case ScriptKeys.GRANTFRIENDRIGHTS:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                        if (friend == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_NOT_FOUND));
                        }
                        ulong rights = 0;
                        Parallel.ForEach(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RIGHTS), message))
                                .Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER}, StringSplitOptions.RemoveEmptyEntries),
                            o =>
                                Parallel.ForEach(
                                    typeof (FriendRights).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    q => { rights |= ((ulong) q.GetValue(null)); }));
                        Client.Friends.GrantRights(agentUUID, (FriendRights) rights);
                    };
                    break;
                case ScriptKeys.MAPFRIEND:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_FRIENDSHIP))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID agentUUID = UUID.Zero;
                        if (
                            !AgentNameToUUID(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FIRSTNAME),
                                    message)),
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.LASTNAME), message)),
                                Configuration.SERVICES_TIMEOUT, ref agentUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.AGENT_NOT_FOUND));
                        }
                        FriendInfo friend = Client.Friends.FriendList.Find(o => o.UUID.Equals(agentUUID));
                        if (friend == null)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_NOT_FOUND));
                        }
                        if (!friend.CanSeeThemOnMap)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_DOES_NOT_ALLOW_MAPPING));
                        }
                        ulong regionHandle = 0;
                        Vector3 position = Vector3.Zero;
                        ManualResetEvent FriendFoundEvent = new ManualResetEvent(false);
                        bool offline = false;
                        EventHandler<FriendFoundReplyEventArgs> FriendFoundEventHandler = (sender, args) =>
                        {
                            if (args.RegionHandle.Equals(0))
                            {
                                offline = true;
                                FriendFoundEvent.Set();
                                return;
                            }
                            regionHandle = args.RegionHandle;
                            position = args.Location;
                            FriendFoundEvent.Set();
                        };
                        Client.Friends.FriendFoundReply += FriendFoundEventHandler;
                        Client.Friends.MapFriend(agentUUID);
                        if (!FriendFoundEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Friends.FriendFoundReply -= FriendFoundEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_MAPPING_FRIEND));
                        }
                        Client.Friends.FriendFoundReply -= FriendFoundEventHandler;
                        if (offline)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.FRIEND_OFFLINE));
                        }
                        UUID parcelUUID = Client.Parcels.RequestRemoteParcelID(position, regionHandle, UUID.Zero);
                        ManualResetEvent ParcelInfoEvent = new ManualResetEvent(false);
                        string regionName = string.Empty;
                        EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                        {
                            regionName = args.Parcel.SimName;
                            ParcelInfoEvent.Set();
                        };
                        Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                        Client.Parcels.RequestParcelInfo(parcelUUID);
                        if (!ParcelInfoEvent.WaitOne(Configuration.SERVICES_TIMEOUT, false))
                        {
                            Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                            throw new Exception(GetEnumDescription(ScriptError.TIMEOUT_GETTING_PARCELS));
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                        result.Add(GetEnumDescription(ResultKeys.DATA),
                            string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, new[] {regionName, position.ToString()}));
                    };
                    break;
                case ScriptKeys.SETOBJECTPERMISSIONS:
                    execute = () =>
                    {
                        if (
                            !HasCorradePermission(group,
                                (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        int who = 0;
                        Parallel.ForEach(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.WHO), message))
                                .Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER}, StringSplitOptions.RemoveEmptyEntries),
                            o =>
                                Parallel.ForEach(
                                    typeof (PermissionWho).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    q => { who |= ((int) q.GetValue(null)); }));
                        int permissions = 0;
                        Parallel.ForEach(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PERMISSIONS), message))
                                .Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER}, StringSplitOptions.RemoveEmptyEntries),
                            o =>
                                Parallel.ForEach(
                                    typeof (PermissionMask).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    q => { permissions |= ((int) q.GetValue(null)); }));
                        Client.Objects.SetPermissions(Client.Network.CurrentSim, new List<uint> {primitive.LocalID},
                            (PermissionWho) who, (PermissionMask) permissions, true);
                    };
                    break;
                case ScriptKeys.OBJECTDEED:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Client.Objects.DeedObject(Client.Network.CurrentSim, primitive.LocalID, groupUUID);
                    };
                    break;
                case ScriptKeys.SETOBJECTGROUP:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        UUID groupUUID =
                            Configuration.GROUPS.FirstOrDefault(
                                o => o.Name.Equals(group, StringComparison.Ordinal)).UUID;
                        if (groupUUID.Equals(UUID.Zero) &&
                            !GroupNameToUUID(group, Configuration.SERVICES_TIMEOUT, ref groupUUID))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.GROUP_NOT_FOUND));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Client.Objects.SetObjectsGroup(Client.Network.CurrentSim, new List<uint> {primitive.LocalID},
                            groupUUID);
                    };
                    break;
                case ScriptKeys.SETOBJECTSALEINFO:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        int price;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.PRICE), message)),
                                out price))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PRICE));
                        }
                        if (price < 0)
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_PRICE));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        FieldInfo saleTypeInfo = typeof (SaleType).GetFields(BindingFlags.Public |
                                                                             BindingFlags.Static)
                            .FirstOrDefault(o =>
                                o.Name.Equals(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message)),
                                    StringComparison.Ordinal));
                        Client.Objects.SetSaleInfo(Client.Network.CurrentSim, primitive.LocalID, saleTypeInfo != null
                            ? (SaleType)
                                saleTypeInfo.GetValue(null)
                            : SaleType.Copy, price);
                    };
                    break;
                case ScriptKeys.SETOBJECTPOSITION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        Client.Objects.SetPosition(Client.Network.CurrentSim, primitive.LocalID, position);
                    };
                    break;
                case ScriptKeys.SETOBJECTROTATION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        Quaternion rotation;
                        if (
                            !Quaternion.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ROTATION), message)),
                                out rotation))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.INVALID_ROTATION));
                        }
                        Client.Objects.SetRotation(Client.Network.CurrentSim, primitive.LocalID, rotation);
                    };
                    break;
                case ScriptKeys.SETOBJECTNAME:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        string name =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        if (string.IsNullOrEmpty(name))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_NAME_PROVIDED));
                        }
                        Client.Objects.SetName(Client.Network.CurrentSim, primitive.LocalID, name);
                    };
                    break;
                case ScriptKeys.SETOBJECTDESCRIPTION:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        float range;
                        if (
                            !float.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.RANGE), message)),
                                out range))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_RANGE_PROVIDED));
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message)),
                                range,
                                Configuration.SERVICES_TIMEOUT,
                                ref primitive))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.PRIMITIVE_NOT_FOUND));
                        }
                        string description =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DESCRIPTION), message));
                        if (string.IsNullOrEmpty(description))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_DESCRIPTION_PROVIDED));
                        }
                        Client.Objects.SetDescription(Client.Network.CurrentSim, primitive.LocalID, description);
                    };
                    break;
                case ScriptKeys.WEARFOLDER:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_GROOMING))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        string folder =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.FOLDER), message));
                        if (string.IsNullOrEmpty(folder))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_FOLDER_SPECIFIED));
                        }
                        bool replace;
                        if (
                            !bool.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.REPLACE), message)),
                                out replace))
                        {
                            replace = true;
                        }
                        List<InventoryBase> items =
                            GetInventoryFolderContents(Client.Inventory.Store.RootFolder, folder,
                                Configuration.SERVICES_TIMEOUT).Cast<InventoryBase>().ToList();
                        Client.Appearance.WearOutfit(items, replace);
                    };
                    break;
                case ScriptKeys.PLAYSOUND:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_INTERACT))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.POSITION), message)),
                                out position))
                        {
                            position = Client.Self.SimPosition;
                        }
                        float gain;
                        if (!float.TryParse(
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.GAIN), message)),
                            out gain))
                        {
                            gain = 1;
                        }
                        UUID itemUUID;
                        string item =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.ITEM), message));
                        if (!UUID.TryParse(item, out itemUUID))
                        {
                            InventoryItem inventoryItem =
                                SearchInventoryItem(Client.Inventory.Store.RootFolder, item,
                                    Configuration.SERVICES_TIMEOUT).FirstOrDefault();
                            if (inventoryItem == null)
                            {
                                throw new Exception(GetEnumDescription(ScriptError.INVENTORY_ITEM_NOT_FOUND));
                            }
                            itemUUID = inventoryItem.UUID;
                        }
                        Client.Sound.SendSoundTrigger(itemUUID, position, gain);
                    };
                    break;
                case ScriptKeys.LOGOUT:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_SYSTEM))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        ConnectionSemaphores.FirstOrDefault(o => o.Key.Equals('u')).Value.Set();
                    };
                    break;
                case ScriptKeys.VERSION:
                    execute = () => result.Add(GetEnumDescription(ResultKeys.VERSION), CORRADE_VERSION);
                    break;
                case ScriptKeys.DIRECTORYSEARCH:
                    execute = () =>
                    {
                        if (!HasCorradePermission(group, (int) Permissions.PERMISSION_DIRECTORY))
                        {
                            throw new Exception(GetEnumDescription(ScriptError.NO_CORRADE_PERMISSIONS));
                        }
                        int timeout;
                        if (
                            !int.TryParse(
                                wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TIMEOUT), message)),
                                out timeout))
                        {
                            timeout = Configuration.SERVICES_TIMEOUT;
                        }
                        object LockObject = new object();
                        List<string> csv = new List<string>();
                        int handledEvents = 0;
                        int counter = 1;
                        string name =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.NAME), message));
                        string fields =
                            wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message));
                        switch (
                            (Type)
                                wasGetEnumValueFromDescription<Type>(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.TYPE), message))
                                        .ToLower(CultureInfo.InvariantCulture)))
                        {
                            case Type.CLASSIFIED:
                                DirectoryManager.Classified searchClassified = new DirectoryManager.Classified();
                                wasCSVToStructure(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                                    ref searchClassified);
                                Dictionary<DirectoryManager.Classified, int> classifieds =
                                    new Dictionary<DirectoryManager.Classified, int>();
                                ManualResetEvent DirClassifiedsEvent = new ManualResetEvent(false);
                                EventHandler<DirClassifiedsReplyEventArgs> DirClassifiedsEventHandler =
                                    (sender, args) =>
                                    {
                                        Parallel.ForEach(args.Classifieds, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchClassified, searchClassified.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o,
                                                                    o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                classifieds.Add(o, score);
                                            }
                                        });
                                    };
                                Client.Directory.DirClassifiedsReply += DirClassifiedsEventHandler;
                                Client.Directory.StartClassifiedSearch(name);
                                DirClassifiedsEvent.WaitOne(timeout, false);
                                DirClassifiedsEvent.Close();
                                Client.Directory.DirClassifiedsReply -= DirClassifiedsEventHandler;
                                DirectoryManager.Classified topClassified =
                                    classifieds.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(
                                    wasGetFields(topClassified, topClassified.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            case Type.EVENT:
                                DirectoryManager.EventsSearchData searchEvent = new DirectoryManager.EventsSearchData();
                                wasCSVToStructure(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                                    ref searchEvent);
                                Dictionary<DirectoryManager.EventsSearchData, int> events =
                                    new Dictionary<DirectoryManager.EventsSearchData, int>();
                                ManualResetEvent DirEventsReplyEvent = new ManualResetEvent(false);
                                EventHandler<DirEventsReplyEventArgs> DirEventsEventHandler =
                                    (sender, args) =>
                                    {
                                        handledEvents += args.MatchedEvents.Count;
                                        Parallel.ForEach(args.MatchedEvents, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchEvent, searchEvent.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o, o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                events.Add(o, score);
                                            }
                                        });
                                        if ((handledEvents - counter)%
                                            LINDEN_CONSTANTS.DIRECTORY.EVENT.SEARCH_RESULTS_COUNT == 0)
                                        {
                                            ++counter;
                                            Client.Directory.StartEventsSearch(name, (uint) handledEvents);
                                        }
                                        DirEventsReplyEvent.Set();
                                    };
                                Client.Directory.DirEventsReply += DirEventsEventHandler;
                                Client.Directory.StartEventsSearch(name,
                                    (uint) handledEvents);
                                DirEventsReplyEvent.WaitOne(timeout, false);
                                Client.Directory.DirEventsReply -= DirEventsEventHandler;
                                DirectoryManager.EventsSearchData topEvent =
                                    events.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(wasGetFields(topEvent, topEvent.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            case Type.GROUP:
                                if (string.IsNullOrEmpty(name))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_SEARCH_TEXT_PROVIDED));
                                }
                                DirectoryManager.GroupSearchData searchGroup = new DirectoryManager.GroupSearchData();
                                wasCSVToStructure(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                                    ref searchGroup);
                                Dictionary<DirectoryManager.GroupSearchData, int> groups =
                                    new Dictionary<DirectoryManager.GroupSearchData, int>();
                                ManualResetEvent DirGroupsReplyEvent = new ManualResetEvent(false);
                                EventHandler<DirGroupsReplyEventArgs> DirGroupsEventHandler =
                                    (sender, args) =>
                                    {
                                        handledEvents += args.MatchedGroups.Count;
                                        Parallel.ForEach(args.MatchedGroups, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchGroup, searchGroup.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o, o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                groups.Add(o, score);
                                            }
                                        });
                                        if ((handledEvents - counter)%
                                            LINDEN_CONSTANTS.DIRECTORY.GROUP.SEARCH_RESULTS_COUNT == 0)
                                        {
                                            ++counter;
                                            Client.Directory.StartGroupSearch(name, handledEvents);
                                        }
                                        DirGroupsReplyEvent.Set();
                                    };
                                Client.Directory.DirGroupsReply += DirGroupsEventHandler;
                                Client.Directory.StartGroupSearch(name, handledEvents);
                                DirGroupsReplyEvent.WaitOne(timeout, false);
                                Client.Directory.DirGroupsReply -= DirGroupsEventHandler;
                                DirectoryManager.GroupSearchData topGroup =
                                    groups.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(wasGetFields(topGroup, topGroup.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            case Type.LAND:
                                DirectoryManager.DirectoryParcel searchLand = new DirectoryManager.DirectoryParcel();
                                wasCSVToStructure(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                                    ref searchLand);
                                Dictionary<DirectoryManager.DirectoryParcel, int> lands =
                                    new Dictionary<DirectoryManager.DirectoryParcel, int>();
                                ManualResetEvent DirLandReplyEvent = new ManualResetEvent(false);
                                EventHandler<DirLandReplyEventArgs> DirLandReplyEventArgs =
                                    (sender, args) =>
                                    {
                                        handledEvents += args.DirParcels.Count;
                                        Parallel.ForEach(args.DirParcels, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchLand, searchLand.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o, o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                lands.Add(o, score);
                                            }
                                        });
                                        if ((handledEvents - counter)%
                                            LINDEN_CONSTANTS.DIRECTORY.LAND.SEARCH_RESULTS_COUNT == 0)
                                        {
                                            ++counter;
                                            Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                                DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue,
                                                handledEvents);
                                        }
                                        DirLandReplyEvent.Set();
                                    };
                                Client.Directory.DirLandReply += DirLandReplyEventArgs;
                                Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                    DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue, handledEvents);
                                DirLandReplyEvent.WaitOne(timeout, false);
                                Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                                DirectoryManager.DirectoryParcel topLand =
                                    lands.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(wasGetFields(topLand, topLand.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            case Type.PEOPLE:
                                if (string.IsNullOrEmpty(name))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_SEARCH_TEXT_PROVIDED));
                                }
                                DirectoryManager.AgentSearchData searchAgent = new DirectoryManager.AgentSearchData();
                                Dictionary<DirectoryManager.AgentSearchData, int> agents =
                                    new Dictionary<DirectoryManager.AgentSearchData, int>();
                                ManualResetEvent AgentSearchDataEvent = new ManualResetEvent(false);
                                EventHandler<DirPeopleReplyEventArgs> DirPeopleReplyEventHandler =
                                    (sender, args) =>
                                    {
                                        handledEvents += args.MatchedPeople.Count;
                                        Parallel.ForEach(args.MatchedPeople, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchAgent, searchAgent.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o, o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                agents.Add(o, score);
                                            }
                                        });
                                        if ((handledEvents - counter)%
                                            LINDEN_CONSTANTS.DIRECTORY.PEOPLE.SEARCH_RESULTS_COUNT == 0)
                                        {
                                            ++counter;
                                            Client.Directory.StartPeopleSearch(name, handledEvents);
                                        }
                                        AgentSearchDataEvent.Set();
                                    };
                                Client.Directory.DirPeopleReply += DirPeopleReplyEventHandler;
                                Client.Directory.StartPeopleSearch(name, handledEvents);
                                AgentSearchDataEvent.WaitOne(timeout, false);
                                Client.Directory.DirPeopleReply -= DirPeopleReplyEventHandler;
                                DirectoryManager.AgentSearchData topAgent =
                                    agents.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(wasGetFields(topAgent, topAgent.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            case Type.PLACE:
                                if (string.IsNullOrEmpty(name))
                                {
                                    throw new Exception(GetEnumDescription(ScriptError.NO_SEARCH_TEXT_PROVIDED));
                                }
                                DirectoryManager.PlacesSearchData searchPlaces = new DirectoryManager.PlacesSearchData();
                                wasCSVToStructure(
                                    wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.DATA), message)),
                                    ref searchPlaces);
                                Dictionary<DirectoryManager.PlacesSearchData, int> places =
                                    new Dictionary<DirectoryManager.PlacesSearchData, int>();
                                ManualResetEvent DirPlacesReplyEvent = new ManualResetEvent(false);
                                EventHandler<PlacesReplyEventArgs> DirPlacesReplyEventHandler =
                                    (sender, args) =>
                                    {
                                        Parallel.ForEach(args.MatchedPlaces, o =>
                                        {
                                            int score = !string.IsNullOrEmpty(fields)
                                                ? wasGetFields(searchPlaces, searchPlaces.GetType().Name)
                                                    .Sum(
                                                        p =>
                                                            (from q in
                                                                wasGetFields(o, o.GetType().Name)
                                                                let r = wasGetInfoValue(p.Key, p.Value)
                                                                where r != null
                                                                let s = wasGetInfoValue(q.Key, q.Value)
                                                                where s != null
                                                                where r.Equals(s)
                                                                select r).Count())
                                                : 0;
                                            lock (LockObject)
                                            {
                                                places.Add(o, score);
                                            }
                                        });
                                    };
                                Client.Directory.PlacesReply += DirPlacesReplyEventHandler;
                                Client.Directory.StartPlacesSearch(name);
                                DirPlacesReplyEvent.WaitOne(timeout, false);
                                DirPlacesReplyEvent.Close();
                                Client.Directory.PlacesReply -= DirPlacesReplyEventHandler;
                                DirectoryManager.PlacesSearchData topPlace =
                                    places.OrderByDescending(o => o.Value).FirstOrDefault().Key;
                                Parallel.ForEach(wasGetFields(topPlace, topPlace.GetType().Name),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.Add(o.Key.Name);
                                            csv.AddRange(wasGetInfo(o.Key, o.Value));
                                        }
                                    });
                                break;
                            default:
                                throw new Exception(GetEnumDescription(ScriptError.UNKNOWN_DIRECTORY_SEARCH_TYPE));
                        }
                        if (!csv.Count.Equals(0))
                        {
                            result.Add(GetEnumDescription(ResultKeys.SEARCH),
                                string.Join(LINDEN_CONSTANTS.LSL.CSV_DELIMITER, csv.ToArray()));
                        }
                    };
                    break;
                default:
                    execute = () => { throw new Exception(GetEnumDescription(ScriptError.COMMAND_NOT_FOUND)); };
                    break;
            }

            // execute command and check for errors
            bool success = false;
            try
            {
                execute.Invoke();
                success = true;
            }
            catch (Exception e)
            {
                result.Add(GetEnumDescription(ResultKeys.ERROR), e.Message);
            }
            result.Add(GetEnumDescription(ResultKeys.SUCCESS), success.ToString(CultureInfo.InvariantCulture));

            // build afterburn
            System.Action afterburn = () =>
            {
                object LockObject = new object();
                Parallel.ForEach(wasKeyValueDecode(message), o =>
                {
                    // remove keys that are script keys or invalid key-value pairs
                    if (string.IsNullOrEmpty(o.Key) || wasGetEnumDescriptions<ScriptKeys>().Contains(o.Key) ||
                        string.IsNullOrEmpty(o.Value))
                        return;
                    lock (LockObject)
                    {
                        result.Add(wasUriEscapeDataString(o.Key), wasUriEscapeDataString(o.Value));
                    }
                });
            };
            afterburn.Invoke();

            // send callback
            System.Action callback = () =>
            {
                string url = wasUriUnescapeDataString(wasKeyValueGet(GetEnumDescription(ScriptKeys.CALLBACK), message));
                if (string.IsNullOrEmpty(url)) return;
                string error = wasPOST(url, wasKeyValueEscape(result));
                if (string.IsNullOrEmpty(error)) return;
                result.Add(GetEnumDescription(ScriptKeys.CALLBACK), url);
                result.Add(GetEnumDescription(ResultKeys.CALLBACKERROR), error);
            };
            callback.Invoke();

            return result;
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
        private static IEnumerable<string> GetStructuredData<T>(T structure, string query)
        {
            HashSet<string[]> result = new HashSet<string[]>();
            object LockObject = new object();
            Parallel.ForEach(query.Split(new[] {LINDEN_CONSTANTS.LSL.CSV_DELIMITER},
                StringSplitOptions.RemoveEmptyEntries), name =>
                {
                    KeyValuePair<FieldInfo, object> fi = wasGetFields(structure,
                        structure.GetType().Name)
                        .FirstOrDefault(o => o.Key.Name.Equals(name, StringComparison.Ordinal));

                    lock (LockObject)
                    {
                        List<string> data = new List<string> {name};
                        data.AddRange(wasGetInfo(fi.Key, fi.Value));
                        if (data.Count >= 2)
                        {
                            result.Add(data.ToArray());
                        }
                    }

                    KeyValuePair<PropertyInfo, object> pi =
                        wasGetProperties(structure, structure.GetType().Name)
                            .FirstOrDefault(
                                o => o.Key.Name.Equals(name, StringComparison.Ordinal));
                    lock (LockObject)
                    {
                        List<string> data = new List<string> {name};
                        data.AddRange(wasGetInfo(pi.Key, pi.Value));
                        if (data.Count >= 2)
                        {
                            result.Add(data.ToArray());
                        }
                    }
                });
            return result.SelectMany(data => data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Takes as input a CSV data values and sets the corresponding
        ///     structure's fields or properties from the CSV data.
        /// </summary>
        /// <typeparam name="T">the type of the structure</typeparam>
        /// <param name="data">a CSV string</param>
        /// <param name="structure">the structure to set the fields and properties for</param>
        private static void wasCSVToStructure<T>(string data, ref T structure)
        {
            foreach (
                KeyValuePair<string, string> match in
                    Regex.Matches(data, @"\s*(?<key>.+?)\s*,\s*(?<value>.+?)\s*(,|$)").
                        Cast<Match>().
                        ToDictionary(m => m.Groups["key"].Value, m => m.Groups["value"].Value))
            {
                KeyValuePair<string, string> localMatch = match;
                KeyValuePair<FieldInfo, object> fi =
                    wasGetFields(structure, structure.GetType().Name)
                        .FirstOrDefault(
                            o =>
                                o.Key.Name.Equals(localMatch.Key,
                                    StringComparison.Ordinal));

                wasSetInfo(fi.Key, fi.Value, match.Value, ref structure);

                KeyValuePair<PropertyInfo, object> pi =
                    wasGetProperties(structure, structure.GetType().Name)
                        .FirstOrDefault(
                            o =>
                                o.Key.Name.Equals(localMatch.Key,
                                    StringComparison.Ordinal));

                wasSetInfo(pi.Key, pi.Value, match.Value, ref structure);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sends a post request to an URL with set key-value pairs.
        /// </summary>
        /// <param name="URL">the url to send the message to</param>
        /// <param name="message">key-value pairs to send</param>
        /// <returns>the error message in case the request fails.</returns>
        private static string wasPOST(string URL, Dictionary<string, string> message)
        {
            try
            {
                byte[] byteArray =
                    Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "{0}",
                        wasKeyValueEncode(message)));
                WebRequest request = WebRequest.Create(URL);
                request.Timeout = Configuration.CALLBACK_TIMEOUT;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Flush();
                dataStream.Close();
                WebResponse response = request.GetResponse();
                dataStream = response.GetResponseStream();
                if (dataStream != null)
                {
                    StreamReader reader = new StreamReader(dataStream);
                    reader.ReadToEnd();
                    reader.Close();
                    //dataStream.Close();
                }
                response.Close();
                return string.Empty;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static void HandleTerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
        {
            if (e.Prim.LocalID == Client.Self.LocalID)
            {
                SetDefaultCamera();
            }
        }

        private static void HandleAvatarUpdate(object sender, AvatarUpdateEventArgs e)
        {
            if (e.Avatar.LocalID == Client.Self.LocalID)
            {
                SetDefaultCamera();
            }
        }

        private static void HandleSimChanged(object sender, SimChangedEventArgs e)
        {
            Client.Self.Movement.SetFOVVerticalAngle(Utils.TWO_PI - 0.05f);
        }

        private static void SetDefaultCamera()
        {
            // SetCamera 5m behind the avatar
            Client.Self.Movement.Camera.LookAt(
                Client.Self.SimPosition + new Vector3(-5, 0, 0)*Client.Self.Movement.BodyRotation,
                Client.Self.SimPosition
                );
        }

        #region NAME AND UUID RESOLVERS

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a group name to an UUID by using the directory search.
        /// </summary>
        /// <param name="groupName">the name of the group to resolve</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groupUUID">an object in which to store the UUID of the group</param>
        /// <returns>true if the group name could be resolved to an UUID</returns>
        private static bool GroupNameToUUID(string groupName, int millisecondsTimeout, ref UUID groupUUID)
        {
            UUID localGroupUUID = UUID.Zero;
            ManualResetEvent DirGroupsEvent = new ManualResetEvent(false);
            EventHandler<DirGroupsReplyEventArgs> DirGroupsReplyDelegate = (sender, args) =>
            {
                localGroupUUID = args.MatchedGroups.FirstOrDefault(o => o.GroupName.Equals(groupName)).GroupID;
                if (!localGroupUUID.Equals(UUID.Zero))
                {
                    DirGroupsEvent.Set();
                }
            };
            Client.Directory.DirGroupsReply += DirGroupsReplyDelegate;
            Client.Directory.StartGroupSearch(groupName, 0);
            if (!DirGroupsEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
                return false;
            }
            Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
            groupUUID = localGroupUUID;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent name to an agent UUID by searching the directory
        ///     services.
        /// </summary>
        /// <param name="agentFirstName">the first name of the agent</param>
        /// <param name="agentLastName">the last name of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="agentUUID">an object to store the agent UUID</param>
        /// <returns>true if the agent name could be resolved to an UUID</returns>
        private static bool AgentNameToUUID(string agentFirstName, string agentLastName, int millisecondsTimeout,
            ref UUID agentUUID)
        {
            UUID localAgentUUID = UUID.Zero;
            ManualResetEvent agentUUIDEvent = new ManualResetEvent(false);
            EventHandler<DirPeopleReplyEventArgs> DirPeopleReplyDelegate = (sender, args) =>
            {
                localAgentUUID =
                    args.MatchedPeople.FirstOrDefault(
                        o =>
                            o.FirstName.Equals(agentFirstName, StringComparison.OrdinalIgnoreCase) &&
                            o.LastName.Equals(agentLastName, StringComparison.OrdinalIgnoreCase)).AgentID;
                if (!localAgentUUID.Equals(UUID.Zero))
                {
                    agentUUIDEvent.Set();
                }
            };
            Client.Directory.DirPeopleReply += DirPeopleReplyDelegate;
            Client.Directory.StartPeopleSearch(
                String.Format(CultureInfo.InvariantCulture, "{0} {1}", agentFirstName, agentLastName), 0);
            if (!agentUUIDEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
                return false;
            }
            Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
            agentUUID = localAgentUUID;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent UUID to an agent name.
        /// </summary>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="agentName">an object to store the name of the agent in</param>
        /// <returns>true if the UUID could be resolved to a name</returns>
        private static bool AgentUUIDToName(UUID agentUUID, int millisecondsTimeout, ref string agentName)
        {
            if (agentUUID.Equals(UUID.Zero))
                return false;
            string localAgentName = string.Empty;
            ManualResetEvent agentNameEvent = new ManualResetEvent(false);
            EventHandler<UUIDNameReplyEventArgs> UUIDNameReplyDelegate = (sender, args) =>
            {
                localAgentName = args.Names.FirstOrDefault(o => o.Key.Equals(agentUUID)).Value;
                if (!string.IsNullOrEmpty(localAgentName))
                {
                    agentNameEvent.Set();
                }
            };
            Client.Avatars.UUIDNameReply += UUIDNameReplyDelegate;
            Client.Avatars.RequestAvatarName(agentUUID);
            if (!agentNameEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
                return false;
            }
            Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
            agentName = localAgentName;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="roleName">the name of the role to be resolved to an UUID</param>
        /// <param name="groupUUID">the UUID of the group to query for the role UUID</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="roleUUID">an UUID object to store the role UUID in</param>
        /// <returns>true if the role could be found</returns>
        private static bool RoleNameToRoleUUID(string roleName, UUID groupUUID, int millisecondsTimeout,
            ref UUID roleUUID)
        {
            UUID localRoleUUID = UUID.Zero;
            ManualResetEvent GroupRoleDataEvent = new ManualResetEvent(false);
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                localRoleUUID =
                    args.Roles.FirstOrDefault(o => o.Value.Name.Equals(roleName, StringComparison.Ordinal))
                        .Key;
                if (!localRoleUUID.Equals(UUID.Zero))
                {
                    GroupRoleDataEvent.Set();
                }
            };
            Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
            Client.Groups.RequestGroupRoles(groupUUID);
            if (!GroupRoleDataEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                return false;
            }
            Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            roleUUID = localRoleUUID;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="RoleUUID">the UUID of the role to be resolved to a name</param>
        /// <param name="GroupUUID">the UUID of the group to query for the role name</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="roleName">a string object to store the role name in</param>
        /// <returns>true if the role could be resolved</returns>
        private static bool RoleUUIDToName(UUID RoleUUID, UUID GroupUUID, int millisecondsTimeout, ref string roleName)
        {
            if (RoleUUID.Equals(UUID.Zero) || GroupUUID.Equals(UUID.Zero))
                return false;
            string localRoleName = string.Empty;
            ManualResetEvent GroupRoleDataEvent = new ManualResetEvent(false);
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                localRoleName = args.Roles.FirstOrDefault(o => o.Key.Equals(RoleUUID)).Value.Name;
                if (!string.IsNullOrEmpty(localRoleName))
                {
                    GroupRoleDataEvent.Set();
                }
            };

            Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
            Client.Groups.RequestGroupRoles(GroupUUID);
            if (!GroupRoleDataEvent.WaitOne(millisecondsTimeout, false))
            {
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                return false;
            }
            Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            roleName = localRoleName;
            return true;
        }

        #endregion

        #region KEY-VALUE DATA

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>true if the key was found in data</returns>
        private static string wasKeyValueGet(string key, string data)
        {
            foreach (string tuples in data.Split('&'))
            {
                string[] tuple = tuples.Split('=');
                if (!tuple.Length.Equals(2))
                {
                    continue;
                }
                if (tuple[0].Equals(key, StringComparison.Ordinal))
                {
                    return tuple[1];
                }
            }
            return string.Empty;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns a key-value data string with a key set to a given value.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="value">the value to set the key to</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>
        ///     a key-value data string or the empty string if either key or
        ///     value are empty
        /// </returns>
        private static string wasKeyValueSet(string key, string value, string data)
        {
            List<string> output = new List<string>();
            foreach (string tuples in data.Split('&'))
            {
                string[] tuple = tuples.Split('=');
                if (!tuple.Length.Equals(2))
                {
                    continue;
                }
                if (tuple[0].Equals(key, StringComparison.Ordinal))
                {
                    tuple[1] = value;
                }
                output.Add(string.Join("=", tuple));
            }
            string add = string.Join("=", new[] {key, value});
            if (!output.Contains(add))
            {
                output.Add(add);
            }
            return string.Join("&", output.ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Deletes a key-value pair from a string referenced by a key.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>a key-value pair string</returns>
        private static string wasKeyValueDelete(string key, string data)
        {
            List<string> output = new List<string>();
            foreach (string tuples in data.Split('&'))
            {
                string[] tuple = tuples.Split('=');
                if (!tuple.Length.Equals(2))
                {
                    continue;
                }
                if (tuple[0].Equals(key, StringComparison.Ordinal))
                {
                    continue;
                }
                output.Add(string.Join("=", tuple));
            }
            return string.Join("&", output.ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decodes key-value pair data to a dictionary.
        /// </summary>
        /// <param name="data">the key-value pair data</param>
        /// <returns>a dictionary containing the keys and values</returns>
        private static Dictionary<string, string> wasKeyValueDecode(string data)
        {
            Dictionary<string, string> output = new Dictionary<string, string>();
            foreach (string tuples in data.Split('&'))
            {
                string[] tuple = tuples.Split('=');
                if (!tuple.Length.Equals(2))
                {
                    continue;
                }
                if (output.ContainsKey(tuple[0]))
                {
                    continue;
                }
                output.Add(tuple[0], tuple[1]);
            }
            return output;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Serialises a dictionary to key-value data.
        /// </summary>
        /// <param name="data">a dictionary</param>
        /// <returns>a key-value data encoded string</returns>
        private static string wasKeyValueEncode(Dictionary<string, string> data)
        {
            List<string> output = new List<string>();
            foreach (KeyValuePair<string, string> tuple in data)
            {
                output.Add(string.Join("=", new[] {tuple.Key, tuple.Value}));
            }
            return string.Join("&", output.ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>URI unescapes an RFC3986 URI escaped string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>the resulting string</returns>
        private static string wasUriUnescapeDataString(string data)
        {
            return
                Regex.Matches(data, @"%([0-9A-Fa-f]+?){2}")
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .Distinct()
                    .Aggregate(data,
                        (current, match) =>
                            current.Replace(match,
                                ((char)
                                    int.Parse(match.Substring(1), NumberStyles.AllowHexSpecifier,
                                        CultureInfo.InvariantCulture)).ToString(
                                            CultureInfo.InvariantCulture)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC3986 URI Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC3986 escaped string</returns>
        private static string wasUriEscapeDataString(string data)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in data.Select(o => o))
            {
                if (char.IsLetter(c) || char.IsDigit(c))
                {
                    result.Append(c);
                    continue;
                }
                result.AppendFormat("%{0:X2}", (int) c);
            }
            return result.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>Escapes a dictionary's keys and values for sending as POST data.</summary>
        /// <param name="data">A dictionary containing keys and values to be escaped</param>
        private static Dictionary<string, string> wasKeyValueEscape(Dictionary<string, string> data)
        {
            Dictionary<string, string> output = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> tuple in data)
            {
                output.Add(wasUriEscapeDataString(tuple.Key), wasUriEscapeDataString(tuple.Value));
            }
            return output;
        }

        #endregion

        /// <summary>
        ///     Possible actions.
        /// </summary>
        private enum Action : uint
        {
            [Description("get")] GET,
            [Description("set")] SET,
            [Description("add")] ADD,
            [Description("remove")] REMOVE,
            [Description("start")] START,
            [Description("stop")] STOP,
            [Description("mute")] MUTE,
            [Description("unmute")] UNMUTE,
            [Description("restart")] RESTART,
            [Description("cancel")] CANCEL,
            [Description("accept")] ACCEPT,
            [Description("decline")] DECLINE,
            [Description("online")] ONLINE,
            [Description("offline")] OFFLINE,
            [Description("request")] REQUEST,
            [Description("response")] RESPONSE,
            [Description("delete")] DELETE
        }

        /// <summary>
        ///     Constants used by Corrade.
        /// </summary>
        private struct CORRADE_CONSTANTS
        {
            /// <summary>
            ///     Censor characters for passwords.
            /// </summary>
            public const string PASSWORD_CENSOR = "***";

            /// <summary>
            ///     Corrade channel sent to the simulator.
            /// </summary>
            public const string CLIENT_CHANNEL = @"[Wizardry and Steamworks]:Corrade";

            public const string WEB_REQUEST = @"Web Request";
            public const string POST = @"POST";
            public const string TEXT_HTML = "text/html";

            public struct HTTP_CODES
            {
                public const int OK = 200;
            }
        }

        private struct Configuration
        {
            public static string FIRST_NAME;

            public static string LAST_NAME;

            public static string PASSWORD;

            public static string LOGIN_URL;

            public static bool HTTP_SERVER;

            public static string HTTP_SERVER_PREFIX;

            public static int CALLBACK_TIMEOUT;

            public static int SERVICES_TIMEOUT;

            public static bool TOS_ACCEPTED;

            public static string START_LOCATION;

            public static string NETWORK_CARD_MAC;

            public static string LOG_FILE;

            public static bool AUTO_ACTIVATE_GROUP;

            public static int GROUP_CREATE_FEE;

            public static HashSet<Group> GROUPS;

            public static HashSet<Master> MASTERS;

            public static void Load(string file)
            {
                FIRST_NAME = string.Empty;
                LAST_NAME = string.Empty;
                PASSWORD = string.Empty;
                LOGIN_URL = string.Empty;
                HTTP_SERVER = false;
                HTTP_SERVER_PREFIX = "http://+:8080/";
                CALLBACK_TIMEOUT = 5000;
                SERVICES_TIMEOUT = 60000;
                TOS_ACCEPTED = false;
                START_LOCATION = "last";
                NETWORK_CARD_MAC = string.Empty;
                LOG_FILE = "Corrade.log";
                AUTO_ACTIVATE_GROUP = false;
                GROUP_CREATE_FEE = 100;
                GROUPS = new HashSet<Group>();
                MASTERS = new HashSet<Master>();

                try
                {
                    file = File.ReadAllText(file);
                }
                catch (Exception e)
                {
                    Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE), e.Message);
                    Environment.Exit(1);
                }

                XmlDocument conf = new XmlDocument();
                try
                {
                    conf.LoadXml(file);
                }
                catch (XmlException e)
                {
                    Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE), e.Message);
                    Environment.Exit(1);
                }

                XmlNode root = conf.DocumentElement;
                if (root == null)
                {
                    Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE));
                    Environment.Exit(1);
                }
                if (root != null)
                {
                    XmlNodeList nodeList = root.SelectNodes("/config/client/*");
                    if (nodeList == null)
                        return;
                    try
                    {
                        foreach (XmlNode client in nodeList)
                            switch (client.Name.ToLower(CultureInfo.InvariantCulture))
                            {
                                case ConfigurationKeys.FIRST_NAME:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    FIRST_NAME = client.InnerText;
                                    break;
                                case ConfigurationKeys.LAST_NAME:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    LAST_NAME = client.InnerText;
                                    break;
                                case ConfigurationKeys.PASSWORD:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    PASSWORD = client.InnerText;
                                    break;
                                case ConfigurationKeys.LOGIN_URL:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    LOGIN_URL = client.InnerText;
                                    break;
                                case ConfigurationKeys.HTTP_SERVER:
                                    if (!bool.TryParse(client.InnerText, out HTTP_SERVER))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.HTTP_SERVER_PREFIX:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    HTTP_SERVER_PREFIX = client.InnerText;
                                    break;
                                case ConfigurationKeys.CALLBACK_TIMEOUT:
                                    if (!int.TryParse(client.InnerText, out CALLBACK_TIMEOUT))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.SERVICES_TIMEOUT:
                                    if (!int.TryParse(client.InnerText, out SERVICES_TIMEOUT))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.TOS_ACCEPTED:
                                    if (!bool.TryParse(client.InnerText, out TOS_ACCEPTED))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.GROUP_CREATE_FEE:
                                    if (!int.TryParse(client.InnerText, out GROUP_CREATE_FEE))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.AUTO_ACTIVATE_GROUP:
                                    if (!bool.TryParse(client.InnerText, out AUTO_ACTIVATE_GROUP))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    break;
                                case ConfigurationKeys.START_LOCATION:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    START_LOCATION = client.InnerText;
                                    break;
                                case ConfigurationKeys.NETWORK_CARD_MAC:
                                    if (!string.IsNullOrEmpty(client.InnerText))
                                    {
                                        NETWORK_CARD_MAC = client.InnerText;
                                    }
                                    break;
                                case ConfigurationKeys.LOG:
                                    if (string.IsNullOrEmpty(client.InnerText))
                                    {
                                        throw new Exception("error in client section");
                                    }
                                    LOG_FILE = client.InnerText;
                                    break;
                            }
                    }
                    catch (Exception e)
                    {
                        Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE), e.Message);
                        Environment.Exit(1);
                    }

                    // Process masters.
                    nodeList = root.SelectNodes("/config/masters/*");
                    if (nodeList == null)
                        return;
                    try
                    {
                        foreach (XmlNode mastersNode in nodeList)
                        {
                            Master configMaster = new Master();
                            foreach (XmlNode masterNode in mastersNode.ChildNodes)
                            {
                                switch (masterNode.Name.ToLower(CultureInfo.InvariantCulture))
                                {
                                    case ConfigurationKeys.FIRST_NAME:
                                        if (string.IsNullOrEmpty(masterNode.InnerText))
                                        {
                                            throw new Exception("error in masters section");
                                        }
                                        configMaster.FirstName = masterNode.InnerText;
                                        break;
                                    case ConfigurationKeys.LAST_NAME:
                                        if (string.IsNullOrEmpty(masterNode.InnerText))
                                        {
                                            throw new Exception("error in masters section");
                                        }
                                        configMaster.LastName = masterNode.InnerText;
                                        break;
                                }
                            }
                            MASTERS.Add(configMaster);
                        }
                    }
                    catch (Exception e)
                    {
                        Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE), e.Message);
                        Environment.Exit(1);
                    }

                    // Process groups.
                    nodeList = root.SelectNodes("/config/groups/*");
                    if (nodeList == null)
                        return;
                    try
                    {
                        foreach (XmlNode groupsNode in nodeList)
                        {
                            Group configGroup = new Group();
                            foreach (XmlNode groupNode in groupsNode.ChildNodes)
                            {
                                switch (groupNode.Name.ToLower(CultureInfo.InvariantCulture))
                                {
                                    case ConfigurationKeys.NAME:
                                        if (string.IsNullOrEmpty(groupNode.InnerText))
                                        {
                                            throw new Exception("error in group section");
                                        }
                                        configGroup.Name = groupNode.InnerText;
                                        break;
                                    case ConfigurationKeys.UUID:
                                        if (!UUID.TryParse(groupNode.InnerText, out configGroup.UUID))
                                        {
                                            throw new Exception("error in group section");
                                        }
                                        break;
                                    case ConfigurationKeys.PASSWORD:
                                        if (string.IsNullOrEmpty(groupNode.InnerText))
                                        {
                                            throw new Exception("error in group section");
                                        }
                                        configGroup.Password = groupNode.InnerText;
                                        break;
                                    case ConfigurationKeys.CHATLOG:
                                        if (string.IsNullOrEmpty(groupNode.InnerText))
                                        {
                                            throw new Exception("error in group section");
                                        }
                                        configGroup.ChatLog = groupNode.InnerText;
                                        break;
                                    case ConfigurationKeys.DATABASE:
                                        if (string.IsNullOrEmpty(groupNode.InnerText))
                                        {
                                            throw new Exception("error in group section");
                                        }
                                        configGroup.DatabaseFile = groupNode.InnerText;
                                        break;
                                    case ConfigurationKeys.PERMISSIONS:
                                        XmlNodeList permissionNodeList = groupNode.SelectNodes("*");
                                        if (permissionNodeList == null)
                                        {
                                            throw new Exception("error in group permission section");
                                        }
                                        uint permissionMask = 0;
                                        foreach (XmlNode permissioNode in permissionNodeList)
                                        {
                                            XmlNode node = permissioNode;
                                            Parallel.ForEach(
                                                wasGetEnumDescriptions<Permissions>()
                                                    .Where(name => name.Equals(node.Name,
                                                        StringComparison.Ordinal)), name =>
                                                        {
                                                            bool granted;
                                                            if (!bool.TryParse(node.InnerText, out granted))
                                                            {
                                                                throw new Exception("error in group permission section");
                                                            }
                                                            if (granted)
                                                            {
                                                                permissionMask = permissionMask |
                                                                                 wasGetEnumValueFromDescription
                                                                                     <Permissions>(name);
                                                            }
                                                        });
                                        }
                                        configGroup.PermissionMask = permissionMask;
                                        break;
                                    case ConfigurationKeys.NOTIFICATIONS:
                                        XmlNodeList notificationNodeList = groupNode.SelectNodes("*");
                                        if (notificationNodeList == null)
                                        {
                                            throw new Exception("error in group notification section");
                                        }
                                        uint notificationMask = 0;
                                        foreach (XmlNode notificationNode in notificationNodeList)
                                        {
                                            XmlNode node = notificationNode;
                                            Parallel.ForEach(
                                                wasGetEnumDescriptions<Notifications>()
                                                    .Where(name => name.Equals(node.Name,
                                                        StringComparison.Ordinal)), name =>
                                                        {
                                                            bool granted;
                                                            if (!bool.TryParse(node.InnerText, out granted))
                                                            {
                                                                throw new Exception(
                                                                    "error in group notification section");
                                                            }
                                                            if (granted)
                                                            {
                                                                notificationMask = notificationMask |
                                                                                   wasGetEnumValueFromDescription
                                                                                       <Notifications>(name);
                                                            }
                                                        });
                                        }
                                        configGroup.NotificationMask = notificationMask;
                                        break;
                                }
                            }
                            GROUPS.Add(configGroup);
                        }
                    }
                    catch (Exception e)
                    {
                        Feedback(GetEnumDescription(ConsoleError.INVALID_CONFIGURATION_FILE), e.Message);
                        Environment.Exit(1);
                    }
                }
                Feedback(GetEnumDescription(ConsoleError.READ_CONFIGURATION_FILE));
            }
        }

        /// <summary>
        ///     Configuration keys.
        /// </summary>
        private struct ConfigurationKeys
        {
            public const string FIRST_NAME = "firstname";
            public const string LAST_NAME = "lastname";
            public const string LOGIN_URL = "loginurl";
            public const string HTTP_SERVER = "httpserver";
            public const string HTTP_SERVER_PREFIX = "httpserverprefix";
            public const string CALLBACK_TIMEOUT = "callbacktimeout";
            public const string SERVICES_TIMEOUT = "servicestimeout";
            public const string TOS_ACCEPTED = "tosaccepted";
            public const string AUTO_ACTIVATE_GROUP = "autoactivategroup";
            public const string GROUP_CREATE_FEE = "groupcreatefee";
            public const string START_LOCATION = "startlocation";
            public const string NETWORK_CARD_MAC = "networkcardmac";
            public const string LOG = "log";
            public const string NAME = "name";
            public const string UUID = "uuid";
            public const string PASSWORD = "password";
            public const string CHATLOG = "chatlog";
            public const string DATABASE = "database";
            public const string PERMISSIONS = "permissions";
            public const string NOTIFICATIONS = "notifications";
        }

        /// <summary>
        ///     Structure containing error messages printed on console for the owner.
        /// </summary>
        private enum ConsoleError
        {
            [Description("access denied")] ACCESS_DENIED = 1,
            [Description("invalid configuration file")] INVALID_CONFIGURATION_FILE,
            [Description("the Terms of Service (TOS) have not been accepted, please check your configuration file")] TOS_NOT_ACCEPTED,
            [Description("teleport failed")] TELEPORT_FAILED,
            [Description("teleport succeeded")] TELEPORT_SUCCEEDED,
            [Description("accepting teleport lure")] ACCEPTING_TELEPORT_LURE,
            [Description("got server message")] GOT_SERVER_MESSAGE,
            [Description("accepted friendship")] ACCEPTED_FRIENDSHIP,
            [Description("login failed")] LOGIN_FAILED,
            [Description("login succeeded")] LOGIN_SUCCEEDED,
            [Description("failed to set appearance")] APPEARANCE_SET_FAILED,
            [Description("appearance set")] APPEARANCE_SET_SUCCEEDED,
            [Description("all simulators disconnected")] ALL_SIMULATORS_DISCONNECTED,
            [Description("simulator connected")] SIMULATOR_CONNECTED,
            [Description("event queue started")] EVENT_QUEUE_STARTED,
            [Description("disconnected")] DISCONNECTED,
            [Description("logging out")] LOGGING_OUT,
            [Description("logging in")] LOGGING_IN,
            [Description("could not write to group chat logfile")] COULD_NOT_WRITE_TO_GROUP_CHAT_LOGFILE,
            [Description("got inventory offer")] GOT_INVENTORY_OFFER,
            [Description("acceping group invite")] ACCEPTING_GROUP_INVITE,
            [Description("agent not found")] AGENT_NOT_FOUND,
            [Description("got group message")] GOT_GROUP_MESSAGE,
            [Description("got teleport lure")] GOT_TELEPORT_LURE,
            [Description("got group invite")] GOT_GROUP_INVITE,
            [Description("read configuration file")] READ_CONFIGURATION_FILE,
            [Description("configuration file modified")] CONFIGURATION_FILE_MODIFIED,
            [Description("notification could not be sent")] NOTIFICATION_COULD_NOT_BE_SENT,
            [Description("got region message")] GOT_REGION_MESSAGE,
            [Description("got group message")] GOT_GROUP_NOTICE,
            [Description("got insant message")] GOT_INSTANT_MESSAGE,
            [Description("HTTP server error")] HTTP_SERVER_ERROR,
            [Description("HTTP server not supported")] HTTP_SERVER_NOT_SUPPORTED,
            [Description("starting HTTP server")] STARTING_HTTP_SERVER,
            [Description("stopping HTTP server")] STOPPING_HTTP_SERVER
        }

        /// <summary>
        ///     Possible entities.
        /// </summary>
        private enum Entity : uint
        {
            [Description("avatar")] AVATAR,
            [Description("local")] LOCAL,
            [Description("group")] GROUP,
            [Description("estate")] ESTATE,
            [Description("region")] REGION,
            [Description("object")] OBJECT,
            [Description("parcel")] PARCEL
        }

        /// <summary>
        ///     Group structure.
        /// </summary>
        private struct Group
        {
            public string ChatLog;
            public string DatabaseFile;
            public string Name;
            public uint NotificationMask;
            public string Password;
            public uint PermissionMask;
            public UUID UUID;
        }

        /// <summary>
        ///     Linden constants.
        /// </summary>
        private struct LINDEN_CONSTANTS
        {
            public struct ALERTS
            {
                public const string NO_ROOM_TO_SIT_HERE = @"No room to sit here, try another spot.";

                public const string UNABLE_TO_SET_HOME =
                    @"You can only set your 'Home Location' on your land or at a mainland Infohub.";
            }

            public struct AVATARS
            {
                public const int SET_DISPLAY_NAME_SUCCESS = 200;
            }

            public struct DIRECTORY
            {
                public struct EVENT
                {
                    public const int SEARCH_RESULTS_COUNT = 200;
                }

                public struct GROUP
                {
                    public const int SEARCH_RESULTS_COUNT = 100;
                }

                public struct LAND
                {
                    public const int SEARCH_RESULTS_COUNT = 100;
                }

                public struct PEOPLE
                {
                    public const int SEARCH_RESULTS_COUNT = 100;
                }
            }

            public struct ESTATE
            {
                public const int REGION_RESTART_DELAY = 120;

                public struct MESSAGES
                {
                    public const string REGION_RESTART_MESSAGE = "restart";
                }
            }

            public struct GRID
            {
                public const string SECOND_LIFE = @"Second Life";
            }

            public struct GROUPS
            {
                public const int MAXIMUM_NUMBER_OF_ROLES = 10;
            }

            public struct LSL
            {
                public const string CSV_DELIMITER = @", ";
            }
        }

        /// <summary>
        ///     Masters structure.
        /// </summary>
        private struct Master
        {
            public string FirstName;
            public string LastName;
        }

        /// <summary>
        ///     A Corrade notification.
        /// </summary>
        private struct Notification
        {
            public string GROUP;
            public int NOTIFICATION_MASK;
            public string URL;
        }

        /// <summary>
        ///     Corrade notification types.
        /// </summary>
        [Flags]
        private enum Notifications : uint
        {
            [Description("alert")] NOTIFICATION_ALERT_MESSAGE = 1,
            [Description("region")] NOTIFICATION_REGION_MESSAGE = 2,
            [Description("group")] NOTIFICATION_GROUP_MESSAGE = 4,
            [Description("balance")] NOTIFICATION_BALANCE = 8,
            [Description("message")] NOTIFICATION_INSTANT_MESSAGE = 16,
            [Description("notice")] NOTIFICATION_GROUP_NOTICE = 32,
            [Description("local")] NOTIFICATION_LOCAL_CHAT = 64,
            [Description("dialog")] NOTIFICATION_SCRIPT_DIALOG = 128,
            [Description("friendship")] NOTIFICATION_FRIENDSHIP = 256,
            [Description("inventory")] NOTIFICATION_INVENTORY_OFFER = 512,
            [Description("permission")] NOTIFICATION_SCRIPT_PERMISSION = 1024,
            [Description("lure")] NOTIFICATION_TELEPORT_LURE = 2048
        }

        /// <summary>
        ///     Corrade permissions.
        /// </summary>
        [Flags]
        private enum Permissions : uint
        {
            [Description("movement")] PERMISSION_MOVEMENT = 1,
            [Description("economy")] PERMISSION_ECONOMY = 2,
            [Description("land")] PERMISSION_LAND = 4,
            [Description("grooming")] PERMISSION_GROOMING = 8,
            [Description("inventory")] PERMISSION_INVENTORY = 16,
            [Description("interact")] PERMISSION_INTERACT = 32,
            [Description("mute")] PERMISSION_MUTE = 64,
            [Description("database")] PERMISSION_DATABASE = 128,
            [Description("notifications")] PERMISSION_NOTIFICATIONS = 256,
            [Description("talk")] PERMISSION_TALK = 512,
            [Description("directory")] PERMISSION_DIRECTORY = 1024,
            [Description("system")] PERMISSION_SYSTEM = 2048,
            [Description("friendship")] PERMISSION_FRIENDSHIP = 4096
        }

        /// <summary>
        ///     Keys returned by Corrade.
        /// </summary>
        private enum ResultKeys : uint
        {
            [Description("version")] VERSION,
            [Description("positions")] POSITIONS,
            [Description("primitives")] PRIMITIVES,
            [Description("avatars")] AVATARS,
            [Description("requests")] REQUESTS,
            [Description("friends")] FRIENDS,
            [Description("displayname")] DISPLAYNAME,
            [Description("inventory")] INVENTORY,
            [Description("running")] RUNNING,
            [Description("particlesystem")] PARTICLESYSTEM,
            [Description("search")] SEARCH,
            [Description("list")] LIST,
            [Description("top")] TOP,
            [Description("animations")] ANIMATIONS,
            [Description("data")] DATA,
            [Description("attachments")] ATTACHMENTS,
            [Description("roles")] ROLES,
            [Description("members")] MEMBERS,
            [Description("powers")] POWERS,
            [Description("balance")] BALANCE,
            [Description("owners")] OWNERS,
            [Description("error")] ERROR,
            [Description("callbackerror")] CALLBACKERROR,
            [Description("success")] SUCCESS,
            [Description("mutes")] MUTES,
            [Description("notifications")] NOTIFICATIONS,
            [Description("wearables")] WEARABLES
        }

        /// <summary>
        ///     Structure containing errors returned to scripts.
        /// </summary>
        private enum ScriptError
        {
            [Description("could not join group")] COULD_NOT_JOIN_GROUP = 1,
            [Description("could not leave group")] COULD_NOT_LEAVE_GROUP,
            [Description("agent not found")] AGENT_NOT_FOUND,
            [Description("group not found")] GROUP_NOT_FOUND,
            [Description("already in group")] ALREADY_IN_GROUP,
            [Description("not in group")] NOT_IN_GROUP,
            [Description("role not found")] ROLE_NOT_FOUND,
            [Description("command not found")] COMMAND_NOT_FOUND,
            [Description("could not eject agent")] COULD_NOT_EJECT_AGENT,
            [Description("no group power for command")] NO_GROUP_POWER_FOR_COMMAND,
            [Description("cannot eject owners")] CANNOT_EJECT_OWNERS,
            [Description("inventory item not found")] INVENTORY_ITEM_NOT_FOUND,
            [Description("invalid pay amount")] INVALID_PAY_AMOUNT,
            [Description("insufficient funds")] INSUFFICIENT_FUNDS,
            [Description("invalid pay target")] INVALID_PAY_TARGET,
            [Description("timeout waiting for balance")] TIMEOUT_WAITING_FOR_BALANCE,
            [Description("teleport failed")] TELEPORT_FAILED,
            [Description("primitive not found")] PRIMITIVE_NOT_FOUND,
            [Description("could not sit")] COULD_NOT_SIT,
            [Description("no Corrade permissions")] NO_CORRADE_PERMISSIONS,
            [Description("could not create group")] COULD_NOT_CREATE_GROUP,
            [Description("could not create role")] COULD_NOT_CREATE_ROLE,
            [Description("no role name specified")] NO_ROLE_NAME_SPECIFIED,
            [Description("timeout getting group roles members")] TIMEOUT_GETING_GROUP_ROLES_MEMBERS,
            [Description("timeout getting group roles")] TIMEOUT_GETTING_GROUP_ROLES,
            [Description("timeout getting role powers")] TIMEOUT_GETTING_ROLE_POWERS,
            [Description("could not find parcel")] COULD_NOT_FIND_PARCEL,
            [Description("unable to set home")] UNABLE_TO_SET_HOME,
            [Description("unable to go home")] UNABLE_TO_GO_HOME,
            [Description("timeout getting profile")] TIMEOUT_GETTING_PROFILE,
            [Description("texture not found")] TEXTURE_NOT_FOUND,
            [Description("type can only be voice or text")] TYPE_CAN_BE_VOICE_OR_TEXT,
            [Description("agent not in group")] AGENT_NOT_IN_GROUP,
            [Description("empty attachments")] EMPTY_ATTACHMENTS,
            [Description("could not get land users")] COULD_NOT_GET_LAND_USERS,
            [Description("no region specified")] NO_REGION_SPECIFIED,
            [Description("empty pick name")] EMPTY_PICK_NAME,
            [Description("unable to join group chat")] UNABLE_TO_JOIN_GROUP_CHAT,
            [Description("invalid position")] INVALID_POSITION,
            [Description("could not find title")] COULD_NOT_FIND_TITLE,
            [Description("fly action can only be start or stop")] FLY_ACTION_START_OR_STOP,
            [Description("invalid proposal text")] INVALID_PROPOSAL_TEXT,
            [Description("invalid proposal quorum")] INVALID_PROPOSAL_QUORUM,
            [Description("invalid proposal majority")] INVALID_PROPOSAL_MAJORITY,
            [Description("invalid proposal duration")] INVALID_PROPOSAL_DURATION,
            [Description("invalid mute target")] INVALID_MUTE_TARGET,
            [Description("unknown action")] UNKNOWN_ACTION,
            [Description("no database file configured")] NO_DATABASE_FILE_CONFIGURED,
            [Description("no database key specified")] NO_DATABASE_KEY_SPECIFIED,
            [Description("no database value specified")] NO_DATABASE_VALUE_SPECIFIED,
            [Description("unknown database action")] UNKNOWN_DATABASE_ACTION,
            [Description("cannot remove owner role")] CANNOT_REMOVE_OWNER_ROLE,
            [Description("cannot remove user from owner role")] CANNOT_REMOVE_USER_FROM_OWNER_ROLE,
            [Description("timeout getting picks")] TIMEOUT_GETTING_PICKS,
            [Description("maximum number of roles exceeded")] MAXIMUM_NUMBER_OF_ROLES_EXCEEDED,
            [Description("cannot delete a group member from the everyone role")] CANNOT_DELETE_A_GROUP_MEMBER_FROM_THE_EVERYONE_ROLE,
            [Description("group members are by default in the everyone role")] GROUP_MEMBERS_ARE_BY_DEFAULT_IN_THE_EVERYONE_ROLE,
            [Description("cannot delete the everyone role")] CANNOT_DELETE_THE_EVERYONE_ROLE,
            [Description("invalid url provided")] INVALID_URL_PROVIDED,
            [Description("invalid notification types")] INVALID_NOTIFICATION_TYPES,
            [Description("unknown notifications action")] UNKNOWN_NOTIFICATIONS_ACTION,
            [Description("notification not allowed")] NOTIFICATION_NOT_ALLOWED,
            [Description("no range provided")] NO_RANGE_PROVIDED,
            [Description("unknwon directory search type")] UNKNOWN_DIRECTORY_SEARCH_TYPE,
            [Description("no search text provided")] NO_SEARCH_TEXT_PROVIDED,
            [Description("unknwon restart action")] UNKNOWN_RESTART_ACTION,
            [Description("unknown move action")] UNKNOWN_MOVE_ACTION,
            [Description("timeout getting top scripts")] TIMEOUT_GETTING_TOP_SCRIPTS,
            [Description("timeout waiting for estate list")] TIMEOUT_WAITING_FOR_ESTATE_LIST,
            [Description("unknwon top type")] UNKNOWN_TOP_TYPE,
            [Description("unknown estate list action")] UNKNWON_ESTATE_LIST_ACTION,
            [Description("unknown estate list")] UNKNOWN_ESTATE_LIST,
            [Description("no item specified")] NO_ITEM_SPECIFIED,
            [Description("unknown animation action")] UNKNOWN_ANIMATION_ACTION,
            [Description("no channel specified")] NO_CHANNEL_SPECIFIED,
            [Description("no button index specified")] NO_BUTTON_INDEX_SPECIFIED,
            [Description("no button specified")] NO_BUTTON_SPECIFIED,
            [Description("no land rights")] NO_LAND_RIGHTS,
            [Description("unknown entity")] UNKNOWN_ENTITY,
            [Description("invalid rotation")] INVALID_ROTATION,
            [Description("could not set script state")] COULD_NOT_SET_SCRIPT_STATE,
            [Description("item is not a script")] ITEM_IS_NOT_A_SCRIPT,
            [Description("avatar not in range")] AVATAR_NOT_IN_RANGE,
            [Description("failed to get display name")] FAILED_TO_GET_DISPLAY_NAME,
            [Description("no name provided")] NO_NAME_PROVIDED,
            [Description("could not set display name")] COULD_NOT_SET_DISPLAY_NAME,
            [Description("timeout joining group")] TIMEOUT_JOINING_GROUP,
            [Description("timeout creating group")] TIMEOUT_CREATING_GROUP,
            [Description("timeout ejecting agent")] TIMEOUT_EJECTING_AGENT,
            [Description("timeout getting group role members")] TIMEOUT_GETTING_GROUP_ROLE_MEMBERS,
            [Description("timeout leaving group")] TIMEOUT_LEAVING_GROUP,
            [Description("timeout joining group chat")] TIMEOUT_JOINING_GROUP_CHAT,
            [Description("timeout during teleport")] TIMEOUT_DURING_TELEPORT,
            [Description("timeout requesting sit")] TIMEOUT_REQUESTING_SIT,
            [Description("timeout getting land users")] TIMEOUT_GETTING_LAND_USERS,
            [Description("timeout getting script state")] TIMEOUT_GETTING_SCRIPT_STATE,
            [Description("timeout updating mute list")] TIMEOUT_UPDATING_MUTE_LIST,
            [Description("timeout getting parcels")] TIMEOUT_GETTING_PARCELS,
            [Description("empty classified name")] EMPTY_CLASSIFIED_NAME,
            [Description("invalid price")] INVALID_PRICE,
            [Description("timeout getting classifieds")] TIMEOUT_GETTING_CLASSIFIEDS,
            [Description("could not find classified")] COULD_NOT_FIND_CLASSIFIED,
            [Description("invalid days")] INVALID_DAYS,
            [Description("invalid interval")] INVALID_INTERVAL,
            [Description("timeout getting group account summary")] TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY,
            [Description("friend not found")] FRIEND_NOT_FOUND,
            [Description("the agent already is a friend")] AGENT_ALREADY_FRIEND,
            [Description("no friendship offer found")] NO_FRIENDSHIP_OFFER_FOUND,
            [Description("friend does not allow mapping")] FRIEND_DOES_NOT_ALLOW_MAPPING,
            [Description("timeout mapping friend")] TIMEOUT_MAPPING_FRIEND,
            [Description("friend offline")] FRIEND_OFFLINE,
            [Description("timeout getting region")] TIMEOUT_GETTING_REGION,
            [Description("region not found")] REGION_NOT_FOUND,
            [Description("no map items found")] NO_MAP_ITEMS_FOUND,
            [Description("no description provided")] NO_DESCRIPTION_PROVIDED,
            [Description("no folder specified")] NO_FOLDER_SPECIFIED,
            [Description("empty wearables")] EMPTY_WEARABLES,
            [Description("parcel not for sale")] PARCEL_NOT_FOR_SALE,
            [Description("unknown access list type")] UNKNOWN_ACCESS_LIST_TYPE,
            [Description("no task specified")] NO_TASK_SPECIFIED,
            [Description("timeout getting group members")] TIMEOUT_GETTING_GROUP_MEMBERS,
            [Description("group not open")] GROUP_NOT_OPEN
        }

        /// <summary>
        ///     Keys reconigzed by Corrade.
        /// </summary>
        private enum ScriptKeys : uint
        {
            [Description("updateprimitiveinventory")] UPDATEPRIMITIVEINVENTORY,
            [Description("version")] VERSION,
            [Description("playsound")] PLAYSOUND,
            [Description("gain")] GAIN,
            [Description("getrolemembers")] GETROLEMEMBERS,
            [Description("status")] STATUS,
            [Description("getmembers")] GETMEMBERS,
            [Description("replytolure")] REPLYTOLURE,
            [Description("session")] SESSION,
            [Description("replytopermission")] REPLYTOPERMISSION,
            [Description("task")] TASK,
            [Description("getparcellist")] GETPARCELLIST,
            [Description("parcelrelease")] PARCELRELEASE,
            [Description("parcelbuy")] PARCELBUY,
            [Description("removecontribution")] REMOVECONTRIBUTION,
            [Description("forgroup")] FORGROUP,
            [Description("parceldeed")] PARCELDEED,
            [Description("parcelreclaim")] PARCELRECLAIM,
            [Description("unwear")] UNWEAR,
            [Description("wear")] WEAR,
            [Description("wearables")] WEARABLES,
            [Description("getwearables")] GETWEARABLES,
            [Description("wearfolder")] WEARFOLDER,
            [Description("folder")] FOLDER,
            [Description("replace")] REPLACE,
            [Description("setobjectrotation")] SETOBJECTROTATION,
            [Description("setobjectdescription")] SETOBJECTDESCRIPTION,
            [Description("setobjectname")] SETOBJECTNAME,
            [Description("setobjectposition")] SETOBJECTPOSITION,
            [Description("setobjectsaleinfo")] SETOBJECTSALEINFO,
            [Description("setobjectgroup")] SETOBJECTGROUP,
            [Description("objectdeed")] OBJECTDEED,
            [Description("setobjectpermissions")] SETOBJECTPERMISSIONS,
            [Description("who")] WHO,
            [Description("permissions")] PERMISSIONS,
            [Description("getavatarpositions")] GETAVATARPOSITIONS,
            [Description("getprimitives")] GETPRIMITIVES,
            [Description("delay")] DELAY,
            [Description("asset")] ASSET,
            [Description("setregiondebug")] SETREGIONDEBUG,
            [Description("scripts")] SCRIPTS,
            [Description("collisions")] COLLISIONS,
            [Description("physics")] PHYSICS,
            [Description("getmapavatarpositions")] GETMAPAVATARPOSITIONS,
            [Description("mapfriend")] MAPFRIEND,
            [Description("friendshiprequest")] FRIENDSHIPREQUEST,
            [Description("getfriendshiprequests")] GETFRIENDSHIPREQUESTS,
            [Description("grantfriendrights")] GRANTFRIENDRIGHTS,
            [Description("rights")] RIGHTS,
            [Description("getfriendslist")] GETFRIENDSLIST,
            [Description("terminatefriendship")] TERMINATEFRIENDSHIP,
            [Description("offerfriendship")] OFFERFRIENDSHIP,
            [Description("getfrienddata")] GETFRIENDDATA,
            [Description("days")] DAYS,
            [Description("interval")] INTERVAL,
            [Description("getgroupaccountsummarydata")] GETGROUPACCOUNTSUMMARYDATA,
            [Description("getselfdata")] GETSELFDATA,
            [Description("deleteclassified")] DELETECLASSIFIED,
            [Description("addclassified")] ADDCLASSIFIED,
            [Description("price")] PRICE,
            [Description("renew")] RENEW,
            [Description("logout")] LOGOUT,
            [Description("displayname")] DISPLAYNAME,
            [Description("returnprimitives")] RETURNPRIMITIVES,
            [Description("getgroupdata")] GETGROUPDATA,
            [Description("getavatardata")] GETAVATARDATA,
            [Description("getprimitiveinventory")] GETPRIMITIVEINVENTORY,
            [Description("getinventorydata")] GETINVENTORYDATA,
            [Description("getprimitiveinventorydata")] GETPRIMITIVEINVENTORYDATA,
            [Description("getscriptrunning")] GETSCRIPTRUNNING,
            [Description("setscriptrunning")] SETSCRIPTRUNNING,
            [Description("derez")] DEREZ,
            [Description("getparceldata")] GETPARCELDATA,
            [Description("rez")] REZ,
            [Description("rotation")] ROTATION,
            [Description("index")] INDEX,
            [Description("replytodialog")] REPLYTODIALOG,
            [Description("owner")] OWNER,
            [Description("button")] BUTTON,
            [Description("getanimations")] GETANIMATIONS,
            [Description("animation")] ANIMATION,
            [Description("setestatelist")] SETESTATELIST,
            [Description("getestatelist")] GETESTATELIST,
            [Description("all")] ALL,
            [Description("getregiontop")] GETREGIONTOP,
            [Description("restartregion")] RESTARTREGION,
            [Description("timeout")] TIMEOUT,
            [Description("directorysearch")] DIRECTORYSEARCH,
            [Description("getprofiledata")] GETPROFILEDATA,
            [Description("getparticlesystem")] GETPARTICLESYSTEM,
            [Description("data")] DATA,
            [Description("range")] RANGE,
            [Description("balance")] BALANCE,
            [Description("key")] KEY,
            [Description("value")] VALUE,
            [Description("database")] DATABASE,
            [Description("text")] TEXT,
            [Description("quorum")] QUORUM,
            [Description("majority")] MAJORITY,
            [Description("startproposal")] STARTPROPOSAL,
            [Description("duration")] DURATION,
            [Description("action")] ACTION,
            [Description("deletefromrole")] DELETEFROMROLE,
            [Description("addtorole")] ADDTOROLE,
            [Description("leave")] LEAVE,
            [Description("updategroupdata")] UPDATEGROUPDATA,
            [Description("eject")] EJECT,
            [Description("invite")] INVITE,
            [Description("join")] JOIN,
            [Description("callback")] CALLBACK,
            [Description("group")] GROUP,
            [Description("password")] PASSWORD,
            [Description("firstname")] FIRSTNAME,
            [Description("lastname")] LASTNAME,
            [Description("command")] COMMAND,
            [Description("role")] ROLE,
            [Description("title")] TITLE,
            [Description("tell")] TELL,
            [Description("notice")] NOTICE,
            [Description("message")] MESSAGE,
            [Description("subject")] SUBJECT,
            [Description("item")] ITEM,
            [Description("pay")] PAY,
            [Description("amount")] AMOUNT,
            [Description("target")] TARGET,
            [Description("reason")] REASON,
            [Description("getbalance")] GETBALANCE,
            [Description("teleport")] TELEPORT,
            [Description("region")] REGION,
            [Description("position")] POSITION,
            [Description("getregiondata")] GETREGIONDATA,
            [Description("sit")] SIT,
            [Description("stand")] STAND,
            [Description("ban")] BAN,
            [Description("parceleject")] PARCELEJECT,
            [Description("creategroup")] CREATEGROUP,
            [Description("parcelfreeze")] PARCELFREEZE,
            [Description("createrole")] CREATEROLE,
            [Description("deleterole")] DELETEROLE,
            [Description("getrolesmembers")] GETROLESMEMBERS,
            [Description("getroles")] GETROLES,
            [Description("getrolepowers")] GETROLEPOWERS,
            [Description("powers")] POWERS,
            [Description("lure")] LURE,
            [Description("parcelmusic")] PARCELMUSIC,
            [Description("URL")] URL,
            [Description("sethome")] SETHOME,
            [Description("gohome")] GOHOME,
            [Description("setprofiledata")] SETPROFILEDATA,
            [Description("give")] GIVE,
            [Description("deleteitem")] DELETEITEM,
            [Description("emptytrash")] EMPTYTRASH,
            [Description("fly")] FLY,
            [Description("addpick")] ADDPICK,
            [Description("deltepick")] DELETEPICK,
            [Description("touch")] TOUCH,
            [Description("moderate")] MODERATE,
            [Description("type")] TYPE,
            [Description("silence")] SILENCE,
            [Description("freeze")] FREEZE,
            [Description("rebake")] REBAKE,
            [Description("getattachments")] GETATTACHMENTS,
            [Description("attach")] ATTACH,
            [Description("attachments")] ATTACHMENTS,
            [Description("detach")] DETACH,
            [Description("getprimitiveowners")] GETPRIMITIVEOWNERS,
            [Description("entity")] ENTITY,
            [Description("channel")] CHANNEL,
            [Description("name")] NAME,
            [Description("description")] DESCRIPTION,
            [Description("getprimitivedata")] GETPRIMITIVEDATA,
            [Description("activate")] ACTIVATE,
            [Description("move")] MOVE,
            [Description("settitle")] SETTITLE,
            [Description("mute")] MUTE,
            [Description("getmutes")] GETMUTES,
            [Description("notify")] NOTIFY
        }

        /// <summary>
        ///     Various types.
        /// </summary>
        private enum Type : uint
        {
            [Description("text")] TEXT,
            [Description("voice")] VOICE,
            [Description("scripts")] SCRIPTS,
            [Description("colliders")] COLLIDERS,
            [Description("ban")] BAN,
            [Description("group")] GROUP,
            [Description("user")] USER,
            [Description("manager")] MANAGER,
            [Description("classified")] CLASSIFIED,
            [Description("event")] EVENT,
            [Description("land")] LAND,
            [Description("people")] PEOPLE,
            [Description("place")] PLACE
        }
    }

    public class NativeMethods
    {
        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        /// <summary>
        ///     Import console handler for windows.
        /// </summary>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool SetConsoleCtrlHandler(Corrade.EventHandler handler,
            [MarshalAs(UnmanagedType.U1)] bool add);
    }
}