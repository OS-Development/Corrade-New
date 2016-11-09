///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel.Syndication;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using BayesSharp;
using Corrade.Constants;
using Corrade.Events;
using Corrade.Helpers;
using Corrade.Structures;
using Corrade.Structures.Effects;
using CorradeConfiguration;
using LanguageDetection;
using OpenMetaverse;
using OpenMetaverse.Assets;
using Syn.Bot.Siml;
using Syn.Bot.Siml.Events;
using wasOpenMetaverse;
using wasSharp;
using wasSharpNET.Cryptography;
using static wasSharp.Time;
using static Corrade.Command;
using Group = OpenMetaverse.Group;
using GroupNotice = Corrade.Structures.GroupNotice;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;
using ThreadState = System.Threading.ThreadState;

#endregion

namespace Corrade
{
    public partial class Corrade : ServiceBase
    {
        public delegate bool EventHandler(NativeMethods.CtrlType ctrlType);

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

        /// <summary>
        ///     A map of notification name to notification.
        /// </summary>
        public static readonly Dictionary<string, Action<NotificationParameters, Dictionary<string, string>>>
            corradeNotifications = typeof(CorradeNotifications).GetFields(BindingFlags.Static |
                                                                          BindingFlags.Public)
                .AsParallel()
                .Where(
                    o =>
                        o.FieldType ==
                        typeof(Action<NotificationParameters, Dictionary<string, string>>))
                .ToDictionary(
                    o => string.Intern(o.Name), o =>
                        (Action<NotificationParameters, Dictionary<string, string>>) o.GetValue(null));

        /// <summary>
        ///     A map of Corrade command name to Corrade command.
        /// </summary>
        public static readonly Dictionary<string, Action<CorradeCommandParameters, Dictionary<string, string>>>
            corradeCommands = typeof(CorradeCommands).GetFields(BindingFlags.Static | BindingFlags.Public)
                .AsParallel()
                .Where(
                    o =>
                        o.FieldType ==
                        typeof(Action<CorradeCommandParameters, Dictionary<string, string>>))
                .ToDictionary(
                    o => string.Intern(o.Name), o =>
                        (Action<CorradeCommandParameters, Dictionary<string, string>>) o.GetValue(null));

        /// <summary>
        ///     Holds all the active RLV rules.
        /// </summary>
        public static readonly HashSet<wasOpenMetaverse.RLV.RLVRule> RLVRules =
            new HashSet<wasOpenMetaverse.RLV.RLVRule>();

        /// <summary>
        ///     A map of RLV behavior name to RLV behavior.
        /// </summary>
        public static readonly Dictionary<string, Action<string, wasOpenMetaverse.RLV.RLVRule, UUID>> rlvBehaviours = typeof
            (RLVBehaviours).GetFields(BindingFlags.Static | BindingFlags.Public)
            .AsParallel()
            .Where(
                o =>
                    o.FieldType ==
                    typeof(Action<string, wasOpenMetaverse.RLV.RLVRule, UUID>))
            .ToDictionary(
                o => string.Intern(o.Name), o =>
                    (Action<string, wasOpenMetaverse.RLV.RLVRule, UUID>) o.GetValue(null));

        public static string InstalledServiceName;
        private static Configuration corradeConfiguration = new Configuration();
        private static Thread programThread;
        private static Thread HTTPListenerThread;
        private static Thread TCPNotificationsThread;
        private static TcpListener TCPListener;
        private static HttpListener HTTPListener;
        private static readonly EventLog CorradeEventLog = new EventLog();
        private static readonly GridClient Client = new GridClient();
        private static LoginParams Login;
        private static int LoginLocationIndex;
        private static InventoryFolder CurrentOutfitFolder;
        private static readonly SimlBot SynBot = new SimlBot();
        private static readonly BotUser SynBotUser = SynBot.MainUser;
        private static LanguageDetector languageDetector = new LanguageDetector();
        private static readonly FileSystemWatcher SIMLBotConfigurationWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher ConfigurationWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher NotificationsWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher SchedulesWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher GroupFeedWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher GroupSoftBansWatcher = new FileSystemWatcher();
        private static readonly object SIMLBotLock = new object();
        private static readonly object ConfigurationFileLock = new object();
        private static readonly object ClientLogFileLock = new object();
        private static readonly object GroupLogFileLock = new object();
        private static readonly object LocalLogFileLock = new object();
        private static readonly object RegionLogFileLock = new object();
        private static readonly object InstantMessageLogFileLock = new object();
        private static readonly object ConferenceMessageLogFileLock = new object();

        private static readonly object GroupMembersStateFileLock = new object();
        private static readonly object GroupSoftBansStateFileLock = new object();
        private static readonly object GroupSchedulesStateFileLock = new object();
        private static readonly object GroupNotificationsStateFileLock = new object();
        private static readonly object MovementStateFileLock = new object();
        private static readonly object ConferencesStateFileLock = new object();
        private static readonly object GroupFeedsStateFileLock = new object();
        private static readonly object GroupCookiesStateFileLock = new object();

        private static readonly TimedThrottle TimedTeleportThrottle =
            new TimedThrottle(wasOpenMetaverse.Constants.TELEPORTS.THROTTLE.MAX_TELEPORTS,
                wasOpenMetaverse.Constants.TELEPORTS.THROTTLE.GRACE_SECONDS);

        private static readonly object GroupNotificationsLock = new object();

        private static HashSet<Notification> GroupNotifications =
            new HashSet<Notification>();

        private static readonly Dictionary<Configuration.Notifications, HashSet<Notification>> GroupNotificationsCache =
            new Dictionary<Configuration.Notifications, HashSet<Notification>>();

        private static readonly Dictionary<UUID, InventoryOffer> InventoryOffers = new Dictionary<UUID, InventoryOffer>();

        private static readonly object InventoryOffersLock = new object();

        private static readonly
            Collections.SerializableDictionary<string, Collections.SerializableDictionary<UUID, string>> GroupFeeds =
                new Collections.SerializableDictionary<string, Collections.SerializableDictionary<UUID, string>>();

        private static readonly object GroupFeedsLock = new object();

        private static readonly BlockingQueue<CallbackQueueElement> CallbackQueue =
            new BlockingQueue<CallbackQueueElement>();

        private static readonly BlockingQueue<NotificationQueueElement> NotificationQueue =
            new BlockingQueue<NotificationQueueElement>();

        private static readonly BlockingQueue<NotificationTCPQueueElement> NotificationTCPQueue =
            new BlockingQueue<NotificationTCPQueueElement>();

        private static readonly Dictionary<UUID, GroupInvite> GroupInvites = new Dictionary<UUID, GroupInvite>();
        private static readonly object GroupInvitesLock = new object();
        private static readonly HashSet<GroupNotice> GroupNotices = new HashSet<GroupNotice>();
        private static readonly object GroupNoticeLock = new object();
        private static readonly Dictionary<UUID, TeleportLure> TeleportLures = new Dictionary<UUID, TeleportLure>();
        private static readonly object TeleportLuresLock = new object();

        // permission requests can be identical
        private static readonly List<ScriptPermissionRequest> ScriptPermissionRequests =
            new List<ScriptPermissionRequest>();

        private static readonly object ScriptPermissionsRequestsLock = new object();

        // script dialogs can be identical
        private static readonly Dictionary<UUID, ScriptDialog> ScriptDialogs = new Dictionary<UUID, ScriptDialog>();
        private static readonly object ScriptDialogsLock = new object();

        private static readonly HashSet<KeyValuePair<UUID, int>> CurrentAnimations =
            new HashSet<KeyValuePair<UUID, int>>();

        private static readonly object CurrentAnimationsLock = new object();

        private static readonly Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<UUID>>
            GroupMembers =
                new Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<UUID>>();

        private static readonly object GroupMembersLock = new object();

        public static readonly Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<SoftBan>>
            GroupSoftBans =
                new Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<SoftBan>>();

        private static readonly object GroupSoftBansLock = new object();

        private static readonly Hashtable GroupWorkers = new Hashtable();
        private static readonly object GroupWorkersLock = new object();

        private static readonly Dictionary<UUID, InventoryBase> GroupDirectoryTrackers =
            new Dictionary<UUID, InventoryBase>();

        private static readonly object GroupDirectoryTrackersLock = new object();
        private static readonly HashSet<LookAtEffect> LookAtEffects = new HashSet<LookAtEffect>();

        private static readonly HashSet<PointAtEffect> PointAtEffects =
            new HashSet<PointAtEffect>();

        private static readonly HashSet<SphereEffect> SphereEffects = new HashSet<SphereEffect>();
        private static readonly object SphereEffectsLock = new object();
        private static readonly HashSet<BeamEffect> BeamEffects = new HashSet<BeamEffect>();
        private static readonly Dictionary<uint, Primitive> RadarObjects = new Dictionary<uint, Primitive>();
        private static readonly object LookAtEffectsLock = new object();
        private static readonly object PointAtEffectsLock = new object();
        private static readonly object RadarObjectsLock = new object();
        private static readonly object BeamEffectsLock = new object();
        private static readonly object InputFiltersLock = new object();
        private static readonly object OutputFiltersLock = new object();
        private static readonly HashSet<GroupSchedule> GroupSchedules = new HashSet<GroupSchedule>();
        private static readonly object GroupSchedulesLock = new object();
        private static readonly HashSet<Conference> Conferences = new HashSet<Conference>();
        private static readonly object ConferencesLock = new object();

        private static readonly Dictionary<UUID, CookieContainer> GroupCookieContainers =
            new Dictionary<UUID, CookieContainer>();

        private static readonly object GroupCookieContainersLock = new object();

        private static readonly Dictionary<UUID, Web.wasHTTPClient> GroupHTTPClients =
            new Dictionary<UUID, Web.wasHTTPClient>();

        private static readonly object GroupHTTPClientsLock = new object();

        private static readonly Dictionary<string, Web.wasHTTPClient> HordeHTTPClients =
            new Dictionary<string, Web.wasHTTPClient>();

        private static readonly object HordeHTTPClientsLock = new object();

        private static readonly Dictionary<UUID, BayesSimpleTextClassifier> GroupBayesClassifiers =
            new Dictionary<UUID, BayesSimpleTextClassifier>();

        private static readonly object GroupBayesClassifiersLock = new object();

        private static string CorradePOSTMediaType;

        private static readonly AES CorradeAES = new AES();

        private static readonly object RLVInventoryLock = new object();

        public static readonly Heartbeat CorradeHeartbeat = new Heartbeat();

        /// <summary>
        ///     Heartbeat timer.
        /// </summary>
        private static readonly Time.Timer CorradeHeartBeatTimer = new Time.Timer(o =>
        {
            // Send notification.
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Heartbeat, new HeartbeatEventArgs
                {
                    ExecutingCommands = CorradeHeartbeat.ExecutingCommands,
                    ExecutingRLVBehaviours = CorradeHeartbeat.ExecutingRLVBehaviours,
                    ProcessedCommands = CorradeHeartbeat.ProcessedCommands,
                    ProcessedRLVBehaviours = CorradeHeartbeat.ProcessedRLVBehaviours,
                    AverageCPUUsage = CorradeHeartbeat.AverageCPUUsage,
                    AverageRAMUsage = CorradeHeartbeat.AverageRAMUsage,
                    Heartbeats = CorradeHeartbeat.Heartbeats,
                    Uptime = CorradeHeartbeat.Uptime,
                    Version = CorradeHeartbeat.Version
                }),
                corradeConfiguration.MaximumNotificationThreads);
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        /// <summary>
        ///     Heartbeat logging.
        /// </summary>
        private static readonly Time.Timer CorradeHeartBeatLogTimer = new Time.Timer(o =>
        {
            // Log heartbeat data.
            Feedback("Heartbeat",
                string.Format("CPU: {0}% RAM: {1:0.}MiB Uptime: {2}d:{3}h:{4}m Commands: {5} Behaviours: {6}",
                    CorradeHeartbeat.AverageCPUUsage,
                    CorradeHeartbeat.AverageRAMUsage/1024/1024, TimeSpan.FromMinutes(CorradeHeartbeat.Uptime).Days,
                    TimeSpan.FromMinutes(CorradeHeartbeat.Uptime).Hours,
                    TimeSpan.FromMinutes(CorradeHeartbeat.Uptime).Minutes, CorradeHeartbeat.ProcessedCommands,
                    CorradeHeartbeat.ProcessedRLVBehaviours));
        }, null, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        ///     Effects expiration timer.
        /// </summary>
        private static readonly Time.Timer EffectsExpirationTimer = new Time.Timer(callback =>
        {
            lock (SphereEffectsLock)
            {
                SphereEffects.RemoveWhere(o => DateTime.Compare(DateTime.Now, o.Termination) > 0);
            }
            lock (BeamEffectsLock)
            {
                BeamEffects.RemoveWhere(o => DateTime.Compare(DateTime.Now, o.Termination) > 0);
            }
        }, null, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        ///     Group membership timer.
        /// </summary>
        private static readonly Time.Timer GroupMembershipTimer = new Time.Timer(callback =>
        {
            lock (Locks.ClientInstanceNetworkLock)
            {
                if (!Client.Network.Connected ||
                    (Client.Network.CurrentSim != null && !Client.Network.CurrentSim.Caps.IsEventQueueRunning))
                    return;
            }

            // Expire any hard soft bans.
            lock (GroupSoftBansLock)
            {
                GroupSoftBans.AsParallel()
                    // Select only the groups to which we have the capability of changing the group access list.
                    .Where(o => Services.HasGroupPowers(Client, Client.Self.AgentID, o.Key,
                        GroupPowers.GroupBanAccess,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                    // Select group and all the soft bans that have expired.
                    .Select(o => new
                    {
                        Group = o.Key,
                        SoftBans = o.Value.AsParallel().Where(p =>
                        {
                            // Only process softbans with a set hard-ban time.
                            if (p.Time.Equals(0))
                                return false;
                            // Get the softban timestamp and covert to datetime.
                            DateTime lastBanDate;
                            if (
                                !DateTime.TryParseExact(p.Last, CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out lastBanDate))
                                return false;
                            // If the current time exceeds the hard-ban time then select the softban for processing.
                            return DateTime.Compare(lastBanDate.AddMinutes(p.Time), DateTime.UtcNow) < 0;
                        })
                    })

                    // Only select groups with non-empty soft bans matching previous criteria.
                    .Where(o => o.SoftBans.Any())
                    // Select only soft bans that are also group bans.
                    .Select(o =>
                    {
                        // Get current group bans.
                        var agents = new HashSet<UUID>();
                        Dictionary<UUID, DateTime> bannedAgents = null;
                        if (Services.GetGroupBans(Client, o.Group, corradeConfiguration.ServicesTimeout,
                            ref bannedAgents) && bannedAgents != null)
                        {
                            agents.UnionWith(bannedAgents.Keys);
                        }
                        return new
                        {
                            o.Group,
                            SoftBans = o.SoftBans.Where(p => agents.Contains(p.Agent))
                        };
                    })
                    // Unban all the agents with expired soft bans that are also group bans.
                    .ForAll(o =>
                    {
                        lock (Locks.ClientInstanceGroupsLock)
                        {
                            var GroupBanEvent = new ManualResetEvent(false);
                            Client.Groups.RequestBanAction(o.Group,
                                GroupBanAction.Unban, o.SoftBans.Select(p => p.Agent).ToArray(),
                                (sender, args) => { GroupBanEvent.Set(); });
                            if (!GroupBanEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_LIFT_HARD_SOFT_BAN),
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST));
                            }
                        }
                    });
            }

            // Get current groups.
            var groups = Enumerable.Empty<UUID>();
            if (!Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout, ref groups))
                return;
            var currentGroups = new HashSet<UUID>(groups);
            // Remove groups that are not configured.
            currentGroups.RemoveWhere(o => !corradeConfiguration.Groups.Any(p => p.UUID.Equals(o)));

            // Bail if no configured groups are also joined.
            if (!currentGroups.Any())
                return;

            var membersGroups = new HashSet<UUID>();
            lock (GroupMembersLock)
            {
                membersGroups.UnionWith(GroupMembers.Keys);
            }
            // Remove groups no longer handled.
            membersGroups.AsParallel().ForAll(o =>
            {
                if (!currentGroups.Contains(o))
                {
                    lock (GroupMembersLock)
                    {
                        GroupMembers[o].CollectionChanged -= HandleGroupMemberJoinPart;
                        GroupMembers.Remove(o);
                    }
                }
            });
            // Add new groups to be handled.
            currentGroups.AsParallel().ForAll(o =>
            {
                lock (GroupMembersLock)
                {
                    if (!GroupMembers.ContainsKey(o))
                    {
                        GroupMembers.Add(o, new Collections.ObservableHashSet<UUID>());
                        GroupMembers[o].CollectionChanged += HandleGroupMemberJoinPart;
                    }
                }
            });

            var LockObject = new object();
            var groupMembersRequestUUIDs = new HashSet<UUID>();
            var GroupMembersReplyEvent = new AutoResetEvent(false);
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                lock (LockObject)
                {
                    switch (groupMembersRequestUUIDs.Contains(args.RequestID))
                    {
                        case true:
                            groupMembersRequestUUIDs.Remove(args.RequestID);
                            break;
                        default:
                            return;
                    }
                }

                lock (GroupMembersLock)
                {
                    if (GroupMembers.ContainsKey(args.GroupID))
                    {
                        switch (!GroupMembers[args.GroupID].Any())
                        {
                            case true:
                                GroupMembers[args.GroupID].UnionWith(args.Members.Values.Select(o => o.ID));
                                break;
                            default:
                                GroupMembers[args.GroupID].ExceptWith(GroupMembers[args.GroupID].AsParallel()
                                    .Where(o => !args.Members.Values.Any(p => p.ID.Equals(o))));
                                GroupMembers[args.GroupID].UnionWith(args.Members.Values.AsParallel()
                                    .Where(o => !GroupMembers[args.GroupID].Contains(o.ID))
                                    .Select(o => o.ID));
                                break;
                        }
                    }
                }
                GroupMembersReplyEvent.Set();
            };

            currentGroups.AsParallel().ForAll(o =>
            {
                Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                lock (LockObject)
                {
                    groupMembersRequestUUIDs.Add(Client.Groups.RequestGroupMembers(o));
                }
                GroupMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false);
                Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
            });
        }, null, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        ///     Group feeds timer.
        /// </summary>
        private static readonly Time.Timer GroupFeedsTimer = new Time.Timer(callback =>
        {
            lock (GroupFeedsLock)
            {
                GroupFeeds.AsParallel().ForAll(o =>
                {
                    try
                    {
                        using (var reader = XmlReader.Create(o.Key))
                        {
                            var syndicationFeed = SyndicationFeed.Load(reader);
                            syndicationFeed?.Items.AsParallel()
                                .Where(
                                    p =>
                                        p != null && p.Title != null && p.Summary != null &&
                                        p.PublishDate.CompareTo(
                                            DateTimeOffset.Now.Subtract(
                                                TimeSpan.FromMilliseconds(corradeConfiguration.FeedsUpdateInterval))) >
                                        0)
                                .ForAll(p =>
                                {
                                    o.Value.AsParallel().ForAll(q =>
                                    {
                                        CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.Feed,
                                                new FeedEventArgs
                                                {
                                                    Title = p.Title.Text,
                                                    Summary = p.Summary.Text,
                                                    Date = p.PublishDate,
                                                    Name = q.Value,
                                                    GroupUUID = q.Key
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                    });
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.ERROR_LOADING_FEED),
                            o.Key,
                            ex.Message);
                    }
                });
            }
        }, null, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        ///     Group schedules timer.
        /// </summary>
        private static readonly Time.Timer GroupSchedulesTimer = new Time.Timer(callback =>
        {
            var groupSchedules = new HashSet<GroupSchedule>();
            lock (GroupSchedulesLock)
            {
                groupSchedules.UnionWith(GroupSchedules.AsParallel()
                    .Where(
                        o =>
                            DateTime.Compare(DateTime.Now.ToUniversalTime(),
                                o.At) >= 0));
            }
            if (groupSchedules.Any())
            {
                groupSchedules.AsParallel().ForAll(
                    o =>
                    {
                        // Spawn the command.
                        CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(
                            () => HandleCorradeCommand(o.Message, o.Sender, o.Identifier, o.Group),
                            corradeConfiguration.MaximumCommandThreads, o.Group.UUID,
                            corradeConfiguration.SchedulerExpiration);
                        lock (GroupSchedulesLock)
                        {
                            GroupSchedules.Remove(o);
                        }
                    });
                SaveGroupSchedulesState.Invoke();
            }
        }, null, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        ///     The various types of threads created by Corrade.
        /// </summary>
        private static readonly Dictionary<Threading.Enumerations.ThreadType, Threading.Thread> CorradeThreadPool =
            new Dictionary<Threading.Enumerations.ThreadType, Threading.Thread>
            {
                {
                    Threading.Enumerations.ThreadType.COMMAND,
                    new Threading.Thread(Threading.Enumerations.ThreadType.COMMAND)
                },
                {Threading.Enumerations.ThreadType.RLV, new Threading.Thread(Threading.Enumerations.ThreadType.RLV)},
                {
                    Threading.Enumerations.ThreadType.NOTIFICATION,
                    new Threading.Thread(Threading.Enumerations.ThreadType.NOTIFICATION)
                },
                {
                    Threading.Enumerations.ThreadType.INSTANT_MESSAGE,
                    new Threading.Thread(Threading.Enumerations.ThreadType.INSTANT_MESSAGE)
                },
                {Threading.Enumerations.ThreadType.LOG, new Threading.Thread(Threading.Enumerations.ThreadType.LOG)},
                {Threading.Enumerations.ThreadType.POST, new Threading.Thread(Threading.Enumerations.ThreadType.POST)}
            };

        /// <summary>
        ///     Schedules a load of the configuration file.
        /// </summary>
        private static readonly Time.Timer ConfigurationChangedTimer =
            new Time.Timer(ConfigurationChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.CONFIGURATION_FILE_MODIFIED));
                lock (ConfigurationFileLock)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.READING_CORRADE_CONFIGURATION));
                    try
                    {
                        using (var stream = new StreamReader(CORRADE_CONSTANTS.CONFIGURATION_FILE, Encoding.UTF8))
                        {
                            var serializer =
                                new XmlSerializer(typeof(Configuration));
                            var loadedConfiguration = (Configuration) serializer.Deserialize(stream);
                            if (corradeConfiguration.EnableHorde)
                            {
                                corradeConfiguration.Groups.AsParallel()
                                    .Where(o => !loadedConfiguration.Groups.Any(p => p.UUID.Equals(o.UUID)))
                                    .ForAll(
                                        o =>
                                        {
                                            HordeDistributeConfigurationGroup(o,
                                                Configuration.HordeDataSynchronizationOption.Remove);
                                        });
                                loadedConfiguration.Groups.AsParallel()
                                    .Where(o => !corradeConfiguration.Groups.Any(p => p.UUID.Equals(o.UUID)))
                                    .Select(o => o).ForAll(o =>
                                    {
                                        HordeDistributeConfigurationGroup(o,
                                            Configuration.HordeDataSynchronizationOption.Add);
                                    });
                            }
                            corradeConfiguration = loadedConfiguration;
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_CONFIGURATION),
                            ex.Message);
                        return;
                    }

                    // Check configuration file compatiblity.
                    Version minimalConfig;
                    Version versionConfig;
                    if (
                        !Version.TryParse(CORRADE_CONSTANTS.ASSEMBLY_CUSTOM_ATTRIBUTES["configuration"],
                            out minimalConfig) ||
                        !Version.TryParse(corradeConfiguration.Version, out versionConfig) ||
                        !minimalConfig.Major.Equals(versionConfig.Major) ||
                        !minimalConfig.Minor.Equals(versionConfig.Minor))
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.CONFIGURATION_FILE_VERSION_MISMATCH));

                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.READ_CORRADE_CONFIGURATION));
                }
                if (!corradeConfiguration.Equals(default(Configuration)))
                {
                    UpdateDynamicConfiguration(corradeConfiguration);
                }
            });

        /// <summary>
        ///     Schedules a load of the notifications file.
        /// </summary>
        private static readonly Time.Timer NotificationsChangedTimer =
            new Time.Timer(NotificationsChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.NOTIFICATIONS_FILE_MODIFIED));
                LoadNotificationState.Invoke();
            });

        /// <summary>
        ///     Schedules a load of the SIML configuration file.
        /// </summary>
        private static readonly Time.Timer SIMLConfigurationChangedTimer =
            new Time.Timer(SIMLConfigurationChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.SIML_CONFIGURATION_MODIFIED));
                new Thread(
                    () =>
                    {
                        lock (SIMLBotLock)
                        {
                            LoadChatBotFiles.Invoke();
                        }
                    })
                {IsBackground = true}.Start();
            });

        /// <summary>
        ///     Schedules a load of the group schedules file.
        /// </summary>
        private static readonly Time.Timer GroupSchedulesChangedTimer =
            new Time.Timer(GroupSchedulesChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.GROUP_SCHEDULES_FILE_MODIFIED));
                LoadGroupSchedulesState.Invoke();
            });

        /// <summary>
        ///     Schedules a load of the group feeds file.
        /// </summary>
        private static readonly Time.Timer GroupFeedsChangedTimer =
            new Time.Timer(GroupFeedsChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.GROUP_FEEDS_FILE_MODIFIED));
                LoadGroupFeedState.Invoke();
            });

        /// <summary>
        ///     Schedules a load of the group soft bans file.
        /// </summary>
        private static readonly Time.Timer GroupSoftBansChangedTimer =
            new Time.Timer(GroupSoftBansChanged =>
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.GROUP_SOFT_BANS_FILE_MODIFIED));
                LoadGroupSoftBansState.Invoke();
            });

        /// <summary>
        ///     Global rebake timer.
        /// </summary>
        private static readonly Time.Timer RebakeTimer = new Time.Timer(Rebake =>
        {
            lock (Locks.ClientInstanceAppearanceLock)
            {
                var AppearanceSetEvent = new ManualResetEvent(false);
                EventHandler<AppearanceSetEventArgs> HandleAppearanceSet =
                    (sender, args) => { AppearanceSetEvent.Set(); };
                Client.Appearance.AppearanceSet += HandleAppearanceSet;
                Client.Appearance.RequestSetAppearance(true);
                AppearanceSetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false);
                Client.Appearance.AppearanceSet -= HandleAppearanceSet;
            }
        });

        /// <summary>
        ///     Current land group activation timer.
        /// </summary>
        private static readonly Time.Timer ActivateCurrentLandGroupTimer =
            new Time.Timer(ActivateCurrentLandGroup =>
            {
                Parcel parcel = null;
                if (
                    !Services.GetParcelAtPosition(Client, Client.Network.CurrentSim, Client.Self.SimPosition,
                        corradeConfiguration.ServicesTimeout, ref parcel)) return;
                var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                if (!groups.Contains(parcel.GroupID)) return;
                Client.Groups.ActivateGroup(parcel.GroupID);
            });

        public static EventHandler ConsoleEventHandler;

        /// <summary>
        ///     Corrade's input filter function.
        /// </summary>
        private static readonly Func<string, string> wasInput = o =>
        {
            if (string.IsNullOrEmpty(o)) return string.Empty;

            List<Configuration.Filter> safeFilters;
            lock (InputFiltersLock)
            {
                safeFilters = corradeConfiguration.InputFilters;
            }
            foreach (var filter in safeFilters)
            {
                switch (filter)
                {
                    case Configuration.Filter.RFC1738:
                        o = Web.URLUnescapeDataString(o);
                        break;
                    case Configuration.Filter.RFC3986:
                        o = Web.URIUnescapeDataString(o);
                        break;
                    case Configuration.Filter.ENIGMA:
                        o = Cryptography.ENIGMA(o, corradeConfiguration.ENIGMAConfiguration.rotors.ToArray(),
                            corradeConfiguration.ENIGMAConfiguration.plugs.ToArray(),
                            corradeConfiguration.ENIGMAConfiguration.reflector);
                        break;
                    case Configuration.Filter.VIGENERE:
                        o = Cryptography.DecryptVIGENERE(o, corradeConfiguration.VIGENERESecret);
                        break;
                    case Configuration.Filter.ATBASH:
                        o = Cryptography.ATBASH(o);
                        break;
                    case Configuration.Filter.AES:
                        o = CorradeAES.wasAESDecrypt(o, corradeConfiguration.AESKey);
                        break;
                    case Configuration.Filter.BASE64:
                        o = Encoding.UTF8.GetString(Convert.FromBase64String(o));
                        break;
                }
            }
            return o;
        };

        /// <summary>
        ///     Corrade's output filter function.
        /// </summary>
        private static readonly Func<string, string> wasOutput = o =>
        {
            if (string.IsNullOrEmpty(o)) return string.Empty;

            List<Configuration.Filter> safeFilters;
            lock (OutputFiltersLock)
            {
                safeFilters = corradeConfiguration.OutputFilters;
            }
            foreach (var filter in safeFilters)
            {
                switch (filter)
                {
                    case Configuration.Filter.RFC1738:
                        o = Web.URLEscapeDataString(o);
                        break;
                    case Configuration.Filter.RFC3986:
                        o = Web.URIEscapeDataString(o);
                        break;
                    case Configuration.Filter.ENIGMA:
                        o = Cryptography.ENIGMA(o, corradeConfiguration.ENIGMAConfiguration.rotors.ToArray(),
                            corradeConfiguration.ENIGMAConfiguration.plugs.ToArray(),
                            corradeConfiguration.ENIGMAConfiguration.reflector);
                        break;
                    case Configuration.Filter.VIGENERE:
                        o = Cryptography.EncryptVIGENERE(o, corradeConfiguration.VIGENERESecret);
                        break;
                    case Configuration.Filter.ATBASH:
                        o = Cryptography.ATBASH(o);
                        break;
                    case Configuration.Filter.AES:
                        o = CorradeAES.wasAESEncrypt(o, corradeConfiguration.AESKey);
                        break;
                    case Configuration.Filter.BASE64:
                        o = Convert.ToBase64String(Encoding.UTF8.GetBytes(o));
                        break;
                }
            }
            return o;
        };


        /// <summary>
        ///     Loads the OpenMetaverse inventory cache.
        /// </summary>
        private static readonly Action LoadInventoryCache = () =>
        {
            int itemsLoaded;
            lock (Locks.ClientInstanceInventoryLock)
            {
                itemsLoaded = Client.Inventory.Store.RestoreFromDisk(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                    CORRADE_CONSTANTS.INVENTORY_CACHE_FILE));
            }

            Feedback(
                Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.INVENTORY_CACHE_ITEMS_LOADED),
                itemsLoaded < 0 ? "0" : itemsLoaded.ToString(Utils.EnUsCulture));
        };

        /// <summary>
        ///     Saves the OpenMetaverse inventory cache.
        /// </summary>
        private static readonly Action SaveInventoryCache = () =>
        {
            var path = Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                CORRADE_CONSTANTS.INVENTORY_CACHE_FILE);
            int itemsSaved;
            lock (Locks.ClientInstanceInventoryLock)
            {
                itemsSaved = Client.Inventory.Store.Items.Count;
                Client.Inventory.Store.SaveToDisk(path);
            }

            Feedback(
                Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.INVENTORY_CACHE_ITEMS_SAVED),
                itemsSaved.ToString(Utils.EnUsCulture));
        };

        /// <summary>
        ///     Loads Corrade's caches.
        /// </summary>
        private static readonly Action LoadCorradeCache = () =>
        {
            new Thread(() =>
            {
                try
                {
                    Cache.AgentCache =
                        Cache.Load(
                            Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.AGENT_CACHE_FILE),
                            Cache.AgentCache);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_CACHE),
                        ex.Message);
                }
            })
            {IsBackground = true}.Start();

            new Thread(() =>
            {
                try
                {
                    Cache.GroupCache =
                        Cache.Load(
                            Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.GROUP_CACHE_FILE),
                            Cache.GroupCache);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_CACHE),
                        ex.Message);
                }
            })
            {IsBackground = true}.Start();

            new Thread(() =>
            {
                try
                {
                    Cache.RegionCache =
                        Cache.Load(
                            Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.REGION_CACHE_FILE),
                            Cache.RegionCache);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_CACHE),
                        ex.Message);
                }
            })
            {IsBackground = true}.Start();
        };

        /// <summary>
        ///     Saves Corrade's caches.
        /// </summary>
        private static readonly Action SaveCorradeCache = () =>
        {
            new Thread(() =>
            {
                try
                {
                    Cache.Save(
                        Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.AGENT_CACHE_FILE),
                        Cache.AgentCache);
                }
                catch (Exception e)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_CACHE),
                        e.Message);
                }
            })
            {IsBackground = true}.Start();

            new Thread(() =>
            {
                try
                {
                    Cache.Save(
                        Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.GROUP_CACHE_FILE),
                        Cache.GroupCache);
                }
                catch (Exception e)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_CACHE),
                        e.Message);
                }
            })
            {IsBackground = true}.Start();

            new Thread(() =>
            {
                try
                {
                    Cache.Save(
                        Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.REGION_CACHE_FILE),
                        Cache.RegionCache);
                }
                catch (Exception e)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_CACHE),
                        e.Message);
                }
            })
            {IsBackground = true}.Start();
        };

        /// <summary>
        ///     Saves Corrade group members.
        /// </summary>
        private static readonly Action SaveGroupBayesClassificiations = () =>
        {
            corradeConfiguration.Groups.AsParallel().ForAll(group =>
            {
                try
                {
                    lock (GroupBayesClassifiersLock)
                    {
                        if (!GroupBayesClassifiers.ContainsKey(group.UUID))
                            return;

                        GroupBayesClassifiers[group.UUID].Save(Path.Combine(CORRADE_CONSTANTS.BAYES_DIRECTORY,
                            group.UUID.ToString()) + @"." + CORRADE_CONSTANTS.BAYES_CLASSIFICATION_EXTENSION);
                    }
                }
                catch (Exception e)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_SAVE_GROUP_BAYES_DATA),
                        e.Message);
                }
            });
        };

        /// <summary>
        ///     Loads Corrade group members.
        /// </summary>
        private static readonly Action LoadGroupBayesClassificiations = () =>
        {
            corradeConfiguration.Groups.AsParallel().ForAll(group =>
            {
                lock (GroupBayesClassifiersLock)
                {
                    if (!GroupBayesClassifiers.ContainsKey(group.UUID))
                    {
                        GroupBayesClassifiers.Add(group.UUID, new BayesSimpleTextClassifier());
                    }
                }
                var groupBayesDataFile = Path.Combine(CORRADE_CONSTANTS.BAYES_DIRECTORY, group.UUID.ToString()) + @"." +
                                         CORRADE_CONSTANTS.BAYES_CLASSIFICATION_EXTENSION;
                if (!File.Exists(groupBayesDataFile))
                    return;
                try
                {
                    lock (GroupBayesClassifiersLock)
                    {
                        GroupBayesClassifiers[group.UUID].Load(groupBayesDataFile);
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_GROUP_BAYES_DATA),
                        ex.Message);
                }
            });
        };

        /// <summary>
        ///     Saves Corrade group members.
        /// </summary>
        private static readonly Action SaveGroupMembersState = () =>
        {
            try
            {
                lock (GroupMembersStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_MEMBERS_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer =
                                new XmlSerializer(
                                    typeof(Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<UUID>>
                                        ));
                            lock (GroupMembersLock)
                            {
                                serializer.Serialize(writer, GroupMembers);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_GROUP_MEMBERS_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Loads Corrade group members.
        /// </summary>
        private static readonly Action LoadGroupMembersState = () =>
        {
            var groupMembersStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_MEMBERS_STATE_FILE);
            if (File.Exists(groupMembersStateFile))
            {
                try
                {
                    lock (GroupMembersStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(groupMembersStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                                ((Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<UUID>>)
                                    new XmlSerializer(
                                        typeof(Collections.SerializableDictionary
                                            <UUID, Collections.ObservableHashSet<UUID>>))
                                        .Deserialize(streamReader))
                                    .AsParallel()
                                    .Where(
                                        o => groups.Contains(o.Key))
                                    .ForAll(o =>
                                    {
                                        lock (GroupMembersLock)
                                        {
                                            switch (!GroupMembers.ContainsKey(o.Key))
                                            {
                                                case true:
                                                    GroupMembers.Add(o.Key, new Collections.ObservableHashSet<UUID>());
                                                    GroupMembers[o.Key].CollectionChanged += HandleGroupMemberJoinPart;
                                                    GroupMembers[o.Key].UnionWith(o.Value);
                                                    break;
                                                default:
                                                    GroupMembers[o.Key].UnionWith(o.Value);
                                                    break;
                                            }
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_GROUP_MEMBERS_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade group soft bans.
        /// </summary>
        private static readonly Action SaveGroupSoftBansState = () =>
        {
            GroupSoftBansWatcher.EnableRaisingEvents = false;
            try
            {
                lock (GroupSoftBansStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_SOFT_BAN_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer =
                                new XmlSerializer(
                                    typeof(
                                        Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<SoftBan>>
                                        ));
                            lock (GroupSoftBansLock)
                            {
                                serializer.Serialize(writer, GroupSoftBans);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_GROUP_SOFT_BAN_STATE),
                    e.Message);
            }
            GroupSoftBansWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade group soft bans.
        /// </summary>
        private static readonly Action LoadGroupSoftBansState = () =>
        {
            var groupSoftBansStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_SOFT_BAN_STATE_FILE);
            if (File.Exists(groupSoftBansStateFile))
            {
                try
                {
                    lock (GroupSoftBansStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(groupSoftBansStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                                ((Collections.SerializableDictionary<UUID, Collections.ObservableHashSet<SoftBan>>)
                                    new XmlSerializer(
                                        typeof(Collections.SerializableDictionary
                                            <UUID, Collections.ObservableHashSet<SoftBan>>))
                                        .Deserialize(streamReader))
                                    .AsParallel()
                                    .Where(
                                        o => groups.Contains(o.Key))
                                    .ForAll(o =>
                                    {
                                        lock (GroupSoftBansLock)
                                        {
                                            switch (!GroupSoftBans.ContainsKey(o.Key))
                                            {
                                                case true:
                                                    GroupSoftBans.Add(o.Key,
                                                        new Collections.ObservableHashSet<SoftBan>());
                                                    GroupSoftBans[o.Key].CollectionChanged += HandleGroupSoftBansChanged;
                                                    GroupSoftBans[o.Key].UnionWith(o.Value);
                                                    break;
                                                default:
                                                    GroupSoftBans[o.Key].UnionWith(o.Value);
                                                    break;
                                            }
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_GROUP_SOFT_BAN_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade notifications.
        /// </summary>
        private static readonly Action SaveGroupSchedulesState = () =>
        {
            SchedulesWatcher.EnableRaisingEvents = false;
            try
            {
                lock (GroupSchedulesStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer = new XmlSerializer(typeof(HashSet<GroupSchedule>));
                            lock (GroupSchedulesLock)
                            {
                                serializer.Serialize(writer, GroupSchedules);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_GROUP_SCHEDULES_STATE),
                    e.Message);
            }
            SchedulesWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly Action LoadGroupSchedulesState = () =>
        {
            SchedulesWatcher.EnableRaisingEvents = false;
            var groupSchedulesStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE);
            if (File.Exists(groupSchedulesStateFile))
            {
                try
                {
                    lock (GroupSchedulesStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(groupSchedulesStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var groups =
                                    new HashSet<UUID>(
                                        corradeConfiguration.Groups
                                            .AsParallel()
                                            .Where(
                                                o =>
                                                    !o.Schedules.Equals(0) &&
                                                    o.PermissionMask.IsMaskFlagSet(Configuration.Permissions.Schedule))
                                            .Select(o => o.UUID));
                                ((HashSet<GroupSchedule>)
                                    new XmlSerializer(typeof(HashSet<GroupSchedule>)).Deserialize(streamReader))
                                    .AsParallel()
                                    .Where(o => groups.Contains(o.Group.UUID)).ForAll(o =>
                                    {
                                        lock (GroupSchedulesLock)
                                        {
                                            if (!GroupSchedules.Contains(o))
                                            {
                                                GroupSchedules.Add(o);
                                            }
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_GROUP_SCHEDULES_STATE),
                        ex.Message);
                }
            }
            SchedulesWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Saves Corrade notifications.
        /// </summary>
        private static readonly Action SaveNotificationState = () =>
        {
            NotificationsWatcher.EnableRaisingEvents = false;
            try
            {
                lock (GroupNotificationsStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer = new XmlSerializer(typeof(HashSet<Notification>));
                            lock (GroupNotificationsLock)
                            {
                                serializer.Serialize(writer, GroupNotifications);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_NOTIFICATIONS_STATE),
                    e.Message);
            }
            NotificationsWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly Action LoadNotificationState = () =>
        {
            NotificationsWatcher.EnableRaisingEvents = false;
            var groupNotificationsStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE);
            if (File.Exists(groupNotificationsStateFile))
            {
                var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                try
                {
                    lock (GroupNotificationsStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(groupNotificationsStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                ((HashSet<Notification>)
                                    new XmlSerializer(typeof(HashSet<Notification>)).Deserialize(streamReader))
                                    .AsParallel()
                                    .Where(
                                        o => groups.Contains(o.GroupUUID))
                                    .ForAll(o =>
                                    {
                                        lock (GroupNotificationsLock)
                                        {
                                            if (!GroupNotifications.Contains(o))
                                            {
                                                GroupNotifications.Add(o);
                                            }
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_NOTIFICATIONS_STATE),
                        ex.Message);
                }

                // Build the group notification cache.
                var LockObject = new object();
                new List<Configuration.Notifications>(Reflection.GetEnumValues<Configuration.Notifications>())
                    .AsParallel().ForAll(o =>
                    {
                        lock (GroupNotificationsLock)
                        {
                            GroupNotifications.AsParallel()
                                .Where(p => p.NotificationMask.IsMaskFlagSet(o)).ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        if (GroupNotificationsCache.ContainsKey(o))
                                        {
                                            GroupNotificationsCache[o].Add(p);
                                            return;
                                        }
                                        GroupNotificationsCache.Add(o, new HashSet<Notification> {p});
                                    }
                                });
                        }
                    });
            }
            NotificationsWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Saves Corrade movement state.
        /// </summary>
        private static readonly Action SaveMovementState = () =>
        {
            try
            {
                lock (MovementStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.MOVEMENT_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer = new XmlSerializer(typeof(AgentMovement));

                            lock (Locks.ClientInstanceSelfLock)
                            {
                                serializer.Serialize(writer, new AgentMovement
                                {
                                    AlwaysRun = Client.Self.Movement.AlwaysRun,
                                    AutoResetControls = Client.Self.Movement.AutoResetControls,
                                    Away = Client.Self.Movement.Away,
                                    BodyRotation = Client.Self.Movement.BodyRotation,
                                    Flags = Client.Self.Movement.Flags,
                                    Fly = Client.Self.Movement.Fly,
                                    HeadRotation = Client.Self.Movement.HeadRotation,
                                    Mouselook = Client.Self.Movement.Mouselook,
                                    SitOnGround = Client.Self.Movement.SitOnGround,
                                    StandUp = Client.Self.Movement.StandUp,
                                    State = Client.Self.Movement.State
                                });
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_MOVEMENT_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Loads Corrade movement state.
        /// </summary>
        private static readonly Action LoadMovementState = () =>
        {
            var movementStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.MOVEMENT_STATE_FILE);
            if (File.Exists(movementStateFile))
            {
                try
                {
                    lock (MovementStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(movementStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var serializer = new XmlSerializer(typeof(AgentMovement));
                                var movement = (AgentMovement) serializer.Deserialize(streamReader);
                                lock (Locks.ClientInstanceSelfLock)
                                {
                                    Client.Self.Movement.AlwaysRun = movement.AlwaysRun;
                                    Client.Self.Movement.AutoResetControls = movement.AutoResetControls;
                                    Client.Self.Movement.Away = movement.Away;
                                    Client.Self.Movement.BodyRotation = movement.BodyRotation;
                                    Client.Self.Movement.Flags = movement.Flags;
                                    Client.Self.Movement.Fly = movement.Fly;
                                    Client.Self.Movement.HeadRotation = movement.HeadRotation;
                                    Client.Self.Movement.Mouselook = movement.Mouselook;
                                    Client.Self.Movement.SitOnGround = movement.SitOnGround;
                                    Client.Self.Movement.StandUp = movement.StandUp;
                                    Client.Self.Movement.State = movement.State;
                                    Client.Self.Movement.SendUpdate(true);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_MOVEMENT_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade movement state.
        /// </summary>
        private static readonly Action SaveConferenceState = () =>
        {
            try
            {
                lock (ConferencesStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.CONFERENCE_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer = new XmlSerializer(typeof(HashSet<Conference>));
                            lock (ConferencesLock)
                            {
                                serializer.Serialize(writer, Conferences);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CONFERENCE_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Loads Corrade movement state.
        /// </summary>
        private static readonly Action LoadConferenceState = () =>
        {
            var conferenceStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.CONFERENCE_STATE_FILE);
            if (File.Exists(conferenceStateFile))
            {
                try
                {
                    lock (ConferencesStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(conferenceStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var serializer = new XmlSerializer(typeof(HashSet<Conference>));
                                ((HashSet<Conference>) serializer.Deserialize(streamReader)).AsParallel()
                                    .ForAll(o =>
                                    {
                                        try
                                        {
                                            // Attempt to rejoin the conference.
                                            lock (Locks.ClientInstanceSelfLock)
                                            {
                                                if (!Client.Self.GroupChatSessions.ContainsKey(o.Session))
                                                    Client.Self.ChatterBoxAcceptInvite(o.Session);
                                            }
                                            // Add the conference to the list of conferences.
                                            lock (ConferencesLock)
                                            {
                                                if (!Conferences.AsParallel()
                                                    .Any(
                                                        p =>
                                                            p.Name.Equals(o.Name, StringComparison.Ordinal) &&
                                                            p.Session.Equals(o.Session)))
                                                {
                                                    Conferences.Add(new Conference
                                                    {
                                                        Name = o.Name,
                                                        Session = o.Session,
                                                        Restored = true
                                                    });
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Feedback(
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ConsoleMessage.UNABLE_TO_RESTORE_CONFERENCE),
                                                o.Name,
                                                ex.Message);
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CONFERENCE_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Loads Corrade group cookies.
        /// </summary>
        private static readonly Action LoadGroupCookiesState = () =>
        {
            var groupCookiesStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_COOKIES_STATE_FILE);
            if (File.Exists(groupCookiesStateFile))
            {
                try
                {
                    lock (GroupCookiesStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(groupCookiesStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                            var serializer = new BinaryFormatter();
                            ((Dictionary<UUID, CookieContainer>)
                                serializer.Deserialize(fileStream)).AsParallel()
                                .Where(o => groups.Contains(o.Key))
                                .ForAll(o =>
                                {
                                    lock (GroupCookieContainersLock)
                                    {
                                        if (!GroupCookieContainers.Contains(o))
                                        {
                                            GroupCookieContainers.Add(o.Key, o.Value);
                                            return;
                                        }
                                        GroupCookieContainers[o.Key] = o.Value;
                                    }
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_GROUP_COOKIES_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade group cookies.
        /// </summary>
        private static readonly Action SaveGroupCookiesState = () =>
        {
            try
            {
                lock (GroupCookiesStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_COOKIES_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        var serializer = new BinaryFormatter();
                        lock (GroupCookieContainersLock)
                        {
                            serializer.Serialize(fileStream, GroupCookieContainers);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_GROUP_COOKIES_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Saves Corrade feeds.
        /// </summary>
        private static readonly Action SaveGroupFeedState = () =>
        {
            GroupFeedWatcher.EnableRaisingEvents = false;
            try
            {
                lock (GroupFeedsStateFileLock)
                {
                    using (
                        var fileStream = File.Open(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.FEEDS_STATE_FILE), FileMode.Create,
                            FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                        {
                            var serializer =
                                new XmlSerializer(
                                    typeof(Collections.SerializableDictionary
                                        <string, Collections.SerializableDictionary<UUID, string>>));
                            lock (GroupFeedsLock)
                            {
                                serializer.Serialize(writer, GroupFeeds);
                            }
                            writer.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_SAVE_CORRADE_FEEDS_STATE),
                    e.Message);
            }
            GroupFeedWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly Action LoadGroupFeedState = () =>
        {
            var feedStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.FEEDS_STATE_FILE);
            if (File.Exists(feedStateFile))
            {
                try
                {
                    lock (GroupFeedsStateFileLock)
                    {
                        using (
                            var fileStream = File.Open(feedStateFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                var groups = new HashSet<UUID>(corradeConfiguration.Groups.Select(o => o.UUID));
                                var serializer =
                                    new XmlSerializer(
                                        typeof(Collections.SerializableDictionary
                                            <string, Collections.SerializableDictionary<UUID, string>>));
                                ((Collections.SerializableDictionary
                                    <string, Collections.SerializableDictionary<UUID, string>>)
                                    serializer.Deserialize(streamReader)).AsParallel()
                                    .Where(o => o.Value.Any(p => groups.Contains(p.Key)))
                                    .ForAll(o =>
                                    {
                                        lock (GroupFeedsLock)
                                        {
                                            if (!GroupFeeds.ContainsKey(o.Key))
                                            {
                                                GroupFeeds.Add(o.Key, o.Value);
                                                return;
                                            }
                                            GroupFeeds[o.Key].Clone().AsParallel().ForAll(p =>
                                            {
                                                if (!GroupFeeds[o.Key].ContainsKey(p.Key))
                                                {
                                                    GroupFeeds[o.Key].Add(p.Key, p.Value);
                                                    return;
                                                }
                                                GroupFeeds[o.Key][p.Key] = p.Value;
                                            });
                                        }
                                    });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_FEEDS_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Loads the chatbot configuration and SIML files.
        /// </summary>
        private static readonly Action LoadChatBotFiles = () =>
        {
            if (!string.IsNullOrEmpty(SIMLBotConfigurationWatcher.Path))
                SIMLBotConfigurationWatcher.EnableRaisingEvents = false;
            Feedback(
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.READING_SIML_BOT_CONFIGURATION));
            try
            {
                var SIMLPackage = Path.Combine(
                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY, SIML_BOT_CONSTANTS.PACKAGE_FILE);
                switch (File.Exists(SIMLPackage))
                {
                    case true:
                        SynBot.PackageManager.LoadFromString(File.ReadAllText(SIMLPackage));
                        break;
                    default:
                        var elementList = new List<XDocument>();
                        foreach (var simlDocument in Directory.GetFiles(Path.Combine(
                            Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                            SIML_BOT_CONSTANTS.SIML_DIRECTORY,
                            SIML_BOT_CONSTANTS.SIML_SETTINGS_DIRECTORY), @"*.siml")
                            .Select(XDocument.Load))
                        {
                            elementList.Add(simlDocument);
                            SynBot.AddSiml(simlDocument);
                        }
                        foreach (var simlDocument in Directory.GetFiles(Path.Combine(
                            Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                            SIML_BOT_CONSTANTS.SIML_DIRECTORY), @"*.siml")
                            .Select(XDocument.Load))
                        {
                            elementList.Add(simlDocument);
                            SynBot.AddSiml(simlDocument);
                        }
                        File.WriteAllText(Path.Combine(
                            Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                            SIML_BOT_CONSTANTS.PACKAGE_FILE), SynBot.PackageManager.ConvertToPackage(elementList));
                        break;
                }

                // Load learned and memorized.
                var SIMLLearned = Path.Combine(
                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                    SIML_BOT_CONSTANTS.EVOLVE_DIRECTORY,
                    SIML_BOT_CONSTANTS.LEARNED_FILE);
                if (File.Exists(SIMLLearned))
                {
                    SynBot.AddSiml(XDocument.Load(SIMLLearned));
                }
                var SIMLMemorized = Path.Combine(
                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                    SIML_BOT_CONSTANTS.EVOLVE_DIRECTORY,
                    SIML_BOT_CONSTANTS.MEMORIZED_FILE);
                if (File.Exists(SIMLMemorized))
                {
                    SynBot.AddSiml(XDocument.Load(SIMLMemorized));
                }
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_LOADING_SIML_BOT_FILES),
                    ex.Message);
                if (!string.IsNullOrEmpty(SIMLBotConfigurationWatcher.Path))
                    SIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                return;
            }
            Feedback(
                Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.READ_SIML_BOT_CONFIGURATION));
            if (!string.IsNullOrEmpty(SIMLBotConfigurationWatcher.Path))
                SIMLBotConfigurationWatcher.EnableRaisingEvents = true;
        };

        private static volatile bool runHTTPServer;
        private static volatile bool runTCPNotificationsServer;
        private static volatile bool runCallbackThread = true;
        private static volatile bool runNotificationThread = true;

        public Corrade()
        {
            if (Environment.UserInteractive) return;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    try
                    {
                        InstalledServiceName = (string)
                            new ManagementObjectSearcher("SELECT * FROM Win32_Service where ProcessId = " +
                                                         Process.GetCurrentProcess().Id).Get()
                                .Cast<ManagementBaseObject>()
                                .First()["Name"];
                    }
                    catch (Exception)
                    {
                        InstalledServiceName = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME;
                    }
                    break;
                default:
                    InstalledServiceName = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME;
                    break;
            }
            CorradeEventLog.Source = InstalledServiceName;
            CorradeEventLog.Log = CORRADE_CONSTANTS.LOG_FACILITY;
            CorradeEventLog.BeginInit();
            if (!EventLog.SourceExists(CorradeEventLog.Source))
            {
                EventLog.CreateEventSource(CorradeEventLog.Source, CorradeEventLog.Log);
            }
            CorradeEventLog.EndInit();
        }

        /// <summary>
        ///     Main thread that processes TCP connections.
        /// </summary>
        private static void ProcessTCPNotifications()
        {
            TCPListener =
                new TcpListener(
                    new IPEndPoint(IPAddress.Parse(corradeConfiguration.TCPNotificationsServerAddress),
                        (int) corradeConfiguration.TCPNotificationsServerPort));
            TCPListener.Start();

            do
            {
                var TCPClient = TCPListener.AcceptTcpClient();

                new Thread(() =>
                {
                    IPEndPoint remoteEndPoint = null;
                    var commandGroup = new Configuration.Group();
                    try
                    {
                        remoteEndPoint = TCPClient.Client.RemoteEndPoint as IPEndPoint;
                        var certificate =
                            new X509Certificate(corradeConfiguration.TCPNotificationsCertificatePath,
                                corradeConfiguration.TCPNotificationsCertificatePassword);
                        using (var networkStream = new SslStream(TCPClient.GetStream()))
                        {
                            SslProtocols protocol;
                            if (!Enum.TryParse(corradeConfiguration.TCPNotificationsSSLProtocol, out protocol))
                                protocol = SslProtocols.Tls12;

                            // Do not require a client certificate.
                            networkStream.AuthenticateAsServer(certificate, false, protocol, true);

                            using (
                                var streamReader = new StreamReader(networkStream,
                                    Encoding.UTF8))
                            {
                                var receiveLine = streamReader.ReadLine();

                                using (
                                    var streamWriter = new StreamWriter(networkStream,
                                        Encoding.UTF8))
                                {
                                    commandGroup = GetCorradeGroupFromMessage(receiveLine);
                                    switch (
                                        commandGroup != null &&
                                        !commandGroup.Equals(default(Configuration.Group)) &&
                                        Authenticate(commandGroup.UUID,
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(
                                                            ScriptKeys.PASSWORD)),
                                                    receiveLine))))
                                    {
                                        case false:
                                            streamWriter.WriteLine(
                                                KeyValue.Encode(new Dictionary<string, string>
                                                {
                                                    {
                                                        Reflection.GetNameFromEnumValue(
                                                            ScriptKeys.SUCCESS),
                                                        false.ToString()
                                                    }
                                                }));
                                            streamWriter.Flush();
                                            TCPClient.Close();
                                            return;
                                    }

                                    var notificationTypes =
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(
                                                    Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                                receiveLine));
                                    Notification notification;
                                    lock (GroupNotificationsLock)
                                    {
                                        notification =
                                            GroupNotifications.AsParallel().FirstOrDefault(
                                                o =>
                                                    o.GroupUUID.Equals(commandGroup.UUID));
                                    }
                                    // Build any requested data for raw notifications.
                                    var fields = wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                            receiveLine));
                                    var data = new HashSet<string>();
                                    var LockObject = new object();
                                    if (!string.IsNullOrEmpty(fields))
                                    {
                                        CSV.ToEnumerable(fields)
                                            .ToArray()
                                            .AsParallel()
                                            .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                            {
                                                lock (LockObject)
                                                {
                                                    data.Add(o);
                                                }
                                            });
                                    }
                                    switch (notification != null)
                                    {
                                        case false:
                                            notification = new Notification
                                            {
                                                GroupName = commandGroup.Name,
                                                GroupUUID = commandGroup.UUID,
                                                NotificationURLDestination =
                                                    new Collections.SerializableDictionary
                                                        <Configuration.Notifications, HashSet<string>>(),
                                                NotificationTCPDestination =
                                                    new Dictionary
                                                        <Configuration.Notifications, HashSet<IPEndPoint>>(),
                                                Data = data
                                            };
                                            break;
                                        case true:
                                            if (notification.NotificationTCPDestination == null)
                                            {
                                                notification.NotificationTCPDestination =
                                                    new Dictionary
                                                        <Configuration.Notifications, HashSet<IPEndPoint>>();
                                            }
                                            if (notification.NotificationURLDestination == null)
                                            {
                                                notification.NotificationURLDestination =
                                                    new Collections.SerializableDictionary
                                                        <Configuration.Notifications, HashSet<string>>();
                                            }
                                            break;
                                    }

                                    var succeeded = true;
                                    Parallel.ForEach(CSV.ToEnumerable(
                                        notificationTypes)
                                        .ToArray()
                                        .AsParallel()
                                        .Where(o => !string.IsNullOrEmpty(o)),
                                        (o, state) =>
                                        {
                                            var notificationValue =
                                                (ulong)
                                                    Reflection
                                                        .GetEnumValueFromName
                                                        <Configuration.Notifications>(o);
                                            if (
                                                !GroupHasNotification(commandGroup.UUID,
                                                    notificationValue))
                                            {
                                                // one of the notification was not allowed, so abort
                                                succeeded = false;
                                                state.Break();
                                            }
                                            switch (
                                                !notification.NotificationTCPDestination.ContainsKey(
                                                    (Configuration.Notifications) notificationValue))
                                            {
                                                case true:
                                                    lock (LockObject)
                                                    {
                                                        notification.NotificationTCPDestination.Add(
                                                            (Configuration.Notifications) notificationValue,
                                                            new HashSet<IPEndPoint> {remoteEndPoint});
                                                    }
                                                    break;
                                                default:
                                                    lock (LockObject)
                                                    {
                                                        notification.NotificationTCPDestination[
                                                            (Configuration.Notifications) notificationValue]
                                                            .Add(
                                                                remoteEndPoint);
                                                    }
                                                    break;
                                            }
                                        });

                                    switch (succeeded)
                                    {
                                        case true:
                                            lock (GroupNotificationsLock)
                                            {
                                                // Replace notification.
                                                GroupNotifications.RemoveWhere(
                                                    o =>
                                                        o.GroupUUID.Equals(commandGroup.UUID));
                                                GroupNotifications.Add(notification);
                                                // Build the group notification cache.
                                                GroupNotificationsCache.Clear();
                                                new List<Configuration.Notifications>(
                                                    Reflection.GetEnumValues<Configuration.Notifications>())
                                                    .AsParallel().ForAll(o =>
                                                    {
                                                        GroupNotifications.AsParallel()
                                                            .Where(p => p.NotificationMask.IsMaskFlagSet(o)).ForAll(p =>
                                                            {
                                                                lock (LockObject)
                                                                {
                                                                    if (GroupNotificationsCache.ContainsKey(o))
                                                                    {
                                                                        GroupNotificationsCache[o].Add(p);
                                                                        return;
                                                                    }
                                                                    GroupNotificationsCache.Add(o,
                                                                        new HashSet<Notification> {p});
                                                                }
                                                            });
                                                    });
                                            }
                                            // Save the notifications state.
                                            SaveNotificationState.Invoke();
                                            streamWriter.WriteLine(
                                                KeyValue.Encode(new Dictionary<string, string>
                                                {
                                                    {
                                                        Reflection.GetNameFromEnumValue(
                                                            ScriptKeys.SUCCESS),
                                                        true.ToString()
                                                    }
                                                }));
                                            streamWriter.Flush();
                                            break;
                                        default:
                                            streamWriter.WriteLine(
                                                KeyValue.Encode(new Dictionary<string, string>
                                                {
                                                    {
                                                        Reflection.GetNameFromEnumValue(
                                                            ScriptKeys.SUCCESS),
                                                        false.ToString()
                                                    }
                                                }));
                                            streamWriter.Flush();
                                            TCPClient.Close();
                                            return;
                                    }

                                    do
                                    {
                                        var notificationTCPQueueElement = new NotificationTCPQueueElement();
                                        if (
                                            !NotificationTCPQueue.Dequeue(
                                                (int) corradeConfiguration.TCPNotificationThrottle,
                                                ref notificationTCPQueueElement))
                                            continue;
                                        if (notificationTCPQueueElement.Equals(default(NotificationTCPQueueElement)) ||
                                            !notificationTCPQueueElement.IPEndPoint.Equals(remoteEndPoint))
                                            continue;
                                        streamWriter.WriteLine(KeyValue.Encode(notificationTCPQueueElement.message));
                                        streamWriter.Flush();
                                    } while (runTCPNotificationsServer && TCPClient.Connected);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        switch (ex.InnerException != null)
                        {
                            case true:
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.TCP_NOTIFICATIONS_SERVER_ERROR),
                                    ex.Message, ex.InnerException.Message);
                                break;
                            default:
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.TCP_NOTIFICATIONS_SERVER_ERROR),
                                    ex.Message);
                                break;
                        }
                    }
                    finally
                    {
                        if (remoteEndPoint != null && commandGroup != null &&
                            !commandGroup.Equals(default(Configuration.Group)))
                        {
                            lock (GroupNotificationsLock)
                            {
                                var notification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupUUID.Equals(commandGroup.UUID));
                                if (notification != null)
                                {
                                    var
                                        notificationTCPDestination =
                                            new Dictionary<Configuration.Notifications, HashSet<IPEndPoint>>
                                                ();
                                    notification.NotificationTCPDestination.AsParallel().ForAll(o =>
                                    {
                                        switch (o.Value.Contains(remoteEndPoint))
                                        {
                                            case true:
                                                var destinations =
                                                    new HashSet<IPEndPoint>(
                                                        o.Value.Where(p => !p.Equals(remoteEndPoint)));
                                                notificationTCPDestination.Add(o.Key, destinations);
                                                break;
                                            default:
                                                notificationTCPDestination.Add(o.Key, o.Value);
                                                break;
                                        }
                                    });

                                    GroupNotifications.Remove(notification);
                                    GroupNotifications.Add(new Notification
                                    {
                                        GroupName = notification.GroupName,
                                        GroupUUID = notification.GroupUUID,
                                        NotificationURLDestination =
                                            notification.NotificationURLDestination,
                                        NotificationTCPDestination = notificationTCPDestination,
                                        Afterburn = notification.Afterburn,
                                        Data = notification.Data
                                    });
                                    // Build the group notification cache.
                                    GroupNotificationsCache.Clear();
                                    var LockObject = new object();
                                    new List<Configuration.Notifications>(
                                        Reflection.GetEnumValues<Configuration.Notifications>())
                                        .AsParallel().ForAll(o =>
                                        {
                                            GroupNotifications.AsParallel()
                                                .Where(p => p.NotificationMask.IsMaskFlagSet(o)).ForAll(p =>
                                                {
                                                    lock (LockObject)
                                                    {
                                                        if (GroupNotificationsCache.ContainsKey(o))
                                                        {
                                                            GroupNotificationsCache[o].Add(p);
                                                            return;
                                                        }
                                                        GroupNotificationsCache.Add(o,
                                                            new HashSet<Notification> {p});
                                                    }
                                                });
                                        });
                                }
                            }
                        }
                    }
                })
                {IsBackground = true}.Start();
            } while (runTCPNotificationsServer);
        }

        private static bool ConsoleCtrlCheck(NativeMethods.CtrlType ctrlType)
        {
            // Set the user disconnect semaphore.
            ConnectionSemaphores['u'].Set();
            // Wait for threads to finish.
            Thread.Sleep((int) corradeConfiguration.ServicesTimeout);
            return true;
        }

        /// <summary>
        ///     Used to check whether a group name matches a group password.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="password">the password for the group</param>
        /// <returns>true if the agent has authenticated</returns>
        private static bool Authenticate(string group, string password)
        {
            /*
             * If the master override feature is enabled and the password matches the 
             * master override password then consider the request to be authenticated.
             * Otherwise, check that the password matches the password for the group.
             */
            return (corradeConfiguration.EnableMasterPasswordOverride &&
                    !string.IsNullOrEmpty(corradeConfiguration.MasterPasswordOverride) && (
                        Strings.StringEquals(corradeConfiguration.MasterPasswordOverride, password,
                            StringComparison.Ordinal) ||
                        Utils.SHA1String(password)
                            .Equals(corradeConfiguration.MasterPasswordOverride, StringComparison.OrdinalIgnoreCase))) ||
                   corradeConfiguration.Groups.AsParallel().Any(
                       o =>
                           Strings.StringEquals(group, o.Name, StringComparison.OrdinalIgnoreCase) &&
                           (Strings.StringEquals(o.Password, password, StringComparison.Ordinal) ||
                            Utils.SHA1String(password)
                                .Equals(o.Password, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        ///     Used to check whether a group UUID matches a group password.
        /// </summary>
        /// <param name="group">the UUID of the group</param>
        /// <param name="password">the password for the group</param>
        /// <returns>true if the agent has authenticated</returns>
        private static bool Authenticate(UUID group, string password)
        {
            /*
             * If the master override feature is enabled and the password matches the 
             * master override password then consider the request to be authenticated.
             * Otherwise, check that the password matches the password for the group.
             */
            return (corradeConfiguration.EnableMasterPasswordOverride &&
                    !string.IsNullOrEmpty(corradeConfiguration.MasterPasswordOverride) && (
                        Strings.StringEquals(corradeConfiguration.MasterPasswordOverride, password,
                            StringComparison.Ordinal) ||
                        Utils.SHA1String(password)
                            .Equals(corradeConfiguration.MasterPasswordOverride, StringComparison.OrdinalIgnoreCase))) ||
                   corradeConfiguration.Groups.AsParallel().Any(
                       o =>
                           group.Equals(o.UUID) &&
                           (Strings.StringEquals(o.Password, password, StringComparison.Ordinal) ||
                            Utils.SHA1String(password)
                                .Equals(o.Password, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        ///     Used to check whether a group has certain permissions for Corrade.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="permission">the numeric Corrade permission</param>
        /// <returns>true if the group has permission</returns>
        private static bool HasCorradePermission(string group, ulong permission)
        {
            return !permission.Equals(0) && corradeConfiguration.Groups.AsParallel().Any(
                o =>
                    Strings.StringEquals(group, o.Name, StringComparison.OrdinalIgnoreCase) &&
                    o.PermissionMask.IsMaskFlagSet((Configuration.Permissions) permission));
        }

        /// <summary>
        ///     Used to check whether a group has certain permissions for Corrade.
        /// </summary>
        /// <param name="group">the UUID of the group</param>
        /// <param name="permission">the numeric Corrade permission</param>
        /// <returns>true if the group has permission</returns>
        private static bool HasCorradePermission(UUID group, ulong permission)
        {
            return !permission.Equals(0) && corradeConfiguration.Groups.AsParallel()
                .Any(
                    o => group.Equals(o.UUID) && o.PermissionMask.IsMaskFlagSet((Configuration.Permissions) permission));
        }

        /// <summary>
        ///     Fetches a Corrade group from a key-value formatted message message.
        /// </summary>
        /// <param name="message">the message to inspect</param>
        /// <returns>the configured group</returns>
        private static Configuration.Group GetCorradeGroupFromMessage(string message)
        {
            var group =
                wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.GROUP)),
                    message));
            UUID groupUUID;
            return UUID.TryParse(group, out groupUUID)
                ? corradeConfiguration.Groups.AsParallel().FirstOrDefault(o => o.UUID.Equals(groupUUID))
                : corradeConfiguration.Groups.AsParallel()
                    .FirstOrDefault(o => Strings.StringEquals(group, o.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Used to check whether a group has a certain notification for Corrade.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="notification">the numeric Corrade notification</param>
        /// <returns>true if the group has the notification</returns>
        private static bool GroupHasNotification(string group, ulong notification)
        {
            return !notification.Equals(0) && corradeConfiguration.Groups.AsParallel().Any(
                o => Strings.StringEquals(group, o.Name, StringComparison.OrdinalIgnoreCase) &&
                     o.NotificationMask.IsMaskFlagSet((Configuration.Notifications) notification));
        }

        /// <summary>
        ///     Used to check whether a group has a certain notification for Corrade.
        /// </summary>
        /// <param name="group">the UUID of the group</param>
        /// <param name="notification">the numeric Corrade notification</param>
        /// <returns>true if the group has the notification</returns>
        private static bool GroupHasNotification(UUID group, ulong notification)
        {
            return !notification.Equals(0) && corradeConfiguration.Groups.AsParallel().Any(
                o => group.Equals(o.UUID) &&
                     o.NotificationMask.IsMaskFlagSet((Configuration.Notifications) notification));
        }

        /// <summary>
        ///     Posts messages to console or log-files.
        /// </summary>
        /// <param name="messages">a list of messages</param>
        public static void Feedback(params string[] messages)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(
                () =>
                {
                    var output = new List<string>
                    {
                        !string.IsNullOrEmpty(InstalledServiceName)
                            ? InstalledServiceName
                            : CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME,
                        string.Format(Utils.EnUsCulture, "[{0}]",
                            DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                Utils.EnUsCulture.DateTimeFormat))
                    };

                    output.AddRange(messages.Select(o => o));

                    // Attempt to write to log file,
                    if (corradeConfiguration.ClientLogEnabled)
                    {
                        try
                        {
                            lock (ClientLogFileLock)
                            {
                                using (
                                    var fileStream = File.Open(corradeConfiguration.ClientLogFile,
                                        FileMode.Append,
                                        FileAccess.Write, FileShare.None))
                                {
                                    using (var logWriter = new StreamWriter(fileStream, Encoding.UTF8))
                                    {
                                        logWriter.WriteLine(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR,
                                            output.ToArray()));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // or fail and append the fail message.
                            output.Add(string.Format(Utils.EnUsCulture, "{0} {1}",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.COULD_NOT_WRITE_TO_CLIENT_LOG_FILE),
                                ex.Message));
                        }
                    }

                    switch (Environment.UserInteractive)
                    {
                        case false:
                            switch (Environment.OSVersion.Platform)
                            {
                                case PlatformID.Win32NT:
                                    CorradeEventLog.WriteEntry(
                                        string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR, output.ToArray()),
                                        EventLogEntryType.Information);
                                    break;
                            }
                            break;
                        default:
                            Console.WriteLine(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR, output.ToArray()));
                            break;
                    }
                },
                corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
        }

        /// <summary>
        ///     Posts messages to console or log-files.
        /// </summary>
        /// <param name="multiline">whether to treat the messages as separate lines</param>
        /// <param name="messages">a list of messages</param>
        public static void Feedback(bool multiline, params string[] messages)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(
                () =>
                {
                    if (!multiline)
                    {
                        Feedback(messages);
                        return;
                    }

                    var output =
                        new List<string>(
                            messages.Select(
                                o => string.Format(Utils.EnUsCulture, "{0}{1}[{2}]{3}{4}",
                                    !string.IsNullOrEmpty(InstalledServiceName)
                                        ? InstalledServiceName
                                        : CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME, CORRADE_CONSTANTS.ERROR_SEPARATOR,
                                    DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                        Utils.EnUsCulture.DateTimeFormat),
                                    CORRADE_CONSTANTS.ERROR_SEPARATOR,
                                    o)));

                    // Attempt to write to log file,
                    if (corradeConfiguration.ClientLogEnabled)
                    {
                        try
                        {
                            lock (ClientLogFileLock)
                            {
                                using (
                                    var fileStream = File.Open(corradeConfiguration.ClientLogFile,
                                        FileMode.Append,
                                        FileAccess.Write, FileShare.None))
                                {
                                    using (var logWriter = new StreamWriter(fileStream, Encoding.UTF8))
                                    {
                                        foreach (var message in output)
                                        {
                                            logWriter.WriteLine(message);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // or fail and append the fail message.
                            output.Add(string.Format(Utils.EnUsCulture, "{0} {1}",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.COULD_NOT_WRITE_TO_CLIENT_LOG_FILE),
                                ex.Message));
                        }
                    }

                    switch (Environment.UserInteractive)
                    {
                        case false:
                            switch (Environment.OSVersion.Platform)
                            {
                                case PlatformID.Win32NT:
                                    foreach (var message in output)
                                    {
                                        CorradeEventLog.WriteEntry(message, EventLogEntryType.Information);
                                    }
                                    break;
                            }
                            break;
                        default:
                            foreach (var message in output)
                            {
                                Console.WriteLine(message);
                            }
                            break;
                    }
                },
                corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
        }

        public static int Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Any())
                {
                    var action = string.Empty;
                    for (var i = 0; i < args.Length; ++i)
                    {
                        switch (args[i].ToUpper())
                        {
                            case "/INSTALL":
                                action = "INSTALL";
                                break;
                            case "/UNINSTALL":
                                action = "UNINSTALL";
                                break;
                            case "/NAME":
                                if (args.Length > i + 1)
                                {
                                    InstalledServiceName = args[++i];
                                }
                                break;
                        }
                    }

                    switch (action)
                    {
                        case "INSTALL":
                            // If administrator privileges are obtained, then install the service.
                            if (new WindowsPrincipal
                                (WindowsIdentity.GetCurrent()).IsInRole
                                (WindowsBuiltInRole.Administrator))
                                return InstallService();
                            if (!ForkPriviledgedSelf(args))
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_INSTALL_SERVICE));
                                return -1;
                            }
                            return 0;
                        case "UNINSTALL":
                            // If administrator privileges are obtained, then uninstall the service.
                            if (new WindowsPrincipal
                                (WindowsIdentity.GetCurrent()).IsInRole
                                (WindowsBuiltInRole.Administrator))
                                return UninstallService();
                            if (!ForkPriviledgedSelf(args))
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_UNINSTALL_SERVICE));
                                return -1;
                            }
                            return 0;
                    }
                }
                // run interactively and log to console
                var corrade = new Corrade();
                corrade.OnStart(null);
                return 0;
            }

            // run as a standard service
            Run(new Corrade());
            return 0;
        }

        private static bool ForkPriviledgedSelf(string[] args)
        {
            // The name parameter has to be escaped in order to preserve the full service name.
            for (var i = 0; i < args.Length; ++i)
            {
                switch (args[i].ToUpper())
                {
                    case "/NAME":
                        if (args.Length > i + 1)
                        {
                            ++i;
                            args[i] = $"\"{args[i]}\"";
                        }
                        break;
                }
            }

            try
            {
                // Create an elevated process with the original arguments.
                var info = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, string.Join(" ", args))
                {
                    Verb = "runas" // indicates to elevate privileges
                };

                var process = new Process
                {
                    EnableRaisingEvents = true, // enable WaitForExit()
                    StartInfo = info
                };

                process.Start();
                process.WaitForExit();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static int InstallService()
        {
            try
            {
                // install the service with the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new[] {Assembly.GetExecutingAssembly().Location});
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.GetType() == typeof(Win32Exception))
                {
                    var we = (Win32Exception) ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service already installed!", we.ErrorCode);
                    return we.ErrorCode;
                }
                Console.WriteLine(ex.ToString());
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
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(Win32Exception))
                {
                    var we = (Win32Exception) ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service not installed!", we.ErrorCode);
                    return we.ErrorCode;
                }
                Console.WriteLine(ex.ToString());
                return -1;
            }

            return 0;
        }

        protected override void OnStop()
        {
            base.OnStop();
            ConnectionSemaphores['u'].Set();
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
            // Set the MTA to above normal for connection consistency.
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            // Set the current directory to the service directory.
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            // Load the configuration file.
            lock (ConfigurationFileLock)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.READING_CORRADE_CONFIGURATION));
                try
                {
                    corradeConfiguration.Load(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_LOAD_CORRADE_CONFIGURATION),
                        ex.Message);
                    return;
                }

                // Check configuration file compatiblity.
                Version minimalConfig;
                Version versionConfig;
                if (
                    !Version.TryParse(CORRADE_CONSTANTS.ASSEMBLY_CUSTOM_ATTRIBUTES["configuration"], out minimalConfig) ||
                    !Version.TryParse(corradeConfiguration.Version, out versionConfig) ||
                    !minimalConfig.Major.Equals(versionConfig.Major) || !minimalConfig.Minor.Equals(versionConfig.Minor))
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.CONFIGURATION_FILE_VERSION_MISMATCH));

                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.READ_CORRADE_CONFIGURATION));
            }
            // Load group cookies.
            LoadGroupCookiesState.Invoke();
            if (!corradeConfiguration.Equals(default(Configuration)))
            {
                UpdateDynamicConfiguration(corradeConfiguration);
            }
            // Write the logo.
            Feedback(true, CORRADE_CONSTANTS.LOGO.ToArray());
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
                                (sender, args) => ConnectionSemaphores['u'].Set();
                        }
                    }
                    break;
            }
            // Load language detection
            try
            {
                languageDetector.AddAllLanguages();
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_LOADING_LANGUAGE_DETECTION),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the configuration file.
            FileSystemEventHandler HandleConfigurationFileChanged = null;
            try
            {
                ConfigurationWatcher.Path = Directory.GetCurrentDirectory();
                ConfigurationWatcher.Filter = CORRADE_CONSTANTS.CONFIGURATION_FILE;
                ConfigurationWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleConfigurationFileChanged = (sender, args) => ConfigurationChangedTimer.Change(1000, 0);
                ConfigurationWatcher.Changed += HandleConfigurationFileChanged;
                ConfigurationWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_CONFIGURATION_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the notifications file.
            FileSystemEventHandler HandleNotificationsFileChanged = null;
            try
            {
                NotificationsWatcher.Path = Path.Combine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                NotificationsWatcher.Filter = CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE;
                NotificationsWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleNotificationsFileChanged = (sender, args) => NotificationsChangedTimer.Change(1000, 0);
                NotificationsWatcher.Changed += HandleNotificationsFileChanged;
                NotificationsWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_NOTIFICATIONS_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the group schedules file.
            FileSystemEventHandler HandleGroupSchedulesFileChanged = null;
            try
            {
                SchedulesWatcher.Path = Path.Combine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                SchedulesWatcher.Filter = CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE;
                SchedulesWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleGroupSchedulesFileChanged = (sender, args) => GroupSchedulesChangedTimer.Change(1000, 0);
                SchedulesWatcher.Changed += HandleGroupSchedulesFileChanged;
                SchedulesWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_SCHEDULES_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the feeds file.
            FileSystemEventHandler HandleGroupFeedsFileChanged = null;
            try
            {
                GroupFeedWatcher.Path = Path.Combine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                GroupFeedWatcher.Filter = CORRADE_CONSTANTS.FEEDS_STATE_FILE;
                GroupFeedWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleGroupFeedsFileChanged = (sender, args) => GroupFeedsChangedTimer.Change(1000, 0);
                GroupFeedWatcher.Changed += HandleGroupFeedsFileChanged;
                GroupFeedWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_FEEDS_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the group soft bans file.
            FileSystemEventHandler HandleGroupSoftBansFileChanged = null;
            try
            {
                GroupSoftBansWatcher.Path = Path.Combine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                GroupSoftBansWatcher.Filter = CORRADE_CONSTANTS.GROUP_SOFT_BAN_STATE_FILE;
                GroupSoftBansWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleGroupSoftBansFileChanged = (sender, args) => GroupSoftBansChangedTimer.Change(1000, 0);
                GroupSoftBansWatcher.Changed += HandleGroupSoftBansFileChanged;
                GroupSoftBansWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_SOFT_BANS_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up the SIML bot in case it has been enabled.
            FileSystemEventHandler HandleSIMLBotConfigurationChanged = null;
            try
            {
                SIMLBotConfigurationWatcher.Path = Path.Combine(Directory.GetCurrentDirectory(),
                    SIML_BOT_CONSTANTS.ROOT_DIRECTORY);
                SIMLBotConfigurationWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleSIMLBotConfigurationChanged = (sender, args) => SIMLConfigurationChangedTimer.Change(1000, 0);
                SIMLBotConfigurationWatcher.Changed += HandleSIMLBotConfigurationChanged;
                if (corradeConfiguration.EnableSIML)
                    SIMLBotConfigurationWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_SIML_CONFIGURATION_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Suppress standard OpenMetaverse logs, we have better ones.
            Settings.LOG_LEVEL = OpenMetaverse.Helpers.LogLevel.None;
            Client.Settings.ALWAYS_REQUEST_PARCEL_ACL = true;
            Client.Settings.ALWAYS_DECODE_OBJECTS = true;
            Client.Settings.ALWAYS_REQUEST_OBJECTS = true;
            Client.Settings.SEND_AGENT_APPEARANCE = true;
            Client.Settings.AVATAR_TRACKING = true;
            Client.Settings.OBJECT_TRACKING = true;
            Client.Settings.PARCEL_TRACKING = true;
            Client.Settings.ALWAYS_REQUEST_PARCEL_DWELL = true;
            Client.Settings.SEND_AGENT_UPDATES = true;
            // Smoother movement for autopilot.
            Client.Settings.DISABLE_AGENT_UPDATE_DUPLICATE_CHECK = true;
            Client.Settings.ENABLE_CAPS = true;
            // Inventory settings.
            Client.Settings.FETCH_MISSING_INVENTORY = true;
            Client.Settings.HTTP_INVENTORY = true;
            // Set the asset cache directory.
            Client.Settings.ASSET_CACHE_DIR = Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                CORRADE_CONSTANTS.ASSET_CACHE_DIRECTORY);
            Client.Settings.USE_ASSET_CACHE = true;
            // More precision for object and avatar tracking updates.
            Client.Settings.USE_INTERPOLATION_TIMER = true;
            // Transfer textures over HTTP if possible.
            Client.Settings.USE_HTTP_TEXTURES = true;
            // Needed for commands dealing with terrain height.
            Client.Settings.STORE_LAND_PATCHES = true;
            // Decode simulator statistics.
            Client.Settings.ENABLE_SIMSTATS = true;
            // Send pings for lag measurement.
            Client.Settings.SEND_PINGS = true;
            // Throttling.
            Client.Settings.SEND_AGENT_THROTTLE = true;
            // Enable multiple simulators.
            Client.Settings.MULTIPLE_SIMS = true;
            // Check TOS
            if (!corradeConfiguration.TOSAccepted)
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.TOS_NOT_ACCEPTED));
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }

            // Get the custom location.
            var location = corradeConfiguration.StartLocations.ElementAtOrDefault(LoginLocationIndex++);
            var startLocation = new
                wasOpenMetaverse.Helpers.StartLocationParser(location);
            // Proceed to log-in.
            Login = new LoginParams(
                Client,
                corradeConfiguration.FirstName,
                corradeConfiguration.LastName,
                corradeConfiguration.Password,
                CORRADE_CONSTANTS.CLIENT_CHANNEL,
                CORRADE_CONSTANTS.CORRADE_VERSION.ToString(Utils.EnUsCulture),
                corradeConfiguration.LoginURL)
            {
                Author = CORRADE_CONSTANTS.WIZARDRY_AND_STEAMWORKS,
                AgreeToTos = corradeConfiguration.TOSAccepted,
                Start =
                    startLocation.isCustom
                        ? NetworkManager.StartLocation(startLocation.Sim, startLocation.X, startLocation.Y,
                            startLocation.Z)
                        : location,
                UserAgent = CORRADE_CONSTANTS.USER_AGENT.ToString()
            };

            // Set the outgoing IP address if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.BindIPAddress))
            {
                try
                {
                    Settings.BIND_ADDR = IPAddress.Parse(corradeConfiguration.BindIPAddress);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.UNKNOWN_IP_ADDRESS),
                        ex.Message);
                    Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
                }
            }
            // Set the ID0 if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.DriveIdentifierHash))
            {
                Login.ID0 = Utils.MD5String(corradeConfiguration.DriveIdentifierHash);
            }
            // Set the MAC if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.NetworkCardMAC))
            {
                Login.MAC = Utils.MD5String(corradeConfiguration.NetworkCardMAC);
            }
            // Load Corrade caches.
            LoadCorradeCache.Invoke();
            // Load group members.
            LoadGroupMembersState.Invoke();
            // Load notification state.
            LoadNotificationState.Invoke();
            // Load group scheduls state.
            LoadGroupSchedulesState.Invoke();
            // Load feeds state.
            LoadGroupFeedState.Invoke();
            // Load group soft bans state.
            LoadGroupSoftBansState.Invoke();
            // Load group Bayes classifications.
            LoadGroupBayesClassificiations.Invoke();
            // Start the callback thread to send callbacks.
            var CallbackThread = new Thread(() =>
            {
                do
                {
                    try
                    {
                        var callbackQueueElement = new CallbackQueueElement();
                        if (CallbackQueue.Dequeue((int) corradeConfiguration.CallbackThrottle, ref callbackQueueElement))
                        {
                            CorradeThreadPool[Threading.Enumerations.ThreadType.POST].Spawn(async () =>
                            {
                                Web.wasHTTPClient wasHTTPClient;
                                lock (GroupHTTPClientsLock)
                                {
                                    GroupHTTPClients.TryGetValue(callbackQueueElement.GroupUUID,
                                        out wasHTTPClient);
                                }
                                if (wasHTTPClient != null)
                                {
                                    await wasHTTPClient.POST(callbackQueueElement.URL, callbackQueueElement.message);
                                }
                            }, corradeConfiguration.MaximumPOSTThreads);
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.CALLBACK_ERROR),
                            ex.Message);
                    }
                } while (runCallbackThread);
            })
            {IsBackground = true};
            CallbackThread.Start();
            // Start the notification thread for notifications.
            var NotificationThread = new Thread(() =>
            {
                do
                {
                    try
                    {
                        var notificationQueueElement = new NotificationQueueElement();
                        if (NotificationQueue.Dequeue((int) corradeConfiguration.NotificationThrottle,
                            ref notificationQueueElement))
                        {
                            CorradeThreadPool[Threading.Enumerations.ThreadType.POST].Spawn(async () =>
                            {
                                Web.wasHTTPClient wasHTTPClient;
                                lock (GroupHTTPClientsLock)
                                {
                                    GroupHTTPClients.TryGetValue(notificationQueueElement.GroupUUID,
                                        out wasHTTPClient);
                                }
                                if (wasHTTPClient != null)
                                {
                                    await
                                        wasHTTPClient.POST(notificationQueueElement.URL,
                                            notificationQueueElement.message);
                                }
                            },
                                corradeConfiguration.MaximumPOSTThreads);
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.NOTIFICATION_ERROR),
                            ex.Message);
                    }
                } while (runNotificationThread);
            })
            {IsBackground = true};
            NotificationThread.Start();
            // Start the group membership thread.
            GroupMembershipTimer.Change(TimeSpan.FromMilliseconds(corradeConfiguration.MembershipSweepInterval),
                TimeSpan.FromMilliseconds(corradeConfiguration.MembershipSweepInterval));
            // Install non-dynamic global event handlers.
            Client.Inventory.InventoryObjectOffered += HandleInventoryObjectOffered;
            Client.Network.LoginProgress += HandleLoginProgress;
            Client.Network.LoggedOut += HandleLoggedOut;
            Client.Appearance.AppearanceSet += HandleAppearanceSet;
            Client.Network.SimConnected += HandleSimulatorConnected;
            Client.Network.Disconnected += HandleDisconnected;
            Client.Network.SimDisconnected += HandleSimulatorDisconnected;
            Client.Network.EventQueueRunning += HandleEventQueueRunning;
            Client.Self.TeleportProgress += HandleTeleportProgress;
            Client.Self.ChatFromSimulator += HandleChatFromSimulator;
            Client.Groups.GroupJoinedReply += HandleGroupJoined;
            Client.Groups.GroupLeaveReply += HandleGroupLeave;
            Client.Sound.PreloadSound += HandlePreloadSound;
            // Each Instant Message is processed in its own thread.
            Client.Self.IM +=
                (sender, args) => CorradeThreadPool[Threading.Enumerations.ThreadType.INSTANT_MESSAGE].Spawn(
                    () => HandleSelfIM(sender, args),
                    corradeConfiguration.MaximumInstantMessageThreads);
            // Log-in to the grid.
            Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.LOGGING_IN));
            lock (Locks.ClientInstanceNetworkLock)
            {
                Client.Network.BeginLogin(Login);
            }
            /*
             * The main thread spins around waiting for the semaphores to become invalidated,
             * at which point Corrade will consider its connection to the grid severed and
             * will terminate.
             *
             */
            WaitHandle.WaitAny(ConnectionSemaphores.Values.Select(o => (WaitHandle) o).ToArray());
            // Now log-out.
            Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.LOGGING_OUT));

            // Disable the configuration watcher.
            try
            {
                ConfigurationWatcher.EnableRaisingEvents = false;
                ConfigurationWatcher.Changed -= HandleConfigurationFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the notifications watcher.
            try
            {
                NotificationsWatcher.EnableRaisingEvents = false;
                NotificationsWatcher.Changed -= HandleNotificationsFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the group schedule watcher.
            try
            {
                SchedulesWatcher.EnableRaisingEvents = false;
                SchedulesWatcher.Changed -= HandleGroupSchedulesFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the SIML bot configuration watcher.
            try
            {
                SIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                SIMLBotConfigurationWatcher.Changed -= HandleSIMLBotConfigurationChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the RSS feeds watcher.
            try
            {
                GroupFeedWatcher.EnableRaisingEvents = false;
                GroupFeedWatcher.Changed -= HandleGroupFeedsFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the group soft bans watcher.
            try
            {
                GroupSoftBansWatcher.EnableRaisingEvents = false;
                GroupSoftBansWatcher.Changed -= HandleGroupSoftBansFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }

            // Reject any inventory that has not been accepted.
            lock (InventoryOffersLock)
            {
                InventoryOffers.Values.AsParallel().ForAll(o =>
                {
                    o.Args.Accept = false;
                    o.Event.Set();
                });
            }

            // Perform the logout now.
            lock (Locks.ClientInstanceNetworkLock)
            {
                if (Client.Network.Connected)
                {
                    // Full speed ahead; do not even attempt to grab a lock.
                    var LoggedOutEvent = new ManualResetEvent(false);
                    EventHandler<LoggedOutEventArgs> LoggedOutEventHandler = (sender, args) => LoggedOutEvent.Set();
                    Client.Network.LoggedOut += LoggedOutEventHandler;
                    Client.Network.BeginLogout();
                    if (!LoggedOutEvent.WaitOne((int) corradeConfiguration.LogoutGrace, false))
                    {
                        Client.Network.LoggedOut -= LoggedOutEventHandler;
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.TIMEOUT_LOGGING_OUT));
                    }
                    Client.Network.LoggedOut -= LoggedOutEventHandler;
                }
            }

            // Uninstall all installed handlers
            Client.Self.IM -= HandleSelfIM;
            Client.Network.SimChanged -= HandleRadarObjects;
            Client.Objects.AvatarUpdate -= HandleAvatarUpdate;
            Client.Objects.ObjectUpdate -= HandleObjectUpdate;
            Client.Objects.KillObject -= HandleKillObject;
            Client.Self.AnimationsChanged -= HandleAnimationsChanged;
            Client.Self.LoadURL -= HandleLoadURL;
            Client.Self.ScriptControlChange -= HandleScriptControlChange;
            Client.Self.MoneyBalanceReply -= HandleMoneyBalance;
            Client.Network.SimChanged -= HandleSimChanged;
            Client.Self.RegionCrossed -= HandleRegionCrossed;
            Client.Self.MeanCollision -= HandleMeanCollision;
            Client.Avatars.ViewerEffectLookAt -= HandleViewerEffect;
            Client.Avatars.ViewerEffectPointAt -= HandleViewerEffect;
            Client.Avatars.ViewerEffect -= HandleViewerEffect;
            Client.Objects.TerseObjectUpdate -= HandleTerseObjectUpdate;
            Client.Self.ScriptDialog -= HandleScriptDialog;
            Client.Objects.AvatarSitChanged -= HandleAvatarSitChanged;
            Client.Groups.GroupJoinedReply -= HandleGroupJoined;
            Client.Groups.GroupLeaveReply -= HandleGroupLeave;
            Client.Self.ChatFromSimulator -= HandleChatFromSimulator;
            Client.Self.MoneyBalance -= HandleMoneyBalance;
            Client.Self.AlertMessage -= HandleAlertMessage;
            Client.Self.ScriptQuestion -= HandleScriptQuestion;
            Client.Self.TeleportProgress -= HandleTeleportProgress;
            Client.Friends.FriendRightsUpdate -= HandleFriendRightsUpdate;
            Client.Friends.FriendOffline -= HandleFriendOnlineStatus;
            Client.Friends.FriendOnline -= HandleFriendOnlineStatus;
            Client.Friends.FriendshipResponse -= HandleFriendShipResponse;
            Client.Friends.FriendshipOffered -= HandleFriendshipOffered;
            Client.Network.EventQueueRunning -= HandleEventQueueRunning;
            Client.Network.SimDisconnected -= HandleSimulatorDisconnected;
            Client.Network.Disconnected -= HandleDisconnected;
            Client.Network.SimConnected -= HandleSimulatorConnected;
            Client.Network.LoginProgress -= HandleLoginProgress;
            Client.Network.LoggedOut -= HandleLoggedOut;
            Client.Appearance.AppearanceSet -= HandleAppearanceSet;
            Client.Inventory.InventoryObjectOffered -= HandleInventoryObjectOffered;
            Client.Sound.PreloadSound -= HandlePreloadSound;

            // Stop the sphere effects expiration timer.
            EffectsExpirationTimer.Stop();
            // Stop the group membership timer.
            GroupMembershipTimer.Stop();
            // Stop the group feed thread.
            GroupFeedsTimer.Stop();
            // Stop the group schedules timer.
            GroupSchedulesTimer.Stop();

            // Save group soft bans state.
            SaveGroupSoftBansState.Invoke();
            // Save conferences state.
            SaveConferenceState.Invoke();
            // Save feeds state.
            SaveGroupFeedState.Invoke();
            // Save notification states.
            SaveNotificationState.Invoke();
            // Save group members.
            SaveGroupMembersState.Invoke();
            // Save group schedules.
            SaveGroupSchedulesState.Invoke();
            // Save movement state.
            SaveMovementState.Invoke();
            // Save Corrade caches.
            SaveCorradeCache.Invoke();
            // Save Bayes classifications.
            SaveGroupBayesClassificiations.Invoke();
            // Save group cookies.
            SaveGroupCookiesState.Invoke();

            // Stop the notification thread.
            try
            {
                runNotificationThread = false;
                if (
                    NotificationThread.ThreadState.Equals(ThreadState.Running) ||
                    NotificationThread.ThreadState.Equals(ThreadState.WaitSleepJoin))
                {
                    if (!NotificationThread.Join(1000))
                    {
                        NotificationThread.Abort();
                        NotificationThread.Join();
                    }
                }
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            finally
            {
                NotificationThread = null;
            }

            // Stop the callback thread.
            try
            {
                runCallbackThread = false;
                if (
                    CallbackThread.ThreadState.Equals(ThreadState.Running) ||
                    CallbackThread.ThreadState.Equals(ThreadState.WaitSleepJoin))
                {
                    if (!CallbackThread.Join(1000))
                    {
                        CallbackThread.Abort();
                        CallbackThread.Join();
                    }
                }
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            finally
            {
                NotificationThread = null;
            }

            // Close HTTP server
            if (HttpListener.IsSupported && corradeConfiguration.EnableHTTPServer)
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.STOPPING_HTTP_SERVER));
                runHTTPServer = false;
                try
                {
                    if (HTTPListenerThread != null)
                    {
                        HTTPListener.Stop();
                        if (
                            HTTPListenerThread.ThreadState.Equals(ThreadState.Running) ||
                            HTTPListenerThread.ThreadState.Equals(ThreadState.WaitSleepJoin))
                        {
                            if (!HTTPListenerThread.Join(1000))
                            {
                                HTTPListenerThread.Abort();
                                HTTPListenerThread.Join();
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    /* We are going down and we do not care. */
                }
                finally
                {
                    HTTPListenerThread = null;
                }
            }

            // Terminate.
            Environment.Exit(corradeConfiguration.ExitCodeExpected);
        }

        private void HandlePreloadSound(object sender, PreloadSoundEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Preload, e),
                corradeConfiguration.MaximumNotificationThreads);

            // Start a thread to download the sound if it is not already cached.
            new Thread(() =>
            {
                lock (Locks.ClientInstanceAssetsLock)
                {
                    if (Client.Assets.Cache.HasAsset(e.SoundID))
                        return;
                }

                var RequestAssetEvent = new ManualResetEvent(false);
                byte[] assetData = null;
                var succeeded = false;
                lock (Locks.ClientInstanceAssetsLock)
                {
                    Client.Assets.RequestAsset(e.SoundID, AssetType.Sound, true,
                        delegate(AssetDownload transfer, Asset asset)
                        {
                            if (!transfer.AssetID.Equals(e.SoundID)) return;
                            succeeded = transfer.Success;
                            if (transfer.Success)
                            {
                                assetData = asset.AssetData;
                            }
                            RequestAssetEvent.Set();
                        });
                    if (
                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.TIMEOUT_DOWNLOADING_PRELOAD_SOUND));
                    }
                }
                if (succeeded)
                {
                    lock (Locks.ClientInstanceAssetsLock)
                    {
                        Client.Assets.Cache.SaveAssetToCache(e.SoundID, assetData);
                    }
                    if (corradeConfiguration.EnableHorde)
                        HordeDistributeCacheAsset(e.SoundID, assetData,
                            Configuration.HordeDataSynchronizationOption.Add);
                }
            }) {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HandleAvatarUpdate(object sender, AvatarUpdateEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.RadarAvatars, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleObjectUpdate(object sender, PrimEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.RadarPrimitives, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleKillObject(object sender, KillObjectEventArgs e)
        {
            Primitive primitive;
            lock (RadarObjectsLock)
            {
                if (!RadarObjects.TryGetValue(e.ObjectLocalID, out primitive)) return;
            }
            switch (primitive is Avatar)
            {
                case true:
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.RadarAvatars, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                default:
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.RadarPrimitives, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
            }
        }

        private static void HandleGroupJoined(object sender, GroupOperationEventArgs e)
        {
            // Add the group to the cache.
            Cache.AddCurrentGroup(e.GroupID);

            // Join group chat if possible.
            if (!Client.Self.GroupChatSessions.ContainsKey(e.GroupID) &&
                Services.HasGroupPowers(Client, Client.Self.AgentID, e.GroupID, GroupPowers.JoinChat,
                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                    new DecayingAlarm(corradeConfiguration.DataDecayType)))
            {
                Services.JoinGroupChat(Client, e.GroupID, corradeConfiguration.ServicesTimeout);
            }
        }

        private static void HandleGroupLeave(object sender, GroupOperationEventArgs e)
        {
            // Remove the group from the cache.
            Cache.CurrentGroupsCache.Remove(e.GroupID);
        }

        private static void HandleLoadURL(object sender, LoadUrlEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.LoadURL, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleScriptControlChange(object sender, ScriptControlEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.ScriptControl, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleAppearanceSet(object sender, AppearanceSetEventArgs e)
        {
            switch (e.Success)
            {
                case true:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.APPEARANCE_SET_SUCCEEDED));
                    break;
                default:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.APPEARANCE_SET_FAILED));
                    break;
            }
        }

        private static void HandleRegionCrossed(object sender, RegionCrossedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.RegionCrossed, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMeanCollision(object sender, MeanCollisionEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.MeanCollision, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleViewerEffect(object sender, object e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.ViewerEffect, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        /// <summary>
        ///     Processes HTTP POST web-requests.
        /// </summary>
        /// <param name="ar">the async HTTP listener object</param>
        private static void ProcessHTTPRequest(IAsyncResult ar)
        {
            // We need to grab the context and everything else outside of the main request.
            HttpListenerContext httpContext = null;
            HttpListenerRequest httpRequest;
            byte[] requestData;
            // Now grab the message and check that the group is set or abandon.
            try
            {
                var httpListener = (HttpListener) ar.AsyncState;
                // bail if we are not listening
                if (httpListener == null || !httpListener.IsListening) return;
                httpContext = httpListener.EndGetContext(ar);
                if (httpContext.Request == null) throw new HTTPCommandException();
                httpRequest = httpContext.Request;
                // only accept connected remote endpoints
                if (httpRequest.RemoteEndPoint == null) throw new HTTPCommandException();
                // Retrieve the message sent even if it is a compressed stream.
                switch (httpRequest.ContentEncoding.EncodingName.ToLower())
                {
                    case "gzip":
                        using (var inputStream = new MemoryStream())
                        {
                            using (var dataGZipStream = new GZipStream(httpRequest.InputStream,
                                CompressionMode.Decompress, false))
                            {
                                dataGZipStream.CopyTo(inputStream);
                                //dataGZipStream.Flush();
                            }
                            requestData = inputStream.ToArray();
                        }
                        break;
                    case "deflate":
                        using (var inputStream = new MemoryStream())
                        {
                            using (
                                var dataDeflateStream = new DeflateStream(httpRequest.InputStream,
                                    CompressionMode.Decompress, false))
                            {
                                dataDeflateStream.CopyTo(inputStream);
                                //dataDeflateStream.Flush();
                            }
                            requestData = inputStream.ToArray();
                        }
                        break;
                    default:
                        using (var inputStream = new MemoryStream())
                        {
                            using (var reader = new BinaryReader(httpRequest.InputStream, httpRequest.ContentEncoding))
                            {
                                reader.BaseStream.CopyTo(inputStream);
                            }
                            requestData = inputStream.ToArray();
                        }
                        break;
                }
            }
            catch (HTTPCommandException)
            {
                /* Close the connection and bail if the preconditions are not satisifed for running the command. */
                httpContext?.Response.Close();
                return;
            }
            catch (HttpListenerException)
            {
                /* This happens when the server goes down, so do not scare the user since it is completely harmelss. */
                return;
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.HTTP_SERVER_PROCESSING_ABORTED),
                    ex.Message);
                return;
            }

            switch (httpRequest.HttpMethod)
            {
                case WebRequestMethods.Http.Put: // Receive data sync pushes.

                    // Do not proceed if horde synchronization is not enabled.
                    if (!corradeConfiguration.EnableHorde)
                        break;

                    // Get the URL path.
                    var urlPath = httpRequest.Url.Segments.Select(o => o.Trim('/')).Skip(1).ToList();
                    if (!urlPath.Any())
                        break;

                    // If authentication is not enabled or the client has not sent any authentication then stop.
                    if (!corradeConfiguration.EnableHTTPServerAuthentication || !httpContext.Request.IsAuthenticated)
                    {
                        httpContext?.Response.Close();
                        return;
                    }

                    // Authenticate.
                    var identity = (HttpListenerBasicIdentity) httpContext.User.Identity;
                    if (!identity.Name.Equals(corradeConfiguration.HTTPServerUsername, StringComparison.Ordinal) ||
                        !identity.Password.Equals(corradeConfiguration.HTTPServerPassword,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        httpContext?.Response.Close();
                        return;
                    }

                    // Find peer from shared secret.
                    string sharedSecretKey;
                    try
                    {
                        sharedSecretKey =
                            httpRequest.Headers.AllKeys.AsParallel()
                                .SingleOrDefault(
                                    o =>
                                        o.Equals(CORRADE_CONSTANTS.HORDE_SHARED_SECRET_HEADER, StringComparison.Ordinal));
                        if (string.IsNullOrEmpty(sharedSecretKey))
                            break;
                    }
                    catch (Exception)
                    {
                        break;
                    }

                    // Find the horde peer.
                    Configuration.HordePeer hordePeer;
                    try
                    {
                        hordePeer =
                            corradeConfiguration.HordePeers.AsParallel()
                                .SingleOrDefault(
                                    o =>
                                        o.SharedSecret.Equals(
                                            Encoding.UTF8.GetString(
                                                Convert.FromBase64String(httpRequest.Headers[sharedSecretKey])),
                                            StringComparison.Ordinal));
                        if (hordePeer == null || hordePeer.Equals(default(Configuration.HordePeer)))
                            break;
                    }
                    catch (Exception)
                    {
                        break;
                    }

                    Configuration.HordeDataSynchronization dataSynchronizationType;
                    Configuration.HordeDataSynchronizationOption dataSynchronizationOption;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.WebResource>(urlPath[0].ToLowerInvariant())
                        )
                    {
                        case Enumerations.WebResource.CACHE: /* /cache/{asset}/add | /cache/{asset}/remove */

                            // Break if the cache request is incompatible with the cache web resource.
                            if (urlPath.Count < 3)
                                break;

                            dataSynchronizationType =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                                    urlPath[1].ToLowerInvariant());

                            // Log the attempt to put cache objects.
                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            // If this synchronization is not allowed with this peer, then break.
                            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                                break;

                            // Storage
                            UUID groupUUID;

                            // Now attempt to add the asset to the cache.
                            switch (dataSynchronizationType)
                            {
                                case Configuration.HordeDataSynchronization.Region:
                                case Configuration.HordeDataSynchronization.Agent:
                                case Configuration.HordeDataSynchronization.Group:

                                    // Get the synchronization option.
                                    dataSynchronizationOption =
                                        Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                            urlPath[2].ToLowerInvariant());

                                    // If this synchronization option is not allowed with this peer, then break.
                                    if (
                                        !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                            dataSynchronizationOption))
                                        break;

                                    try
                                    {
                                        using (
                                            var stringReader =
                                                new StringReader(httpRequest.ContentEncoding.GetString(requestData)))
                                        {
                                            switch (dataSynchronizationType)
                                            {
                                                case Configuration.HordeDataSynchronization.Region:
                                                    var region = (Cache.Region)
                                                        new XmlSerializer(typeof(Cache.Region)).Deserialize(
                                                            stringReader);
                                                    switch (dataSynchronizationOption)
                                                    {
                                                        case Configuration.HordeDataSynchronizationOption.Add:
                                                            Cache.UpdateRegion(region.Name, region.Handle);
                                                            break;
                                                        case Configuration.HordeDataSynchronizationOption.Remove:
                                                            Cache.RemoveRegion(region.Name, region.Handle);
                                                            break;
                                                    }
                                                    break;
                                                case Configuration.HordeDataSynchronization.Agent:
                                                    var agent = (Cache.Agent)
                                                        new XmlSerializer(typeof(Cache.Agent)).Deserialize(stringReader);
                                                    switch (dataSynchronizationOption)
                                                    {
                                                        case Configuration.HordeDataSynchronizationOption.Add:
                                                            Cache.AddAgent(agent.FirstName, agent.LastName, agent.UUID);
                                                            break;
                                                        case Configuration.HordeDataSynchronizationOption.Remove:
                                                            Cache.RemoveAgent(agent.FirstName, agent.LastName,
                                                                agent.UUID);
                                                            break;
                                                    }
                                                    break;
                                                case Configuration.HordeDataSynchronization.Group:
                                                    var group = (Cache.Group)
                                                        new XmlSerializer(typeof(Cache.Group)).Deserialize(stringReader);
                                                    switch (dataSynchronizationOption)
                                                    {
                                                        case Configuration.HordeDataSynchronizationOption.Add:
                                                            Cache.AddGroup(group.Name, group.UUID);
                                                            break;
                                                        case Configuration.HordeDataSynchronizationOption.Remove:
                                                            Cache.RemoveGroup(group.Name, group.UUID);
                                                            break;
                                                    }
                                                    break;
                                            }
                                            Feedback(
                                                CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                                Reflection.GetNameFromEnumValue(dataSynchronizationType));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                                            Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                            ex.Message);
                                    }
                                    break;
                                case Configuration.HordeDataSynchronization.Asset:
                                    /* /cache/asset/add/UUID | /cache/asset/remove/UUID */

                                    // Invalid request.
                                    if (!urlPath.Count.Equals(4))
                                        break;

                                    // Get the synchronization option.
                                    dataSynchronizationOption =
                                        Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                            urlPath[2].ToLowerInvariant());

                                    // If this synchronization option is not allowed with this peer, then break.
                                    if (
                                        !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                            dataSynchronizationOption))
                                        break;

                                    // Invalid asset UUID.
                                    UUID assetUUID;
                                    if (!UUID.TryParse(urlPath[3], out assetUUID))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        switch (dataSynchronizationOption)
                                        {
                                            case Configuration.HordeDataSynchronizationOption.Add:
                                                lock (Locks.ClientInstanceAssetsLock)
                                                {
                                                    if (!Client.Assets.Cache.HasAsset(assetUUID))
                                                    {
                                                        Client.Assets.Cache.SaveAssetToCache(assetUUID, requestData);
                                                        HordeDistributeCacheAsset(assetUUID, requestData,
                                                            Configuration.HordeDataSynchronizationOption.Add);
                                                    }
                                                }
                                                break;
                                            case Configuration.HordeDataSynchronizationOption.Remove:
                                                bool hasAsset;
                                                lock (Locks.ClientInstanceAssetsLock)
                                                {
                                                    hasAsset = Client.Assets.Cache.HasAsset(assetUUID);
                                                }
                                                if (hasAsset)
                                                {
                                                    var fileName = Client.Assets.Cache.AssetFileName(assetUUID);
                                                    File.Delete(Path.Combine(Client.Settings.ASSET_CACHE_DIR, fileName));
                                                    HordeDistributeCacheAsset(assetUUID, requestData,
                                                        Configuration.HordeDataSynchronizationOption.Remove);
                                                }
                                                break;
                                        }
                                        Feedback(
                                            CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint +
                                            ")",
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                            Reflection.GetNameFromEnumValue(dataSynchronizationType));
                                    }
                                    catch (Exception ex)
                                    {
                                        Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                                            Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                            ex.Message);
                                    }
                                    break;
                            }
                            break;
                        case Enumerations.WebResource.MUTE: /* /mute/add | /mute/remove */

                            // Break if the mute request is incompatible with the mute web resource.
                            if (urlPath.Count < 2)
                                break;

                            dataSynchronizationType =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                                    urlPath[0].ToLowerInvariant());

                            // Log the attempt to put cache objects.
                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            // If this synchronization is not allowed with this peer, then break.
                            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                                break;

                            // Get the synchronization option.
                            dataSynchronizationOption =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                    urlPath[1].ToLowerInvariant());

                            // If this synchronization option is not allowed with this peer, then break.
                            if (
                                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                    dataSynchronizationOption))
                                break;

                            MuteEntry mute;
                            try
                            {
                                using (
                                    var stringReader =
                                        new StringReader(httpRequest.ContentEncoding.GetString(requestData)))
                                {
                                    mute = (MuteEntry)
                                        new XmlSerializer(typeof(MuteEntry)).Deserialize(stringReader);
                                }
                            }
                            catch (Exception ex)
                            {
                                Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                    ex.Message);
                                break;
                            }

                            // The currently active mutes.
                            var mutes = Enumerable.Empty<MuteEntry>();
                            bool mutesRetrieved;
                            switch (Cache.MuteCache.IsVirgin)
                            {
                                case true:
                                    mutesRetrieved = Services.GetMutes(Client, corradeConfiguration.ServicesTimeout,
                                        ref mutes);
                                    break;
                                default:
                                    mutes = Cache.MuteCache.AsEnumerable();
                                    mutesRetrieved = true;
                                    break;
                            }

                            if (!mutesRetrieved)
                                break;

                            var muteExists =
                                mutes.ToList().AsParallel().Any(o => o.ID.Equals(mute.ID) && o.Name.Equals(mute.Name));

                            switch (dataSynchronizationOption)
                            {
                                case Configuration.HordeDataSynchronizationOption.Add:
                                    // Check that the mute entry does not already exist
                                    if (muteExists)
                                        break;

                                    // Add the mute.
                                    var MuteListUpdatedEvent = new ManualResetEvent(false);
                                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                                        (sender, args) => MuteListUpdatedEvent.Set();

                                    lock (Locks.ClientInstanceSelfLock)
                                    {
                                        Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                        Client.Self.UpdateMuteListEntry(mute.Type, mute.ID, mute.Name, mute.Flags);
                                        if (
                                            !MuteListUpdatedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                false))
                                        {
                                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                            throw new ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_UPDATING_MUTE_LIST);
                                        }
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                    }

                                    // Add the mute to the cache.
                                    Cache.AddMute(mute.Flags, mute.ID, mute.Name, mute.Type);
                                    break;
                                case Configuration.HordeDataSynchronizationOption.Remove:
                                    // If the mute does not exist then we have nothing to do.
                                    if (!muteExists)
                                        break;
                                    Cache.RemoveMute(mute.Flags, mute.ID, mute.Name, mute.Type);
                                    break;
                            }

                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            break;
                        case Enumerations.WebResource.SOFTBAN:
                            /* /softban/add/<Group UUID> /softban/remove/<Group UUID> */

                            // Break if the mute request is incompatible with the mute web resource.
                            if (urlPath.Count < 3)
                                break;

                            dataSynchronizationType =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                                    urlPath[0].ToLowerInvariant());

                            // Log the attempt to put cache objects.
                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            // If this synchronization is not allowed with this peer, then break.
                            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                                break;

                            // Get the synchronization option.
                            dataSynchronizationOption =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                    urlPath[1].ToLowerInvariant());

                            // If this synchronization option is not allowed with this peer, then break.
                            if (
                                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                    dataSynchronizationOption))
                                break;

                            // Invalid group UUID or group is not a configured group.
                            if (!UUID.TryParse(urlPath[2], out groupUUID) ||
                                !corradeConfiguration.Groups.AsParallel().Any(o => o.UUID.Equals(groupUUID)))
                                break;

                            SoftBan softBan;
                            try
                            {
                                using (
                                    var stringReader =
                                        new StringReader(httpRequest.ContentEncoding.GetString(requestData)))
                                {
                                    softBan = (SoftBan)
                                        new XmlSerializer(typeof(SoftBan)).Deserialize(stringReader);
                                }
                            }
                            catch (Exception ex)
                            {
                                Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                    ex.Message);
                                break;
                            }

                            // Invalid soft ban.
                            if (softBan.Equals(default(SoftBan)))
                                break;

                            var groupSoftBansModified = false;
                            switch (dataSynchronizationOption)
                            {
                                case Configuration.HordeDataSynchronizationOption.Add:
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (!GroupSoftBans.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupSoftBans.Add(groupUUID,
                                                    new Collections.ObservableHashSet<SoftBan>());
                                                GroupSoftBans[groupUUID].CollectionChanged += HandleGroupSoftBansChanged;
                                                GroupSoftBans[groupUUID].Add(softBan);
                                                groupSoftBansModified = true;
                                                break;
                                            default:
                                                if (
                                                    GroupSoftBans[groupUUID].AsParallel()
                                                        .Any(o => o.Agent.Equals(softBan.Agent)))
                                                    break;
                                                GroupSoftBans[groupUUID].Add(softBan);
                                                groupSoftBansModified = true;
                                                break;
                                        }
                                    }
                                    break;
                                case Configuration.HordeDataSynchronizationOption.Remove:
                                    lock (GroupSoftBansLock)
                                    {
                                        if (GroupSoftBans.ContainsKey(groupUUID) &&
                                            GroupSoftBans[groupUUID].AsParallel()
                                                .Any(o => o.Agent.Equals(softBan.Agent)))
                                        {
                                            GroupSoftBans[groupUUID].RemoveWhere(o => o.Agent.Equals(softBan.Agent));
                                            groupSoftBansModified = true;
                                        }
                                    }
                                    break;
                            }

                            if (groupSoftBansModified)
                                SaveGroupSoftBansState.Invoke();

                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            break;
                        case Enumerations.WebResource.USER:
                            /* /user/add/<Group UUID> /user/remove/<Group UUID> */
                            // Break if the mute request is incompatible with the mute web resource.
                            if (urlPath.Count < 3)
                                break;

                            dataSynchronizationType =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                                    urlPath[0].ToLowerInvariant());

                            // Log the attempt to put cache objects.
                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            // If this synchronization is not allowed with this peer, then break.
                            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                                break;

                            // Get the synchronization option.
                            dataSynchronizationOption =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                    urlPath[1].ToLowerInvariant());

                            // If this synchronization option is not allowed with this peer, then break.
                            if (
                                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                    dataSynchronizationOption))
                                break;

                            // Invalid UUID or group.
                            if (!UUID.TryParse(urlPath[2], out groupUUID))
                                break;

                            Configuration.Group configurationGroup;
                            try
                            {
                                using (
                                    var stringReader =
                                        new StringReader(httpRequest.ContentEncoding.GetString(requestData)))
                                {
                                    configurationGroup = (Configuration.Group)
                                        new XmlSerializer(typeof(Configuration.Group)).Deserialize(stringReader);
                                }
                            }
                            catch (Exception ex)
                            {
                                Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                    ex.Message);
                                break;
                            }

                            // Invalid configuration group.
                            if (configurationGroup == null || configurationGroup.Equals(default(Configuration.Group)))
                                break;

                            // Check that this is the group that is being pushed.
                            if (!groupUUID.Equals(configurationGroup.UUID))
                                break;

                            // Search the configuration for the pushed group.
                            var configuredGroup = corradeConfiguration.Groups.AsParallel()
                                .FirstOrDefault(o => o.UUID.Equals(configurationGroup.UUID));
                            var corradeConfigurationGroupsModified = false;
                            switch (dataSynchronizationOption)
                            {
                                case Configuration.HordeDataSynchronizationOption.Add:
                                    // If the configuration does not contain the group, then add the group.
                                    if (configuredGroup == null || configuredGroup.Equals(default(Configuration.Group)))
                                    {
                                        corradeConfiguration.Groups.Add(configurationGroup);
                                        corradeConfigurationGroupsModified = true;
                                    }
                                    break;
                                case Configuration.HordeDataSynchronizationOption.Remove:
                                    // If the configuration contains the group, then remove the configured group.
                                    if (configuredGroup != null && !configuredGroup.Equals(default(Configuration.Group)))
                                    {
                                        corradeConfiguration.Groups.Remove(configuredGroup);
                                        corradeConfigurationGroupsModified = true;
                                    }
                                    break;
                            }

                            // Save the configuration to the configuration file.
                            if (corradeConfigurationGroupsModified)
                            {
                                try
                                {
                                    lock (ConfigurationFileLock)
                                    {
                                        corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                            ref corradeConfiguration);
                                    }
                                }
                                catch (Exception)
                                {
                                    throw new ScriptException(Enumerations.ScriptError.UNABLE_TO_SAVE_CONFIGURATION);
                                }
                            }

                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            break;
                        case Enumerations.WebResource.BAYES:
                            /* /bayes/add/<Group UUID> /bayes/remove/<Group UUID> */
                            // Break if the bayes request is incompatible with the bayes web resource.
                            if (urlPath.Count < 3)
                                break;

                            dataSynchronizationType =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                                    urlPath[0].ToLowerInvariant());

                            // Log the attempt to put cache objects.
                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            // If this synchronization is not allowed with this peer, then break.
                            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                                break;

                            // Get the synchronization option.
                            dataSynchronizationOption =
                                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronizationOption>(
                                    urlPath[1].ToLowerInvariant());

                            // If this synchronization option is not allowed with this peer, then break.
                            if (
                                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                                    dataSynchronizationOption))
                                break;

                            // Invalid UUID or group.
                            if (!UUID.TryParse(urlPath[2], out groupUUID))
                                break;

                            var bayes = new BayesSimpleTextClassifier();
                            try
                            {
                                using (
                                    var stringReader =
                                        new StringReader(httpRequest.ContentEncoding.GetString(requestData)))
                                {
                                    bayes.ImportJsonData(stringReader.ReadToEnd());
                                }
                            }
                            catch (Exception ex)
                            {
                                Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                                    ex.Message);
                                break;
                            }

                            if (bayes == null)
                                break;

                            var bayesDataModified = false;
                            switch (dataSynchronizationOption)
                            {
                                case Configuration.HordeDataSynchronizationOption.Add:
                                    lock (GroupBayesClassifiersLock)
                                    {
                                        switch (GroupBayesClassifiers.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupBayesClassifiers[groupUUID] = bayes;
                                                bayesDataModified = true;
                                                break;
                                            default:
                                                GroupBayesClassifiers.Add(groupUUID, bayes);
                                                bayesDataModified = true;
                                                break;
                                        }
                                    }
                                    break;
                                case Configuration.HordeDataSynchronizationOption.Remove:
                                    lock (GroupBayesClassifiersLock)
                                    {
                                        if (GroupBayesClassifiers.ContainsKey(groupUUID))
                                        {
                                            GroupBayesClassifiers.Remove(groupUUID);
                                            bayesDataModified = true;
                                        }
                                    }
                                    break;
                            }

                            if (bayesDataModified)
                                SaveGroupBayesClassificiations.Invoke();

                            Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + httpRequest.RemoteEndPoint + ")",
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                                Reflection.GetNameFromEnumValue(dataSynchronizationType));

                            break;
                    }
                    httpContext?.Response.Close();
                    break;
                case WebRequestMethods.Http.Post: // Process commands.
                    // Get the message.
                    var message = httpRequest.ContentEncoding.GetString(requestData);

                    // ignore empty messages right-away.
                    if (string.IsNullOrEmpty(message))
                    {
                        httpContext?.Response.Close();
                        return;
                    }

                    var commandGroup = GetCorradeGroupFromMessage(message);
                    // do not process anything from unknown groups.
                    if (commandGroup == null || commandGroup.Equals(default(Configuration.Group)))
                    {
                        httpContext?.Response.Close();
                        return;
                    }

                    // We have the group so schedule the Corrade command though the group scheduler.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(() =>
                    {
                        try
                        {
                            var result = HandleCorradeCommand(message,
                                CORRADE_CONSTANTS.WEB_REQUEST,
                                httpRequest.RemoteEndPoint.ToString(), commandGroup);
                            using (var response = httpContext.Response)
                            {
                                // set the content type based on chosen output filers
                                switch (corradeConfiguration.OutputFilters.Last())
                                {
                                    case Configuration.Filter.RFC1738:
                                        response.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                                        break;
                                    default:
                                        response.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                                        break;
                                }
                                response.StatusCode = (int) HttpStatusCode.OK;
                                response.StatusDescription = "OK";
                                response.SendChunked = true;
                                switch (corradeConfiguration.HTTPServerKeepAlive)
                                {
                                    case true:
                                        response.ProtocolVersion = HttpVersion.Version11;
                                        break;
                                    default:
                                        response.ProtocolVersion = HttpVersion.Version10;
                                        response.KeepAlive = false;
                                        break;
                                }
                                var data =
                                    Encoding.UTF8.GetBytes(
                                        KeyValue.Encode(KeyValue.Escape(result, wasOutput)));
                                using (var outputStream = new MemoryStream())
                                {
                                    switch (corradeConfiguration.HTTPServerCompression)
                                    {
                                        case Configuration.HTTPCompressionMethod.GZIP:
                                            using (var dataGZipStream = new GZipStream(outputStream,
                                                CompressionMode.Compress, false))
                                            {
                                                dataGZipStream.Write(data, 0, data.Length);
                                                //dataGZipStream.Flush();
                                            }
                                            response.AddHeader("Content-Encoding", "gzip");
                                            data = outputStream.ToArray();
                                            break;
                                        case Configuration.HTTPCompressionMethod.DEFLATE:
                                            using (
                                                var dataDeflateStream = new DeflateStream(outputStream,
                                                    CompressionMode.Compress, false))
                                            {
                                                dataDeflateStream.Write(data, 0, data.Length);
                                                //dataDeflateStream.Flush();
                                            }
                                            response.AddHeader("Content-Encoding", "deflate");
                                            data = outputStream.ToArray();
                                            break;
                                        default:
                                            response.AddHeader("Content-Encoding", "UTF-8");
                                            break;
                                    }
                                }
                                using (var responseStream = response.OutputStream)
                                {
                                    using (var responseBinaryWriter = new BinaryWriter(responseStream))
                                    {
                                        responseBinaryWriter.Write(data);
                                    }
                                }
                            }
                        }
                        catch (HttpListenerException)
                        {
                            /* This happens when the server goes down, so do not scare the user since it is completely harmless. */
                        }
                        catch (Exception ex)
                        {
                            Feedback(
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.HTTP_SERVER_PROCESSING_ABORTED),
                                ex.Message);
                        }
                        finally
                        {
                            /* Close the connection. */
                            httpContext?.Response.Close();
                        }
                    }, corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                        corradeConfiguration.SchedulerExpiration);
                    break;
                default: // no other methods recognized
                    httpContext?.Response.Close();
                    break;
            }
        }

        /// <summary>
        ///     Sends a notification to each group with a configured and installed notification.
        /// </summary>
        /// <param name="notification">the notification to send</param>
        /// <param name="args">the event arguments</param>
        private static void SendNotification(Configuration.Notifications notification, object args)
        {
            // Create a list of groups that have the notification installed.
            HashSet<Notification> notifications;
            lock (GroupNotificationsLock)
            {
                if (!GroupNotificationsCache.TryGetValue(notification, out notifications) || !notifications.Any())
                    return;
            }

            // Find the notification action.
            var CorradeNotification = corradeNotifications[Reflection.GetNameFromEnumValue(notification)];
            if (CorradeNotification == null)
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.NOTIFICATION_ERROR),
                    Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.UNKNOWN_NOTIFICATION_TYPE),
                    Reflection.GetNameFromEnumValue(notification));
                return;
            }

            // For each group build the notification.
            notifications.AsParallel().ForAll(z =>
            {
                // Create the notification data storage for this notification.
                var notificationData = new Dictionary<string, string>();

                try
                {
                    CorradeNotification.Invoke(new NotificationParameters
                    {
                        Notification = z,
                        Event = args,
                        Type = notification
                    }, notificationData);
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.NOTIFICATION_ERROR),
                        ex.Message);
                    return;
                }

                // Do not send empty notifications.
                if (!notificationData.Any()) return;

                // Add the notification type.
                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE),
                    Reflection.GetNameFromEnumValue(notification));

                // Build the afterburn.
                if (z.Afterburn != null && z.Afterburn.Any())
                {
                    notificationData = notificationData.Concat(z.Afterburn).ToDictionary(o => o.Key, o => o.Value);
                }

                // Enqueue the notification for the group.
                if (z.NotificationURLDestination != null && z.NotificationURLDestination.Any())
                {
                    HashSet<string> URLdestinations;
                    if (z.NotificationURLDestination.TryGetValue(notification, out URLdestinations))
                    {
                        URLdestinations.AsParallel().ForAll(p =>
                        {
                            // Check that the notification queue is not already full.
                            switch (NotificationQueue.Count <= corradeConfiguration.NotificationQueueLength)
                            {
                                case true:
                                    NotificationQueue.Enqueue(new NotificationQueueElement
                                    {
                                        GroupUUID = z.GroupUUID,
                                        URL = p,
                                        message = KeyValue.Escape(notificationData, wasOutput)
                                    });
                                    break;
                                default:
                                    Feedback(
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ConsoleMessage.NOTIFICATION_THROTTLED));
                                    break;
                            }
                        });
                    }
                }

                // Enqueue the TCP notification for the group.
                if (z.NotificationTCPDestination != null && z.NotificationTCPDestination.Any())
                {
                    HashSet<IPEndPoint> TCPdestinations;
                    if (z.NotificationTCPDestination.TryGetValue(notification, out TCPdestinations))
                    {
                        TCPdestinations.AsParallel().ForAll(p =>
                        {
                            switch (
                                NotificationTCPQueue.Count <= corradeConfiguration.TCPNotificationQueueLength)
                            {
                                case true:
                                    NotificationTCPQueue.Enqueue(new NotificationTCPQueueElement
                                    {
                                        message = KeyValue.Escape(notificationData, wasOutput),
                                        IPEndPoint = p
                                    });
                                    break;
                                default:
                                    Feedback(
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ConsoleMessage.TCP_NOTIFICATION_THROTTLED));
                                    break;
                            }
                        });
                    }
                }
            });
        }

        private static void HandleScriptDialog(object sender, ScriptDialogEventArgs e)
        {
            var dialogUUID = UUID.Random();
            var scriptDialog = new ScriptDialog
            {
                Message = e.Message,
                Agent = new Agent
                {
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    UUID = e.OwnerID
                },
                Channel = e.Channel,
                Name = e.ObjectName,
                Item = e.ObjectID,
                Button = e.ButtonLabels,
                ID = dialogUUID
            };
            lock (ScriptDialogsLock)
            {
                ScriptDialogs.Add(dialogUUID, scriptDialog);
            }
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.ScriptDialog, scriptDialog),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleAvatarSitChanged(object sender, AvatarSitChangedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.SitChanged, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleSoundTrigger(object sender, SoundTriggerEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Sound, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleAnimationsChanged(object sender, AnimationsChangedEventArgs e)
        {
            lock (CurrentAnimationsLock)
            {
                if (!e.Animations.Copy().Except(CurrentAnimations).Any())
                    return;
                CurrentAnimations.Clear();
                CurrentAnimations.UnionWith(e.Animations.Copy());
            }
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.AnimationsChanged, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleChatFromSimulator(object sender, ChatEventArgs e)
        {
            // Check if message is from muted agent or object and ignore it.
            if (Cache.MuteCache.Any(o => o.ID.Equals(e.SourceID) || o.ID.Equals(e.OwnerID)))
                return;
            // Get the full name.
            var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(e.FromName));
            Configuration.Group commandGroup;
            switch (e.Type)
            {
                case ChatType.StartTyping:
                case ChatType.StopTyping:
                    Cache.AddAgent(fullName.First(), fullName.Last(), e.SourceID);
                    // Send typing notifications.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.Typing, new TypingEventArgs
                        {
                            Action = !e.Type.Equals(
                                ChatType.StartTyping)
                                ? Enumerations.Action.STOP
                                : Enumerations.Action.START,
                            AgentUUID = e.SourceID,
                            FirstName = fullName.First(),
                            LastName = fullName.Last(),
                            Entity = Enumerations.Entity.LOCAL
                        }),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                case ChatType.OwnerSay:
                    // If this is a message from an agent, add the agent to the cache.
                    if (e.SourceType.Equals(ChatSourceType.Agent))
                    {
                        Cache.AddAgent(fullName.First(), fullName.Last(), e.SourceID);
                    }
                    // If RLV is enabled, process RLV and terminate.
                    if (corradeConfiguration.EnableRLV &&
                        e.Message.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.COMMAND_OPERATOR))
                    {
                        // Send RLV message notifications.
                        CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                            () => SendNotification(Configuration.Notifications.RLVMessage, e),
                            corradeConfiguration.MaximumNotificationThreads);
                        CorradeThreadPool[Threading.Enumerations.ThreadType.RLV].SpawnSequential(
                            () =>
                                RLV.HandleRLVBehaviour(e.Message.Substring(1, e.Message.Length - 1), e.SourceID),
                            corradeConfiguration.MaximumRLVThreads, corradeConfiguration.ServicesTimeout);
                        break;
                    }
                    // If this is a Corrade command, process it and terminate.
                    if (Utilities.IsCorradeCommand(e.Message))
                    {
                        // If the group was not set properly, then bail.
                        commandGroup = GetCorradeGroupFromMessage(e.Message);
                        if (commandGroup == null || commandGroup.Equals(default(Configuration.Group)))
                            return;
                        // Spawn the command.
                        CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(
                            () => HandleCorradeCommand(e.Message, e.FromName, e.OwnerID.ToString(), commandGroup),
                            corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                            corradeConfiguration.SchedulerExpiration);
                        return;
                    }
                    // Otherwise, send llOwnerSay notifications.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.OwnerSay, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                case ChatType.Debug:
                    // Send debug notifications.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.DebugMessage, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                case ChatType.Normal:
                case ChatType.Shout:
                case ChatType.Whisper:
                    // If this is a message from an agent, add the agent to the cache.
                    if (e.SourceType.Equals(ChatSourceType.Agent))
                    {
                        Cache.AddAgent(fullName.First(), fullName.Last(), e.SourceID);
                    }
                    // Send chat notifications.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.LocalChat, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    // Log local chat if the message could be heard.
                    if (corradeConfiguration.LocalMessageLogEnabled && !string.IsNullOrEmpty(e.Message))
                    {
                        CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(() =>
                        {
                            try
                            {
                                lock (LocalLogFileLock)
                                {
                                    using (
                                        var fileStream =
                                            File.Open(Path.Combine(corradeConfiguration.LocalMessageLogDirectory,
                                                Client.Network.CurrentSim.Name) +
                                                      "." +
                                                      CORRADE_CONSTANTS.LOG_FILE_EXTENSION, FileMode.Append,
                                                FileAccess.Write, FileShare.None))
                                    {
                                        using (var logWriter = new StreamWriter(fileStream, Encoding.UTF8))
                                        {
                                            logWriter.WriteLine(CORRADE_CONSTANTS.LOCAL_MESSAGE_LOG_MESSAGE_FORMAT,
                                                DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                    Utils.EnUsCulture.DateTimeFormat),
                                                fullName.First(), fullName.Last(),
                                                Enum.GetName(typeof(ChatType), e.Type),
                                                e.Message);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // or fail and append the fail message.
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.COULD_NOT_WRITE_TO_LOCAL_MESSAGE_LOG_FILE),
                                    ex.Message);
                            }
                        }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                    }
                    break;
                case (ChatType) 9:
                    // Send llRegionSayTo notification in case we do not have a command.
                    if (!Utilities.IsCorradeCommand(e.Message))
                    {
                        // Send chat notifications.
                        CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                            () => SendNotification(Configuration.Notifications.RegionSayTo, e),
                            corradeConfiguration.MaximumNotificationThreads);
                        break;
                    }
                    // If the group was not set properly, then bail.
                    commandGroup = GetCorradeGroupFromMessage(e.Message);
                    if (commandGroup == null || commandGroup.Equals(default(Configuration.Group)))
                        return;

                    // Spawn the command.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(
                        () => HandleCorradeCommand(e.Message, e.FromName, e.OwnerID.ToString(), commandGroup),
                        corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                        corradeConfiguration.SchedulerExpiration);
                    break;
            }
        }

        private static void HandleAlertMessage(object sender, AlertMessageEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.AlertMessage, e),
                corradeConfiguration.MaximumNotificationThreads);
        }


        private static void HandleInventoryObjectAdded(object sender, InventoryObjectAddedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Store, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleInventoryObjectRemoved(object sender, InventoryObjectRemovedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Store, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleInventoryObjectUpdated(object sender, InventoryObjectUpdatedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Store, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleInventoryObjectOffered(object sender, InventoryObjectOfferedEventArgs e)
        {
            // We need to block until we get a reply from a script.
            var inventoryOffer = new InventoryOffer
            {
                Args = e,
                Event = new ManualResetEvent(false)
            };
            // Add the inventory offer to the list of inventory offers.
            lock (InventoryOffersLock)
            {
                InventoryOffers.Add(inventoryOffer.Args.Offer.IMSessionID, inventoryOffer);
            }

            // Accept anything from master avatars.
            if (
                corradeConfiguration.Masters.AsParallel().Select(
                    o => string.Format(Utils.EnUsCulture, "{0} {1}", o.FirstName, o.LastName))
                    .Any(p => Strings.StringEquals(e.Offer.FromAgentName, p, StringComparison.OrdinalIgnoreCase)))
            {
                e.Accept = true;
                // It is accepted, so update the inventory.
                InventoryNode node;
                // Find the node.
                lock (Locks.ClientInstanceInventoryLock)
                {
                    node = Client.Inventory.Store.GetNodeFor(e.FolderID.Equals(UUID.Zero)
                        ? Client.Inventory.FindFolderForType(e.AssetType)
                        : e.FolderID);
                }
                if (node != null)
                {
                    // Set the node to be updated.
                    node.NeedsUpdate = true;
                    // Update the inventory.
                    try
                    {
                        switch (node.Data is InventoryFolder)
                        {
                            case true:
                                Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store.RootFolder,
                                    corradeConfiguration.ServicesTimeout);
                                break;
                            default:
                                Inventory.UpdateInventoryRecursive(Client,
                                    Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)]
                                        .Data as InventoryFolder, corradeConfiguration.ServicesTimeout);
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                    }
                }
                // Send notification
                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                    () => SendNotification(Configuration.Notifications.Inventory, e),
                    corradeConfiguration.MaximumNotificationThreads);
                return;
            }

            // It is temporary, so update the inventory.
            lock (Locks.ClientInstanceInventoryLock)
            {
                Client.Inventory.Store.GetNodeFor(e.FolderID.Equals(UUID.Zero)
                    ? Client.Inventory.FindFolderForType(e.AssetType)
                    : e.FolderID).NeedsUpdate =
                    true;
            }

            // Find the item in the inventory.
            InventoryBase inventoryBaseItem = null;
            lock (Locks.ClientInstanceInventoryLock)
            {
                var itemUUID = new UUID(e.Offer.BinaryBucket, 1);
                if (Client.Inventory.Store.Contains(itemUUID))
                {
                    inventoryBaseItem = Client.Inventory.Store[itemUUID];
                }
            }

            if (inventoryBaseItem != null)
            {
                // Assume we do not want the item.
                lock (Locks.ClientInstanceInventoryLock)
                {
                    Client.Inventory.Move(
                        inventoryBaseItem,
                        Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.TrashFolder)].Data as
                            InventoryFolder);
                }
                lock (Locks.ClientInstanceInventoryLock)
                {
                    Client.Inventory.Store.GetNodeFor(inventoryBaseItem.ParentUUID).NeedsUpdate = true;
                    Client.Inventory.Store.GetNodeFor(Client.Inventory.FindFolderForType(AssetType.TrashFolder))
                        .NeedsUpdate = true;
                }
            }

            // Send notification
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Inventory, e),
                corradeConfiguration.MaximumNotificationThreads);

            // Wait for a reply.
            inventoryOffer.Event.WaitOne(Timeout.Infinite);

            if (inventoryBaseItem == null) return;

            var itemParentUUID = UUID.Zero;
            switch (inventoryBaseItem.ParentUUID.Equals(UUID.Zero))
            {
                case true:
                    UUID rootFolderUUID;
                    UUID libraryFolderUUID;
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        rootFolderUUID = Client.Inventory.Store.RootFolder.UUID;
                        libraryFolderUUID = Client.Inventory.Store.LibraryFolder.UUID;
                    }
                    if (inventoryBaseItem.UUID.Equals(rootFolderUUID))
                    {
                        itemParentUUID = rootFolderUUID;
                        break;
                    }
                    if (inventoryBaseItem.UUID.Equals(libraryFolderUUID))
                    {
                        itemParentUUID = libraryFolderUUID;
                    }
                    break;
                default:
                    itemParentUUID = inventoryBaseItem.ParentUUID;
                    break;
            }

            switch (e.Accept)
            {
                case false: // if the item is to be discarded, then remove the item from inventory
                    switch (inventoryBaseItem is InventoryFolder)
                    {
                        case true:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RemoveFolder(inventoryBaseItem.UUID);
                            }
                            break;
                        default:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RemoveItem(inventoryBaseItem.UUID);
                            }
                            break;
                    }
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.Store.GetNodeFor(itemParentUUID).NeedsUpdate = true;
                        Client.Inventory.Store.GetNodeFor(Client.Inventory.FindFolderForType(AssetType.TrashFolder))
                            .NeedsUpdate = true;
                    }
                    return;
            }

            // If no folder UUID was specified, move it to the default folder for the asset type.
            switch (!e.FolderID.Equals(UUID.Zero))
            {
                case true:
                    InventoryFolder inventoryFolder = null;
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        var node = Client.Inventory.Store.GetNodeFor(e.FolderID);
                        if (node != null)
                        {
                            inventoryFolder = node.Data as InventoryFolder;
                        }
                    }
                    if (inventoryFolder != null)
                    {
                        switch (inventoryBaseItem is InventoryFolder)
                        {
                            case true: // folders
                                // if a name was specified, rename the item as well.

                                switch (string.IsNullOrEmpty(inventoryOffer.Name))
                                {
                                    case false:
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            Client.Inventory.MoveFolder(inventoryBaseItem.UUID,
                                                inventoryFolder.UUID, inventoryOffer.Name);
                                        }
                                        break;
                                    default:
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            Client.Inventory.MoveFolder(inventoryBaseItem.UUID,
                                                inventoryFolder.UUID);
                                        }
                                        break;
                                }
                                break;
                            default: // all other items
                                switch (string.IsNullOrEmpty(inventoryOffer.Name))
                                {
                                    case false:
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            Client.Inventory.Move(inventoryBaseItem, inventoryFolder,
                                                inventoryOffer.Name);
                                            Client.Inventory.RequestUpdateItem(inventoryBaseItem as InventoryItem);
                                        }
                                        break;
                                    default:
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            Client.Inventory.Move(inventoryBaseItem, inventoryFolder);
                                        }
                                        break;
                                }
                                break;
                        }
                        lock (Locks.ClientInstanceInventoryLock)
                        {
                            Client.Inventory.Store.GetNodeFor(itemParentUUID).NeedsUpdate = true;
                            Client.Inventory.Store.GetNodeFor(inventoryFolder.UUID).NeedsUpdate = true;
                        }
                    }
                    break;
                default:
                    switch (inventoryBaseItem is InventoryFolder)
                    {
                        case true: // move inventory folders into the root
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                // if a name was specified, rename the item as well.
                                switch (string.IsNullOrEmpty(inventoryOffer.Name))
                                {
                                    case false:
                                        Client.Inventory.MoveFolder(
                                            inventoryBaseItem.UUID, Client.Inventory.Store.RootFolder.UUID,
                                            inventoryOffer.Name);
                                        break;
                                    default:
                                        Client.Inventory.MoveFolder(
                                            inventoryBaseItem.UUID, Client.Inventory.Store.RootFolder.UUID);
                                        break;
                                }
                                Client.Inventory.Store.GetNodeFor(itemParentUUID).NeedsUpdate = true;
                                Client.Inventory.Store.GetNodeFor(Client.Inventory.Store.RootFolder.UUID).NeedsUpdate =
                                    true;
                            }
                            break;
                        default:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                InventoryFolder destinationFolder = null;
                                var node =
                                    Client.Inventory.Store.GetNodeFor(Client.Inventory.FindFolderForType(e.AssetType));
                                if (node != null)
                                {
                                    destinationFolder = node.Data as InventoryFolder;
                                }
                                if (destinationFolder != null)
                                {
                                    switch (string.IsNullOrEmpty(inventoryOffer.Name))
                                    {
                                        case false:
                                            Client.Inventory.Move(inventoryBaseItem, destinationFolder,
                                                inventoryOffer.Name);
                                            Client.Inventory.RequestUpdateItem(inventoryBaseItem as InventoryItem);
                                            break;
                                        default:
                                            Client.Inventory.Move(inventoryBaseItem, destinationFolder);
                                            break;
                                    }
                                    Client.Inventory.Store.GetNodeFor(itemParentUUID).NeedsUpdate = true;
                                    Client.Inventory.Store.GetNodeFor(destinationFolder.UUID).NeedsUpdate = true;
                                }
                            }
                            break;
                    }
                    break;
            }
            try
            {
                switch (inventoryBaseItem is InventoryFolder)
                {
                    case true:
                        Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store.RootFolder,
                            corradeConfiguration.ServicesTimeout);
                        break;
                    default:
                        Inventory.UpdateInventoryRecursive(Client,
                            Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)]
                                .Data as InventoryFolder, corradeConfiguration.ServicesTimeout);
                        break;
                }
            }
            catch (Exception)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
            }
        }

        private static void HandleScriptQuestion(object sender, ScriptQuestionEventArgs e)
        {
            var owner = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(e.ObjectOwnerName));
            var ownerUUID = UUID.Zero;
            // Don't add permission requests from unknown agents.
            if (
                !Resolvers.AgentNameToUUID(Client, owner.First(), owner.Last(), corradeConfiguration.ServicesTimeout,
                    corradeConfiguration.DataTimeout,
                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                    ref ownerUUID))
            {
                return;
            }

            lock (ScriptPermissionsRequestsLock)
            {
                ScriptPermissionRequests.Add(new ScriptPermissionRequest
                {
                    Name = e.ObjectName,
                    Agent = new Agent
                    {
                        FirstName = owner.First(),
                        LastName = owner.Last(),
                        UUID = ownerUUID
                    },
                    Item = e.ItemID,
                    Task = e.TaskID,
                    Permission = e.Questions,
                    Region = e.Simulator.Name
                });
            }
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.ScriptPermission, e),
                corradeConfiguration.MaximumNotificationThreads);

            // Handle RLV: acceptpermission
            lock (RLV.RLVRulesLock)
            {
                if (
                    !RLVRules.AsParallel().Any(o =>
                        o.Behaviour.Equals(
                            Reflection.GetNameFromEnumValue(RLV.RLVBehaviour.ACCEPTPERMISSION))))
                    return;
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.ScriptQuestionReply(e.Simulator, e.ItemID, e.TaskID, e.Questions);
                }
            }
        }

        private static void HandleDisconnected(object sender, DisconnectedEventArgs e)
        {
            Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.DISCONNECTED));
            ConnectionSemaphores['l'].Set();
        }

        private static void HandleEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.EVENT_QUEUE_STARTED));
        }

        private static void HandleSimulatorConnected(object sender, SimConnectedEventArgs e)
        {
            Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.SIMULATOR_CONNECTED));
        }

        private static void HandleSimulatorDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            // if any simulators are still connected, we are not disconnected
            lock (Locks.ClientInstanceNetworkLock)
            {
                if (Client.Network.Simulators.Any()) return;
            }
            Feedback(
                Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.ALL_SIMULATORS_DISCONNECTED));
            ConnectionSemaphores['s'].Set();
        }

        private static void HandleLoggedOut(object sender, LoggedOutEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Login, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleLoginProgress(object sender, LoginProgressEventArgs e)
        {
            // Send the notification.
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Login, e),
                corradeConfiguration.MaximumNotificationThreads);

            switch (e.Status)
            {
                case LoginStatus.Success:
                    // Login succeeded so start all the updates.
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.LOGIN_SUCCEEDED));
                    // Load movement state.
                    LoadMovementState.Invoke();
                    // Start thread and wait on caps to restore conferences.
                    new Thread(() =>
                    {
                        var EventQueueRunningEvent = new ManualResetEvent(false);
                        EventHandler<EventQueueRunningEventArgs> EventQueueRunningHandler =
                            (o, p) => { EventQueueRunningEvent.Set(); };
                        Client.Network.EventQueueRunning += EventQueueRunningHandler;
                        if (EventQueueRunningEvent.WaitOne(Timeout.Infinite, false))
                        {
                            // Load conference state.
                            LoadConferenceState.Invoke();
                        }
                        Client.Network.EventQueueRunning -= EventQueueRunningHandler;
                    })
                    {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
                    // Start inventory update thread.
                    new Thread(() =>
                    {
                        // First load the caches.
                        LoadInventoryCache.Invoke();
                        // Update the inventory.
                        try
                        {
                            // Update the inventory.
                            Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store.RootFolder,
                                corradeConfiguration.ServicesTimeout);

                            // Update the library.
                            Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store.LibraryFolder,
                                corradeConfiguration.ServicesTimeout);

                            // Get COF.
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                CurrentOutfitFolder =
                                    Client.Inventory.Store[
                                        Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)
                                        ] as
                                        InventoryFolder;
                            }
                        }
                        catch (Exception)
                        {
                            Feedback(
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                        }
                        // Now save the caches.
                        SaveInventoryCache.Invoke();

                        // Bind to the inventory store notifications if enabled.
                        if (Client.Inventory.Store != null)
                        {
                            switch (
                                corradeConfiguration.Groups.AsParallel()
                                    .Any(
                                        o => o.NotificationMask.IsMaskFlagSet(Configuration.Notifications.Store))
                                )
                            {
                                case true:
                                    Client.Inventory.Store.InventoryObjectAdded += HandleInventoryObjectAdded;
                                    Client.Inventory.Store.InventoryObjectRemoved += HandleInventoryObjectRemoved;
                                    Client.Inventory.Store.InventoryObjectUpdated += HandleInventoryObjectUpdated;
                                    break;
                                default:
                                    Client.Inventory.Store.InventoryObjectAdded -= HandleInventoryObjectAdded;
                                    Client.Inventory.Store.InventoryObjectRemoved -= HandleInventoryObjectRemoved;
                                    Client.Inventory.Store.InventoryObjectUpdated -= HandleInventoryObjectUpdated;
                                    break;
                            }
                        }
                    })
                    {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
                    // Set current group to land group.
                    new Thread(() =>
                    {
                        if (!corradeConfiguration.AutoActivateGroup) return;
                        ActivateCurrentLandGroupTimer.Change(corradeConfiguration.AutoActivateGroupDelay, 0);
                    })
                    {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
                    // Retrieve instant messages.
                    new Thread(() =>
                    {
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.RetrieveInstantMessages();
                        }
                    })
                    {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
                    // Request the mute list.
                    new Thread(() =>
                    {
                        var mutes = Enumerable.Empty<MuteEntry>();
                        if (!Services.GetMutes(Client, corradeConfiguration.ServicesTimeout, ref mutes))
                            return;
                        Cache.MuteCache.UnionWith(mutes);
                    })
                    {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();

                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );

                    break;
                case LoginStatus.Failed:
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.LOGIN_FAILED),
                        e.FailReason,
                        e.Message);
                    // Login failed, so trying the next start location...
                    var location = corradeConfiguration.StartLocations.ElementAtOrDefault(LoginLocationIndex++);
                    // We have exceeded the configured locations so raise the semaphore and abort.
                    if (string.IsNullOrEmpty(location))
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.COULD_NOT_CONNECT_TO_ANY_SIMULATOR));
                        ConnectionSemaphores['l'].Set();
                        break;
                    }
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.CYCLING_SIMULATORS));
                    var startLocation = new
                        wasOpenMetaverse.Helpers.StartLocationParser(location);
                    Login.Start = startLocation.isCustom
                        ? NetworkManager.StartLocation(startLocation.Sim, startLocation.X, startLocation.Y,
                            startLocation.Z)
                        : location;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        Client.Network.BeginLogin(Login);
                    }
                    break;
                case LoginStatus.ConnectingToLogin:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.CONNECTING_TO_LOGIN_SERVER));
                    break;
                case LoginStatus.Redirecting:
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.REDIRECTING));
                    break;
                case LoginStatus.ReadingResponse:
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.READING_RESPONSE));
                    break;
                case LoginStatus.ConnectingToSim:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.CONNECTING_TO_SIMULATOR));
                    break;
            }
        }

        private static void HandleFriendOnlineStatus(object sender, FriendInfoEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendRightsUpdate(object sender, FriendInfoEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendShipResponse(object sender, FriendshipResponseEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendshipOffered(object sender, FriendshipOfferedEventArgs e)
        {
            // Send friendship notifications
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleTeleportProgress(object sender, TeleportEventArgs e)
        {
            // Send teleport notifications
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Teleport, e),
                corradeConfiguration.MaximumNotificationThreads);

            switch (e.Status)
            {
                case TeleportStatus.Finished:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.TELEPORT_SUCCEEDED));
                    // Set current group to land group.
                    if (corradeConfiguration.AutoActivateGroup)
                    {
                        new Thread(
                            () =>
                            {
                                ActivateCurrentLandGroupTimer.Change(corradeConfiguration.AutoActivateGroupDelay, 0);
                            })
                        {IsBackground = true}.Start();
                    }
                    break;
                case TeleportStatus.Failed:
                    Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.TELEPORT_FAILED));
                    break;
            }
        }

        private static void HandleSelfIM(object sender, InstantMessageEventArgs args)
        {
            // Check if message is from muted agent and ignore it.
            if (Cache.MuteCache.Any(o => o.ID.Equals(args.IM.FromAgentID)))
                return;
            var fullName =
                new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(args.IM.FromAgentName));
            // Process dialog messages.
            switch (args.IM.Dialog)
            {
                // Send typing notification.
                case InstantMessageDialog.StartTyping:
                case InstantMessageDialog.StopTyping:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.Typing, new TypingEventArgs
                        {
                            Action = !args.IM.Dialog.Equals(
                                InstantMessageDialog.StartTyping)
                                ? Enumerations.Action.STOP
                                : Enumerations.Action.START,
                            AgentUUID = args.IM.FromAgentID,
                            FirstName = fullName.First(),
                            LastName = fullName.Last(),
                            Entity = Enumerations.Entity.MESSAGE
                        }),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.FriendshipOffered:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    // Accept friendships only from masters (for the time being)
                    if (
                        !corradeConfiguration.Masters.AsParallel().Any(
                            o =>
                                Strings.StringEquals(fullName.First(), o.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                Strings.StringEquals(fullName.Last(), o.LastName, StringComparison.OrdinalIgnoreCase)))
                        return;
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.ACCEPTED_FRIENDSHIP),
                        args.IM.FromAgentName);
                    Client.Friends.AcceptFriendship(args.IM.FromAgentID, args.IM.IMSessionID);
                    break;
                case InstantMessageDialog.InventoryAccepted:
                case InstantMessageDialog.InventoryDeclined:
                case InstantMessageDialog.TaskInventoryOffered:
                case InstantMessageDialog.InventoryOffered:
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.Inventory, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.MessageBox:
                    // Not used.
                    return;
                case InstantMessageDialog.RequestTeleport:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    // Handle RLV: acccepttp
                    lock (RLV.RLVRulesLock)
                    {
                        if (RLVRules.AsParallel().Any(o =>
                            o.Behaviour.Equals(Reflection.GetNameFromEnumValue(RLV.RLVBehaviour.ACCEPTTP))))
                        {
                            if (wasOpenMetaverse.Helpers.IsSecondLife(Client) && !TimedTeleportThrottle.IsSafe)
                            {
                                // or fail and append the fail message.
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.TELEPORT_THROTTLED));
                                return;
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.TeleportLureRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                            }
                            return;
                        }
                    }
                    // Store teleport lure.
                    lock (TeleportLuresLock)
                    {
                        TeleportLures.Add(args.IM.IMSessionID, new TeleportLure
                        {
                            Agent = new Agent
                            {
                                FirstName = fullName.First(),
                                LastName = fullName.Last(),
                                UUID = args.IM.FromAgentID
                            },
                            Session = args.IM.IMSessionID
                        });
                    }
                    // Send teleport lure notification.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.TeleportLure, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    // If we got a teleport request from a master, then accept it (for the moment).
                    lock (Locks.ClientInstanceConfigurationLock)
                    {
                        if (
                            !corradeConfiguration.Masters.AsParallel()
                                .Any(
                                    o =>
                                        Strings.StringEquals(fullName.First(), o.FirstName,
                                            StringComparison.OrdinalIgnoreCase) &&
                                        Strings.StringEquals(fullName.Last(), o.LastName,
                                            StringComparison.OrdinalIgnoreCase)))
                            return;
                    }
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) && !TimedTeleportThrottle.IsSafe)
                    {
                        // or fail and append the fail message.
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.TELEPORT_THROTTLED));
                        return;
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        {
                            Client.Self.Stand();
                        }
                        // stop all non-built-in animations
                        Client.Self.SignaledAnimations.Copy()
                            .Keys.AsParallel()
                            .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                            .ForAll(o => { Client.Self.AnimationStop(o, true); });
                        Client.Self.TeleportLureRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                    }
                    return;
                // Group invitations received
                case InstantMessageDialog.GroupInvitation:
                    var inviteGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, args.IM.FromAgentID, corradeConfiguration.ServicesTimeout,
                            ref inviteGroup))
                        return;
                    // Add the group to the cache.
                    Cache.AddGroup(inviteGroup.Name, inviteGroup.ID);
                    var inviteGroupAgent = UUID.Zero;
                    if (
                        !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType),
                            ref inviteGroupAgent))
                        return;
                    // Add the group invite - have to track them manually.
                    lock (GroupInvitesLock)
                    {
                        GroupInvites.Add(args.IM.IMSessionID, new GroupInvite
                        {
                            Agent = new Agent
                            {
                                FirstName = fullName.First(),
                                LastName = fullName.Last(),
                                UUID = inviteGroupAgent
                            },
                            Group = inviteGroup.Name,
                            Session = args.IM.IMSessionID,
                            Fee = inviteGroup.MembershipFee
                        });
                    }
                    // Send group invitation notification.
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.GroupInvite, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    // If a master sends it, then accept.
                    lock (Locks.ClientInstanceConfigurationLock)
                    {
                        if (
                            !corradeConfiguration.Masters.AsParallel()
                                .Any(
                                    o =>
                                        Strings.StringEquals(fullName.First(), o.FirstName,
                                            StringComparison.OrdinalIgnoreCase) &&
                                        Strings.StringEquals(fullName.Last(), o.LastName,
                                            StringComparison.OrdinalIgnoreCase)))
                            return;
                    }
                    Client.Self.GroupInviteRespond(inviteGroup.ID, args.IM.IMSessionID, true);
                    return;
                // Notice received.
                case InstantMessageDialog.GroupNotice:
                    var noticeGroup = new Group();
                    if (
                        !Services.RequestGroup(Client,
                            args.IM.BinaryBucket.Length >= 18 ? new UUID(args.IM.BinaryBucket, 2) : args.IM.FromAgentID,
                            corradeConfiguration.ServicesTimeout, ref noticeGroup))
                        return;
                    // Add the group to the cache.
                    Cache.AddGroup(noticeGroup.Name, noticeGroup.ID);
                    var noticeGroupAgent = UUID.Zero;
                    if (
                        !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType),
                            ref noticeGroupAgent))
                        return;
                    // message contains an attachment
                    bool noticeAttachment;
                    var noticeAssetType = AssetType.Unknown;
                    var noticeFolder = UUID.Zero;
                    switch (args.IM.BinaryBucket.Length > 18 && !args.IM.BinaryBucket[0].Equals(0))
                    {
                        case true:
                            noticeAssetType = (AssetType) args.IM.BinaryBucket[1];
                            noticeFolder = Client.Inventory.FindFolderForType(noticeAssetType);
                            noticeAttachment = true;
                            break;
                        default:
                            noticeAttachment = false;
                            break;
                    }
                    // get the subject and the message
                    var noticeSubject = string.Empty;
                    var noticeMessage = string.Empty;
                    var noticeData = args.IM.Message.Split('|');
                    if (noticeData.Length > 0 && !string.IsNullOrEmpty(noticeData[0]))
                    {
                        noticeSubject = noticeData[0];
                    }
                    if (noticeData.Length > 1 && !string.IsNullOrEmpty(noticeData[1]))
                    {
                        noticeMessage = noticeData[1];
                    }
                    lock (GroupNoticeLock)
                    {
                        GroupNotices.Add(new GroupNotice
                        {
                            Agent = new Agent
                            {
                                FirstName = fullName.First(),
                                LastName = fullName.Last(),
                                UUID = noticeGroupAgent
                            },
                            Asset = noticeAssetType,
                            Attachment = noticeAttachment,
                            Folder = noticeFolder,
                            Group = noticeGroup,
                            Message = noticeMessage,
                            Subject = noticeSubject,
                            Session = args.IM.IMSessionID
                        });
                    }
                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Configuration.Notifications.GroupNotice, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.SessionSend:
                case InstantMessageDialog.MessageFromAgent:
                    // Check if this is a group message.
                    // Note that this is a lousy way of doing it but libomv does not properly set the GroupIM field
                    // such that the only way to determine if we have a group message is to check that the UUID
                    // of the session is actually the UUID of a current group. Furthermore, what's worse is that
                    // group mesages can appear both through SessionSend and from MessageFromAgent. Hence the problem.
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        return;
                    var messageGroups = new HashSet<UUID>(currentGroups);

                    // Check if this is a group message.
                    switch (messageGroups.Contains(args.IM.IMSessionID))
                    {
                        case true:
                            var messageGroup =
                                corradeConfiguration.Groups.AsParallel()
                                    .FirstOrDefault(p => p.UUID.Equals(args.IM.IMSessionID));
                            if (messageGroup != null && !messageGroup.Equals(default(Configuration.Group)))
                            {
                                // Add the group to the cache.
                                Cache.AddGroup(messageGroup.Name, messageGroup.UUID);
                                // Add the agent to the cache.
                                Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                                // Send group notice notifications.
                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                    () =>
                                        SendNotification(Configuration.Notifications.GroupMessage,
                                            new GroupMessageEventArgs
                                            {
                                                AgentUUID = args.IM.FromAgentID,
                                                FirstName = fullName.First(),
                                                LastName = fullName.Last(),
                                                GroupName = messageGroup.Name,
                                                GroupUUID = messageGroup.UUID,
                                                Message = args.IM.Message
                                            }),
                                    corradeConfiguration.MaximumNotificationThreads);
                                // Log group messages
                                corradeConfiguration.Groups.AsParallel().Where(
                                    o =>
                                        messageGroup.UUID.Equals(o.UUID) &&
                                        o.ChatLogEnabled).ForAll(o =>
                                        {
                                            // Attempt to write to log file,
                                            CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(
                                                () =>
                                                {
                                                    try
                                                    {
                                                        lock (GroupLogFileLock)
                                                        {
                                                            using (
                                                                var fileStream = File.Open(o.ChatLog, FileMode.Append,
                                                                    FileAccess.Write, FileShare.None))
                                                            {
                                                                using (
                                                                    var logWriter = new StreamWriter(fileStream,
                                                                        Encoding.UTF8))
                                                                {
                                                                    logWriter.WriteLine(
                                                                        CORRADE_CONSTANTS
                                                                            .GROUP_MESSAGE_LOG_MESSAGE_FORMAT,
                                                                        DateTime.Now.ToString(
                                                                            CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                                            Utils.EnUsCulture.DateTimeFormat),
                                                                        fullName.First(),
                                                                        fullName.Last(),
                                                                        args.IM.Message);
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        // or fail and append the fail message.
                                                        Feedback(
                                                            Reflection.GetDescriptionFromEnumValue(
                                                                Enumerations.ConsoleMessage
                                                                    .COULD_NOT_WRITE_TO_GROUP_CHAT_LOG_FILE),
                                                            ex.Message);
                                                    }
                                                }, corradeConfiguration.MaximumLogThreads,
                                                corradeConfiguration.ServicesTimeout);
                                        });
                            }
                            return;
                    }
                    // Check if this is a conference message.
                    switch (
                        (args.IM.Dialog == InstantMessageDialog.SessionSend &&
                         !messageGroups.Contains(args.IM.IMSessionID)) ||
                        (args.IM.Dialog == InstantMessageDialog.MessageFromAgent && args.IM.BinaryBucket.Length > 1))
                    {
                        case true:
                            // Join the chat if not yet joined
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                if (!Client.Self.GroupChatSessions.ContainsKey(args.IM.IMSessionID))
                                    Client.Self.ChatterBoxAcceptInvite(args.IM.IMSessionID);
                            }
                            var conferenceName = Utils.BytesToString(args.IM.BinaryBucket);
                            // Add the conference to the list of conferences.
                            lock (ConferencesLock)
                            {
                                if (!Conferences.AsParallel()
                                    .Any(
                                        o =>
                                            o.Name.Equals(conferenceName, StringComparison.Ordinal) &&
                                            o.Session.Equals(args.IM.IMSessionID)))
                                {
                                    Conferences.Add(new Conference
                                    {
                                        Name = conferenceName,
                                        Session = args.IM.IMSessionID,
                                        Restored = false
                                    });
                                }
                            }
                            // Save the conference state.
                            SaveConferenceState.Invoke();
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            // Send conference message notification.
                            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Configuration.Notifications.Conference, args),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Log conference messages,
                            if (corradeConfiguration.ConferenceMessageLogEnabled)
                            {
                                CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (ConferenceMessageLogFileLock)
                                        {
                                            using (
                                                var fileStream =
                                                    File.Open(
                                                        Path.Combine(corradeConfiguration.ConferenceMessageLogDirectory,
                                                            conferenceName) +
                                                        "." + CORRADE_CONSTANTS.LOG_FILE_EXTENSION, FileMode.Append,
                                                        FileAccess.Write, FileShare.None))
                                            {
                                                using (
                                                    var logWriter = new StreamWriter(fileStream,
                                                        Encoding.UTF8))
                                                {
                                                    logWriter.WriteLine(
                                                        CORRADE_CONSTANTS.CONFERENCE_MESSAGE_LOG_MESSAGE_FORMAT,
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        fullName.First(),
                                                        fullName.Last(),
                                                        args.IM.Message);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage
                                                    .COULD_NOT_WRITE_TO_CONFERENCE_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            return;
                    }
                    // Check if this is an instant message.
                    switch (!args.IM.ToAgentID.Equals(Client.Self.AgentID))
                    {
                        case false:
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            // Send instant message notification.
                            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Configuration.Notifications.InstantMessage, args),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Check if we were ejected.
                            var groupUUID = UUID.Zero;
                            if (
                                Resolvers.GroupNameToUUID(
                                    Client,
                                    CORRADE_CONSTANTS.EjectedFromGroupRegEx.Match(args.IM.Message).Groups[1].Value,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref groupUUID))
                            {
                                // Remove the group from the cache.
                                Cache.CurrentGroupsCache.Remove(groupUUID);
                            }

                            // Log instant messages,
                            if (corradeConfiguration.InstantMessageLogEnabled)
                            {
                                CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (InstantMessageLogFileLock)
                                        {
                                            using (
                                                var fileStream =
                                                    File.Open(
                                                        Path.Combine(corradeConfiguration.InstantMessageLogDirectory,
                                                            args.IM.FromAgentName) +
                                                        "." + CORRADE_CONSTANTS.LOG_FILE_EXTENSION, FileMode.Append,
                                                        FileAccess.Write, FileShare.None))
                                            {
                                                using (
                                                    var logWriter = new StreamWriter(fileStream,
                                                        Encoding.UTF8))
                                                {
                                                    logWriter.WriteLine(
                                                        CORRADE_CONSTANTS.INSTANT_MESSAGE_LOG_MESSAGE_FORMAT,
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        fullName.First(),
                                                        fullName.Last(),
                                                        args.IM.Message);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage
                                                    .COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            return;
                    }
                    // Check if this is a region message.
                    switch (!args.IM.IMSessionID.Equals(UUID.Zero))
                    {
                        case false:
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Configuration.Notifications.RegionMessage, args),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Log region messages,
                            if (corradeConfiguration.RegionMessageLogEnabled)
                            {
                                CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (RegionLogFileLock)
                                        {
                                            using (
                                                var fileStream =
                                                    File.Open(
                                                        Path.Combine(corradeConfiguration.RegionMessageLogDirectory,
                                                            Client.Network.CurrentSim.Name) + "." +
                                                        CORRADE_CONSTANTS.LOG_FILE_EXTENSION, FileMode.Append,
                                                        FileAccess.Write, FileShare.None))
                                            {
                                                using (
                                                    var logWriter = new StreamWriter(fileStream, Encoding.UTF8)
                                                    )
                                                {
                                                    logWriter.WriteLine(
                                                        CORRADE_CONSTANTS.REGION_MESSAGE_LOG_MESSAGE_FORMAT,
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        fullName.First(),
                                                        fullName.Last(),
                                                        args.IM.Message);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage
                                                    .COULD_NOT_WRITE_TO_REGION_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            return;
                    }
                    break;
            }

            // We are now in a region of code where the message is an IM sent by an object.
            // Check if this is not a Corrade command and send an object IM notification.
            if (!Utilities.IsCorradeCommand(args.IM.Message))
            {
                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                    () => SendNotification(Configuration.Notifications.ObjectInstantMessage, args),
                    corradeConfiguration.MaximumNotificationThreads);
                return;
            }

            // If the group was not set properly, then bail.
            var commandGroup = GetCorradeGroupFromMessage(args.IM.Message);
            if (commandGroup == null || commandGroup.Equals(default(Configuration.Group)))
                return;

            // Otherwise process the command.
            CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(
                () =>
                    HandleCorradeCommand(args.IM.Message, args.IM.FromAgentName, args.IM.FromAgentID.ToString(),
                        commandGroup),
                corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                corradeConfiguration.SchedulerExpiration);
        }


        private static Dictionary<string, string> HandleCorradeCommand(string message, string sender, string identifier,
            Configuration.Group commandGroup)
        {
            // Get password.
            var password =
                wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PASSWORD)),
                    message));
            // Bail if no password set.
            if (string.IsNullOrEmpty(password)) return null;
            // Authenticate the request against the group password.
            if (!Authenticate(commandGroup.UUID, password))
            {
                Feedback(commandGroup.Name,
                    Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.ACCESS_DENIED));
                return null;
            }
            // Censor password.
            message = KeyValue.Set(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PASSWORD)),
                CORRADE_CONSTANTS.PASSWORD_CENSOR, message);
            /*
             * OpenSim sends the primitive UUID through args.IM.FromAgentID while Second Life properly sends
             * the agent UUID - which just shows how crap and non-compliant OpenSim really is. This tries to
             * resolve args.IM.FromAgentID to a name, which is what Second Life does, otherwise it just sets
             * the name to the name of the primitive sending the message.
             */
            if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
            {
                UUID fromAgentID;
                if (UUID.TryParse(identifier, out fromAgentID))
                {
                    if (
                        !Resolvers.AgentUUIDToName(Client, fromAgentID, corradeConfiguration.ServicesTimeout,
                            ref sender))
                    {
                        Feedback(
                            Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.AGENT_NOT_FOUND),
                            fromAgentID.ToString());
                        return null;
                    }
                }
            }

            // Log the command.
            Feedback(string.Format(Utils.EnUsCulture, "{0} : {1} ({2}) : {3}", commandGroup.Name, sender, identifier,
                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.COMMAND)), message)));

            // Initialize workers for the group if they are not set.
            lock (GroupWorkersLock)
            {
                if (!GroupWorkers.Contains(commandGroup.Name))
                {
                    GroupWorkers.Add(commandGroup.Name, 0u);
                }
            }

            var configuredGroup = corradeConfiguration.Groups.AsParallel().FirstOrDefault(
                o => commandGroup.UUID.Equals(o.UUID));
            if (configuredGroup == null || configuredGroup.Equals(default(Configuration.Group)))
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.UNKNOWN_GROUP),
                    commandGroup.Name);
                return null;
            }

            // Check if the workers have not been exceeded.
            uint currentWorkers;
            lock (GroupWorkersLock)
            {
                currentWorkers = (uint) GroupWorkers[commandGroup.Name];
            }

            // Refuse to proceed if the workers have been exceeded.
            if (currentWorkers > configuredGroup.Workers)
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.WORKERS_EXCEEDED),
                    commandGroup.Name);
                return null;
            }

            // Increment the group workers.
            lock (GroupWorkersLock)
            {
                GroupWorkers[commandGroup.Name] = (uint) GroupWorkers[commandGroup.Name] + 1;
            }
            // Perform the command.
            var result = ProcessCommand(new CorradeCommandParameters
            {
                Message = message,
                Sender = sender,
                Identifier = identifier,
                Group = commandGroup
            });
            // Decrement the group workers.
            lock (GroupWorkersLock)
            {
                GroupWorkers[commandGroup.Name] = (uint) GroupWorkers[commandGroup.Name] - 1;
            }
            // do not send a callback if the callback queue is saturated
            if (CallbackQueue.Count >= corradeConfiguration.CallbackQueueLength)
            {
                Feedback(Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.CALLBACK_THROTTLED));
                return result;
            }
            // send callback if registered
            var url =
                wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.CALLBACK)),
                    message));
            // if no url was provided, do not send the callback
            if (string.IsNullOrEmpty(url)) return result;
            CallbackQueue.Enqueue(new CallbackQueueElement
            {
                GroupUUID = commandGroup.UUID,
                URL = url,
                message = KeyValue.Escape(result, wasOutput)
            });
            return result;
        }

        /// <summary>
        ///     This function is responsible for processing commands.
        /// </summary>
        /// <param name="corradeCommandParameters">the command parameters</param>
        /// <returns>a dictionary of key-value pairs representing the results of the command</returns>
        private static Dictionary<string, string> ProcessCommand(
            CorradeCommandParameters corradeCommandParameters)
        {
            var result = new Dictionary<string, string>
            {
                // add the command group to the response.
                {Reflection.GetNameFromEnumValue(ScriptKeys.GROUP), corradeCommandParameters.Group.Name}
            };

            // retrieve the command from the message.
            var command =
                wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.COMMAND)),
                    corradeCommandParameters.Message));
            if (!string.IsNullOrEmpty(command))
            {
                result.Add(Reflection.GetNameFromEnumValue(ScriptKeys.COMMAND), command);
            }

            // execute command, sift data and check for errors
            var success = false;
            try
            {
                // Find command.
                var scriptKey = Reflection.GetEnumValueFromName<ScriptKeys>(command);
                if (scriptKey.Equals(default(ScriptKeys)))
                {
                    throw new ScriptException(Enumerations.ScriptError.COMMAND_NOT_FOUND);
                }
                var execute =
                    Reflection.GetAttributeFromEnumValue<CorradeCommandAttribute>(scriptKey);

                // Execute the command.
                try
                {
                    Interlocked.Increment(ref CorradeHeartbeat.ExecutingCommands);
                    execute.CorradeCommand.Invoke(corradeCommandParameters, result);
                    Interlocked.Increment(ref CorradeHeartbeat.ProcessedCommands);
                    // Sifting was requested so apply the filters in order.
                    var sift =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SIFT)),
                            corradeCommandParameters.Message));
                    string data;
                    if (result.TryGetValue(Reflection.GetNameFromEnumValue(ResultKeys.DATA), out data) &&
                        !string.IsNullOrEmpty(sift))
                    {
                        foreach (var kvp in CSV.ToKeyValue(sift))
                        {
                            switch (Reflection.GetEnumValueFromName<Sift>(wasInput(kvp.Key).ToLowerInvariant()))
                            {
                                case Sift.TAKE:
                                    // Take a specified amount from the results if requested.
                                    int take;
                                    if (!string.IsNullOrEmpty(data) && int.TryParse(wasInput(kvp.Value), out take))
                                    {
                                        data = CSV.FromEnumerable(CSV.ToEnumerable(data).Take(take));
                                    }
                                    break;
                                case Sift.SKIP:
                                    // Skip a number of elements if requested.
                                    int skip;
                                    if (!string.IsNullOrEmpty(data) && int.TryParse(wasInput(kvp.Value), out skip))
                                    {
                                        data = CSV.FromEnumerable(CSV.ToEnumerable(data).Skip(skip));
                                    }
                                    break;
                                case Sift.EACH:
                                    // Return a stride in case it was requested.
                                    int each;
                                    if (!string.IsNullOrEmpty(data) && int.TryParse(wasInput(kvp.Value), out each))
                                    {
                                        data = CSV.FromEnumerable(CSV.ToEnumerable(data).Where((e, i) => i%each == 0));
                                    }
                                    break;
                                case Sift.MATCH:
                                    // Match the results if requested.
                                    var regex = wasInput(kvp.Value);
                                    if (!string.IsNullOrEmpty(data) && !string.IsNullOrEmpty(regex))
                                    {
                                        data =
                                            CSV.FromEnumerable(new Regex(regex, RegexOptions.Compiled).Matches(data)
                                                .AsParallel()
                                                .Cast<Match>()
                                                .Select(m => m.Groups).SelectMany(
                                                    matchGroups => Enumerable.Range(0, matchGroups.Count).Skip(1),
                                                    (matchGroups, i) => new {matchGroups, i})
                                                .SelectMany(
                                                    t => Enumerable.Range(0, t.matchGroups[t.i].Captures.Count),
                                                    (t, j) => t.matchGroups[t.i].Captures[j].Value));
                                    }
                                    break;
                                default:
                                    throw new ScriptException(Enumerations.ScriptError.UNKNOWN_SIFT);
                            }
                            switch (!string.IsNullOrEmpty(data))
                            {
                                case true:
                                    result[Reflection.GetNameFromEnumValue(ResultKeys.DATA)] = data;
                                    break;
                                default:
                                    result.Remove(Reflection.GetNameFromEnumValue(ResultKeys.DATA));
                                    break;
                            }
                        }
                    }

                    success = true;
                }
                catch (ScriptException sx)
                {
                    // we have a script error so return a status as well
                    result.Add(Reflection.GetNameFromEnumValue(ResultKeys.ERROR), sx.Message);
                    result.Add(Reflection.GetNameFromEnumValue(ResultKeys.STATUS),
                        sx.Status.ToString());
                }
                finally
                {
                    Interlocked.Decrement(ref CorradeHeartbeat.ExecutingCommands);
                }
            }
            catch (Exception ex)
            {
                // we have a generic exception so return the message
                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.ERROR), ex.Message);
            }

            // add the final success status
            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.SUCCESS),
                success.ToString(Utils.EnUsCulture));

            // add the time stamp
            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.TIME),
                DateTime.Now.ToUniversalTime().ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP));

            // build afterburn
            var AfterBurnLock = new object();
            // remove keys that are script keys, result keys or invalid key-value pairs
            var resultKeys = new HashSet<string>(Reflection.GetEnumNames<ResultKeys>());
            var scriptKeys = new HashSet<string>(Reflection.GetEnumNames<ScriptKeys>());
            KeyValue.Decode(corradeCommandParameters.Message)
                .AsParallel()
                .Where(
                    o =>
                        !string.IsNullOrEmpty(o.Key) && !resultKeys.Contains(wasInput(o.Key)) &&
                        !scriptKeys.Contains(wasInput(o.Key)) && !string.IsNullOrEmpty(o.Value))
                .ForAll(o =>
                {
                    lock (AfterBurnLock)
                    {
                        result.Add(o.Key, o.Value);
                    }
                });

            return result;
        }


        private static void HandleTerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.TerseUpdates, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleRadarObjects(object sender, SimChangedEventArgs e)
        {
            lock (RadarObjectsLock)
            {
                if (RadarObjects.Any())
                {
                    RadarObjects.Clear();
                }
            }
        }

        private static void HandleSimChanged(object sender, SimChangedEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.RegionCrossed, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMoneyBalance(object sender, BalanceEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Balance, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMoneyBalance(object sender, MoneyBalanceReplyEventArgs e)
        {
            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Configuration.Notifications.Economy, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void UpdateDynamicConfiguration(Configuration configuration)
        {
            // Send message that we are updating the configuration.
            Feedback(
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.UPDATING_CORRADE_CONFIGURATION));

            // Setup heartbeat log timer.
            CorradeHeartBeatLogTimer.Change(TimeSpan.FromMilliseconds(configuration.HeartbeatLogInterval),
                TimeSpan.FromMilliseconds(configuration.HeartbeatLogInterval));

            // Set the content type based on chosen output filers
            switch (configuration.OutputFilters.LastOrDefault())
            {
                case Configuration.Filter.RFC1738:
                    CorradePOSTMediaType = CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                    break;
                default:
                    CorradePOSTMediaType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                    break;
            }

            // Setup per-group HTTP clients.
            configuration.Groups.AsParallel().ForAll(o =>
            {
                // Create cookie containers for new groups.
                lock (GroupCookieContainersLock)
                {
                    if (!GroupCookieContainers.ContainsKey(o.UUID))
                    {
                        GroupCookieContainers.Add(o.UUID, new CookieContainer());
                    }
                }
                lock (GroupHTTPClientsLock)
                {
                    if (!GroupHTTPClients.ContainsKey(o.UUID))
                    {
                        GroupHTTPClients.Add(o.UUID, new Web.wasHTTPClient
                            (CORRADE_CONSTANTS.USER_AGENT, GroupCookieContainers[o.UUID], CorradePOSTMediaType, null,
                                null,
                                configuration.ServicesTimeout));
                    }
                }
            });

            // Remove HTTP clients from groups that are not configured.
            lock (GroupHTTPClientsLock)
            {
                new List<UUID>(
                    GroupHTTPClients.Keys.AsParallel().Where(o => !configuration.Groups.Any(p => p.UUID.Equals(o))))
                    .AsParallel().ForAll(
                        o => { GroupHTTPClients.Remove(o); });
            }

            // Setup horde synchronization if enabled.
            switch (configuration.EnableHorde)
            {
                case true:
                    // Setup HTTP clients.
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.Clear();
                    }
                    configuration.HordePeers.AsParallel().ForAll(o =>
                    {
                        lock (HordeHTTPClientsLock)
                        {
                            HordeHTTPClients.Add(o.URL, new Web.wasHTTPClient
                                (CORRADE_CONSTANTS.USER_AGENT, new CookieContainer(), @"text/plain",
                                    new AuthenticationHeaderValue(@"Basic",
                                        Convert.ToBase64String(
                                            Encoding.ASCII.GetBytes($"{o.Username}:{o.Password}"))),
                                    new Dictionary<string, string>
                                    {
                                        {
                                            CORRADE_CONSTANTS.HORDE_SHARED_SECRET_HEADER,
                                            Convert.ToBase64String(Encoding.UTF8.GetBytes(o.SharedSecret))
                                        }
                                    },
                                    configuration.ServicesTimeout));
                        }
                    });
                    // Bind to horde synchronization changes.
                    switch (
                        configuration.HordePeers.AsParallel()
                            .Any(
                                o => o.SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Agent)))
                    {
                        case true:
                            Cache.ObservableAgentCache.CollectionChanged -= HandleAgentCacheChanged;
                            Cache.ObservableAgentCache.CollectionChanged += HandleAgentCacheChanged;
                            break;
                        default:
                            Cache.ObservableAgentCache.CollectionChanged -= HandleAgentCacheChanged;
                            break;
                    }
                    switch (
                        configuration.HordePeers.AsParallel()
                            .Any(
                                o => o.SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Region))
                        )
                    {
                        case true:
                            Cache.ObservableRegionCache.CollectionChanged -= HandleRegionCacheChanged;
                            Cache.ObservableRegionCache.CollectionChanged += HandleRegionCacheChanged;
                            break;
                        default:
                            Cache.ObservableRegionCache.CollectionChanged -= HandleRegionCacheChanged;
                            break;
                    }
                    switch (
                        configuration.HordePeers.AsParallel()
                            .Any(
                                o => o.SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Group)))
                    {
                        case true:
                            Cache.ObservableGroupCache.CollectionChanged -= HandleGroupCacheChanged;
                            Cache.ObservableGroupCache.CollectionChanged += HandleGroupCacheChanged;
                            break;
                        default:
                            Cache.ObservableGroupCache.CollectionChanged -= HandleGroupCacheChanged;
                            break;
                    }
                    switch (
                        configuration.HordePeers.AsParallel()
                            .Any(
                                o => o.SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Mute)))
                    {
                        case true:
                            Cache.ObservableMuteCache.CollectionChanged -= HandleMuteCacheChanged;
                            Cache.ObservableMuteCache.CollectionChanged += HandleMuteCacheChanged;
                            break;
                        default:
                            Cache.ObservableMuteCache.CollectionChanged -= HandleMuteCacheChanged;
                            break;
                    }
                    break;
                default:
                    // Remove HTTP clients.
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.Clear();
                    }
                    Cache.ObservableAgentCache.CollectionChanged -= HandleAgentCacheChanged;
                    Cache.ObservableRegionCache.CollectionChanged -= HandleRegionCacheChanged;
                    Cache.ObservableGroupCache.CollectionChanged -= HandleGroupCacheChanged;
                    Cache.ObservableMuteCache.CollectionChanged -= HandleMuteCacheChanged;
                    break;
            }

            // Enable the group scheduling timer if permissions were granted to groups.
            switch (configuration.Groups.AsParallel()
                .Any(
                    o => o.PermissionMask.IsMaskFlagSet(Configuration.Permissions.Schedule) &&
                         !o.Schedules.Equals(0)))
            {
                case true:
                    // Start the group schedules timer.
                    GroupSchedulesTimer.Change(TimeSpan.FromMilliseconds(configuration.SchedulesResolution),
                        TimeSpan.FromMilliseconds(configuration.SchedulesResolution));
                    break;
                default:
                    GroupSchedulesTimer.Stop();
                    break;
            }

            // Enable SIML in case it was enabled in the configuration file.
            try
            {
                switch (configuration.EnableSIML)
                {
                    case true:
                        lock (SIMLBotLock)
                        {
                            SynBot.Learning += HandleSynBotLearning;
                            SynBot.Memorizing += HandleSynBotMemorizing;
                            SynBotUser.EmotionChanged += HandleSynBotUserEmotionChanged;
                            LoadChatBotFiles.Invoke();
                        }
                        break;
                    default:
                        lock (SIMLBotLock)
                        {
                            SynBot.Learning -= HandleSynBotLearning;
                            SynBot.Memorizing -= HandleSynBotMemorizing;
                            SynBotUser.EmotionChanged -= HandleSynBotUserEmotionChanged;
                            if (!string.IsNullOrEmpty(SIMLBotConfigurationWatcher.Path))
                                SIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_SIML_CONFIGURATION_WATCHER),
                    ex.Message);
            }

            // Dynamically disable or enable notifications.
            Reflection.GetEnumValues<Configuration.Notifications>().AsParallel().ForAll(o =>
            {
                var enabled = configuration.Groups.AsParallel().Any(
                    p => p.NotificationMask.IsMaskFlagSet(o));
                switch (o)
                {
                    case Configuration.Notifications.Sound:
                        switch (enabled)
                        {
                            case true:
                                Client.Sound.SoundTrigger += HandleSoundTrigger;
                                break;
                            default:
                                Client.Sound.SoundTrigger -= HandleSoundTrigger;
                                break;
                        }
                        break;
                    case Configuration.Notifications.AnimationsChanged:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.AnimationsChanged += HandleAnimationsChanged;
                                break;
                            default:
                                Client.Self.AnimationsChanged -= HandleAnimationsChanged;
                                break;
                        }
                        break;
                    case Configuration.Notifications.Feed:
                        switch (enabled)
                        {
                            case true:
                                // Start the group feed thread.
                                GroupFeedsTimer.Change(
                                    TimeSpan.FromMilliseconds(corradeConfiguration.FeedsUpdateInterval),
                                    TimeSpan.FromMilliseconds(corradeConfiguration.FeedsUpdateInterval));
                                break;
                            default:
                                // Stop the group feed thread.
                                GroupFeedsTimer.Stop();
                                break;
                        }
                        break;
                    case Configuration.Notifications.Friendship:
                        switch (enabled)
                        {
                            case true:
                                Client.Friends.FriendshipOffered += HandleFriendshipOffered;
                                Client.Friends.FriendshipResponse += HandleFriendShipResponse;
                                Client.Friends.FriendOnline += HandleFriendOnlineStatus;
                                Client.Friends.FriendOffline += HandleFriendOnlineStatus;
                                Client.Friends.FriendRightsUpdate += HandleFriendRightsUpdate;
                                break;
                            default:
                                Client.Friends.FriendshipOffered -= HandleFriendshipOffered;
                                Client.Friends.FriendshipResponse -= HandleFriendShipResponse;
                                Client.Friends.FriendOnline -= HandleFriendOnlineStatus;
                                Client.Friends.FriendOffline -= HandleFriendOnlineStatus;
                                Client.Friends.FriendRightsUpdate -= HandleFriendRightsUpdate;
                                break;
                        }
                        break;
                    case Configuration.Notifications.ScriptPermission:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.ScriptQuestion += HandleScriptQuestion;
                                break;
                            default:
                                Client.Self.ScriptQuestion -= HandleScriptQuestion;
                                break;
                        }
                        break;
                    case Configuration.Notifications.AlertMessage:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.AlertMessage += HandleAlertMessage;
                                break;
                            default:
                                Client.Self.AlertMessage -= HandleAlertMessage;
                                break;
                        }
                        break;
                    case Configuration.Notifications.Balance:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.MoneyBalance += HandleMoneyBalance;
                                break;
                            default:
                                Client.Self.MoneyBalance -= HandleMoneyBalance;
                                break;
                        }
                        break;
                    case Configuration.Notifications.Economy:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.MoneyBalanceReply += HandleMoneyBalance;
                                break;
                            default:
                                Client.Self.MoneyBalanceReply -= HandleMoneyBalance;
                                break;
                        }
                        break;
                    case Configuration.Notifications.ScriptDialog:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.ScriptDialog += HandleScriptDialog;
                                break;
                            default:
                                Client.Self.ScriptDialog -= HandleScriptDialog;
                                break;
                        }
                        break;
                    case Configuration.Notifications.SitChanged:
                        switch (enabled)
                        {
                            case true:
                                Client.Objects.AvatarSitChanged += HandleAvatarSitChanged;
                                break;
                            default:
                                Client.Objects.AvatarSitChanged -= HandleAvatarSitChanged;
                                break;
                        }
                        break;
                    case Configuration.Notifications.TerseUpdates:
                        switch (enabled)
                        {
                            case true:
                                Client.Objects.TerseObjectUpdate += HandleTerseObjectUpdate;
                                break;
                            default:
                                Client.Objects.TerseObjectUpdate -= HandleTerseObjectUpdate;
                                break;
                        }
                        break;
                    case Configuration.Notifications.ViewerEffect:
                        switch (enabled)
                        {
                            case true:
                                Client.Avatars.ViewerEffect += HandleViewerEffect;
                                Client.Avatars.ViewerEffectPointAt += HandleViewerEffect;
                                Client.Avatars.ViewerEffectLookAt += HandleViewerEffect;
                                break;
                            default:
                                Client.Avatars.ViewerEffect -= HandleViewerEffect;
                                Client.Avatars.ViewerEffectPointAt -= HandleViewerEffect;
                                Client.Avatars.ViewerEffectLookAt -= HandleViewerEffect;
                                break;
                        }
                        break;
                    case Configuration.Notifications.MeanCollision:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.MeanCollision += HandleMeanCollision;
                                break;
                            default:
                                Client.Self.MeanCollision -= HandleMeanCollision;
                                break;
                        }
                        break;
                    case Configuration.Notifications.RegionCrossed:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.RegionCrossed += HandleRegionCrossed;
                                Client.Network.SimChanged += HandleSimChanged;
                                break;
                            default:
                                Client.Self.RegionCrossed -= HandleRegionCrossed;
                                Client.Network.SimChanged -= HandleSimChanged;
                                break;
                        }
                        break;
                    case Configuration.Notifications.LoadURL:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.LoadURL += HandleLoadURL;
                                break;
                            default:
                                Client.Self.LoadURL -= HandleLoadURL;
                                break;
                        }
                        break;
                    case Configuration.Notifications.ScriptControl:
                        switch (enabled)
                        {
                            case true:
                                Client.Self.ScriptControlChange += HandleScriptControlChange;
                                break;
                            default:
                                Client.Self.ScriptControlChange -= HandleScriptControlChange;
                                break;
                        }
                        break;
                    case Configuration.Notifications.Store:
                        if (Client.Inventory.Store != null)
                        {
                            switch (enabled)
                            {
                                case true:
                                    Client.Inventory.Store.InventoryObjectAdded += HandleInventoryObjectAdded;
                                    Client.Inventory.Store.InventoryObjectRemoved += HandleInventoryObjectRemoved;
                                    Client.Inventory.Store.InventoryObjectUpdated += HandleInventoryObjectUpdated;
                                    break;
                                default:
                                    Client.Inventory.Store.InventoryObjectAdded -= HandleInventoryObjectAdded;
                                    Client.Inventory.Store.InventoryObjectRemoved -= HandleInventoryObjectRemoved;
                                    Client.Inventory.Store.InventoryObjectUpdated -= HandleInventoryObjectUpdated;
                                    break;
                            }
                        }
                        break;
                }
            });

            // Depending on whether groups have bound to the viewer effects notification,
            // start or stop the viwer effect expiration thread.
            switch (
                configuration.Groups.AsParallel()
                    .Any(o => o.NotificationMask.IsMaskFlagSet(Configuration.Notifications.ViewerEffect)))
            {
                case true:
                    // Start sphere and beam effect expiration thread
                    EffectsExpirationTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                    break;
                default:
                    // Stop the effects expiration thread.
                    EffectsExpirationTimer.Stop();
                    break;
            }

            // Depending on whether any group has bound either the avatar radar notification,
            // or the primitive radar notification, install or uinstall the listeners.
            switch (
                configuration.Groups.AsParallel().Any(
                    o =>
                        o.NotificationMask.IsMaskFlagSet(Configuration.Notifications.RadarAvatars) ||
                        o.NotificationMask.IsMaskFlagSet(Configuration.Notifications.RadarPrimitives)))
            {
                case true:
                    Client.Network.SimChanged += HandleRadarObjects;
                    Client.Objects.AvatarUpdate += HandleAvatarUpdate;
                    Client.Objects.ObjectUpdate += HandleObjectUpdate;
                    Client.Objects.KillObject += HandleKillObject;
                    break;
                default:
                    Client.Network.SimChanged -= HandleRadarObjects;
                    Client.Objects.AvatarUpdate -= HandleAvatarUpdate;
                    Client.Objects.ObjectUpdate -= HandleObjectUpdate;
                    Client.Objects.KillObject -= HandleKillObject;
                    break;
            }

            // Enable the TCP notifications server in case it was enabled in the Configuration.
            switch (configuration.EnableTCPNotificationsServer)
            {
                case true:
                    // Don't start if the TCP notifications server is already started.
                    if (TCPNotificationsThread != null) break;
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.STARTING_TCP_NOTIFICATIONS_SERVER));
                    runTCPNotificationsServer = true;
                    // Start the TCP notifications server.
                    TCPNotificationsThread = new Thread(ProcessTCPNotifications);
                    TCPNotificationsThread.IsBackground = true;
                    TCPNotificationsThread.Start();
                    break;
                default:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.STOPPING_TCP_NOTIFICATIONS_SERVER));
                    runTCPNotificationsServer = false;
                    try
                    {
                        if (TCPNotificationsThread != null)
                        {
                            TCPListener.Stop();
                            if (
                                TCPNotificationsThread.ThreadState.Equals(ThreadState.Running) ||
                                TCPNotificationsThread.ThreadState.Equals(ThreadState.WaitSleepJoin))
                            {
                                if (!TCPNotificationsThread.Join(1000))
                                {
                                    TCPNotificationsThread.Abort();
                                    TCPNotificationsThread.Join();
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        /* We are going down and we do not care. */
                    }
                    finally
                    {
                        TCPNotificationsThread = null;
                    }
                    break;
            }

            // Enable the HTTP server in case it is supported and it was enabled in the Configuration.
            switch (HttpListener.IsSupported)
            {
                case true:
                    switch (configuration.EnableHTTPServer)
                    {
                        case true:
                            // Don't start if the HTTP server is already started.
                            if (HTTPListenerThread != null) break;
                            Feedback(
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.STARTING_HTTP_SERVER));
                            runHTTPServer = true;
                            HTTPListenerThread = new Thread(() =>
                            {
                                try
                                {
                                    using (HTTPListener = new HttpListener())
                                    {
                                        // Add prefixes.
                                        HTTPListener.Prefixes.Add(configuration.HTTPServerPrefix);
                                        // Add authentication.
                                        HTTPListener.AuthenticationSchemes = AuthenticationSchemes.Basic |
                                                                             AuthenticationSchemes.Anonymous;
                                        // TimeoutManager is not supported on mono (what is mono good for anyway, practically speaking?).
                                        switch (Environment.OSVersion.Platform)
                                        {
                                            case PlatformID.Win32NT:
                                                // We have to set this through reflection to prevent mono from bombing.
                                                var pi =
                                                    HTTPListener.GetType()
                                                        .GetProperty("TimeoutManager",
                                                            BindingFlags.Public | BindingFlags.Instance);
                                                var timeoutManager = pi?.GetValue(HTTPListener, null);
                                                // Check if we have TimeoutManager.
                                                if (timeoutManager == null) break;
                                                // Now, set the properties through reflection.
                                                pi = timeoutManager.GetType().GetProperty("DrainEntityBody");
                                                pi?.SetValue(timeoutManager,
                                                    TimeSpan.FromMilliseconds(configuration.HTTPServerDrainTimeout),
                                                    null);
                                                pi = timeoutManager.GetType().GetProperty("EntityBody");
                                                pi?.SetValue(timeoutManager,
                                                    TimeSpan.FromMilliseconds(configuration.HTTPServerBodyTimeout),
                                                    null);
                                                pi = timeoutManager.GetType().GetProperty("HeaderWait");
                                                pi?.SetValue(timeoutManager,
                                                    TimeSpan.FromMilliseconds(configuration.HTTPServerHeaderTimeout),
                                                    null);
                                                pi = timeoutManager.GetType().GetProperty("IdleConnection");
                                                pi?.SetValue(timeoutManager,
                                                    TimeSpan.FromMilliseconds(configuration.HTTPServerIdleTimeout),
                                                    null);
                                                pi = timeoutManager.GetType().GetProperty("RequestQueue");
                                                pi?.SetValue(timeoutManager,
                                                    TimeSpan.FromMilliseconds(configuration.HTTPServerQueueTimeout),
                                                    null);
                                                break;
                                        }
                                        HTTPListener.Start();
                                        while (runHTTPServer && HTTPListener.IsListening)
                                        {
                                            var result = HTTPListener.BeginGetContext(ProcessHTTPRequest,
                                                HTTPListener);
                                            WaitHandle.WaitAny(new[] {result.AsyncWaitHandle});
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Feedback(
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ConsoleMessage.HTTP_SERVER_ERROR),
                                        ex.Message);
                                }
                            })
                            {IsBackground = true};
                            HTTPListenerThread.Start();
                            break;
                        default:
                            Feedback(
                                Reflection.GetDescriptionFromEnumValue(
                                    Enumerations.ConsoleMessage.STOPPING_HTTP_SERVER));
                            runHTTPServer = false;
                            try
                            {
                                if (HTTPListenerThread != null)
                                {
                                    HTTPListener.Stop();
                                    if (
                                        HTTPListenerThread.ThreadState.Equals(ThreadState.Running) ||
                                        HTTPListenerThread.ThreadState.Equals(ThreadState.WaitSleepJoin))
                                    {
                                        if (!HTTPListenerThread.Join(1000))
                                        {
                                            HTTPListenerThread.Abort();
                                            HTTPListenerThread.Join();
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                /* We are going down and we do not care. */
                            }
                            finally
                            {
                                HTTPListenerThread = null;
                            }
                            break;
                    }
                    break;
                default:
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.HTTP_SERVER_ERROR),
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.HTTP_SERVER_NOT_SUPPORTED));
                    break;
            }

            // Apply settings to the instance.
            Client.Self.Movement.Camera.Far = configuration.Range;
            Client.Settings.LOGIN_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.LOGOUT_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.SIMULATOR_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.CAPS_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.MAP_REQUEST_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.TRANSFER_TIMEOUT = (int) configuration.ServicesTimeout;
            Client.Settings.TELEPORT_TIMEOUT = (int) configuration.ServicesTimeout;
            Settings.MAX_HTTP_CONNECTIONS = (int) configuration.ConnectionLimit;

            // Network Settings
            ServicePointManager.DefaultConnectionLimit = (int) configuration.ConnectionLimit;
            ServicePointManager.UseNagleAlgorithm = configuration.UseNaggle;
            ServicePointManager.Expect100Continue = configuration.UseExpect100Continue;
            ServicePointManager.MaxServicePointIdleTime = (int) configuration.ConnectionIdleTime;
            // Do not use SSLv3 - POODLE
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 |
                                                   SecurityProtocolType.Tls12;

            // Throttles.
            Client.Throttle.Total = configuration.ThrottleTotal;
            Client.Throttle.Land = configuration.ThrottleLand;
            Client.Throttle.Task = configuration.ThrottleTask;
            Client.Throttle.Texture = configuration.ThrottleTexture;
            Client.Throttle.Wind = configuration.ThrottleWind;
            Client.Throttle.Resend = configuration.ThrottleResend;
            Client.Throttle.Asset = configuration.ThrottleAsset;
            Client.Throttle.Cloud = configuration.ThrottleCloud;

            // Client identification tag.
            Client.Settings.CLIENT_IDENTIFICATION_TAG = configuration.ClientIdentificationTag;

            // Cache settings.
            Client.Assets.Cache.AutoPruneInterval = corradeConfiguration.CacheAutoPruneInterval;
            Client.Assets.Cache.AutoPruneEnabled = corradeConfiguration.CacheEnableAutoPrune;

            // Send message that the configuration has been updated.
            Feedback(
                Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.CORRADE_CONFIGURATION_UPDATED));
        }

        private static void HandleSynBotUserEmotionChanged(object sender, EmotionChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private static void HordeDistributeCacheAsset(UUID assetUUID, byte[] data,
            Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel()
                            .Where(
                                p =>
                                {
                                    var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                        q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                    return peer != null && peer
                                        .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Asset);
                                })
                            .ForAll(async p =>
                            {
                                await
                                    p.Value.PUT(
                                        $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Enumerations.WebResource.CACHE)}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Asset)}/{Reflection.GetNameFromEnumValue(option)}/{assetUUID.ToString()}",
                                        data);
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Asset),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HandleDistributeBayes(UUID groupUUID, string data,
            Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel()
                            .Where(
                                p =>
                                {
                                    var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                        q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                    return peer != null && peer
                                        .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Bayes);
                                })
                            .ForAll(
                                async p =>
                                {
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Enumerations.WebResource.BAYES)}/{Reflection.GetNameFromEnumValue(option)}/{groupUUID.ToString()}",
                                            data);
                                });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Bayes),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeCacheGroup(Cache.Group o, Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Group);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(Cache.Group));
                                    serializer.Serialize(writer, o);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Enumerations.WebResource.CACHE)}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Group)}/{Reflection.GetNameFromEnumValue(option)}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Group),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeCacheRegion(Cache.Region o,
            Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Region);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(Cache.Region));
                                    serializer.Serialize(writer, o);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Enumerations.WebResource.CACHE)}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Region)}/{Reflection.GetNameFromEnumValue(option)}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Region),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeCacheAgent(Cache.Agent o, Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Agent);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(Cache.Agent));
                                    serializer.Serialize(writer, o);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Enumerations.WebResource.CACHE)}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Agent)}/{Reflection.GetNameFromEnumValue(option)}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Agent),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeCacheMute(MuteEntry o, Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.Mute);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(MuteEntry));
                                    serializer.Serialize(writer, o);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Mute)}/{Reflection.GetNameFromEnumValue(option)}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.Mute),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeGroupSoftBan(UUID groupUUID, UUID agentUUID,
            Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.SoftBan);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(UUID));
                                    serializer.Serialize(writer, agentUUID);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.SoftBan)}/{Reflection.GetNameFromEnumValue(option)}/{groupUUID.ToString()}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.SoftBan),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HordeDistributeConfigurationGroup(Configuration.Group group,
            Configuration.HordeDataSynchronizationOption option)
        {
            new Thread(() =>
            {
                try
                {
                    lock (HordeHTTPClientsLock)
                    {
                        HordeHTTPClients.AsParallel().Where(
                            p =>
                            {
                                var peer = corradeConfiguration.HordePeers.SingleOrDefault(
                                    q => p.Key.Equals(q.URL, StringComparison.OrdinalIgnoreCase));
                                return peer != null && peer
                                    .SynchronizationMask.IsMaskFlagSet(Configuration.HordeDataSynchronization.User);
                            })
                            .ForAll(async p =>
                            {
                                using (var writer = new StringWriter())
                                {
                                    var serializer = new XmlSerializer(typeof(Configuration.Group));
                                    serializer.Serialize(writer, group);
                                    await
                                        p.Value.PUT(
                                            $"{p.Key.TrimEnd('/')}/{Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.User)}/{Reflection.GetNameFromEnumValue(option)}/{group.UUID.ToString()}",
                                            writer.ToString());
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            Enumerations.ConsoleMessage.UNABLE_TO_DISTRIBUTE_RESOURCE),
                        Reflection.GetNameFromEnumValue(Configuration.HordeDataSynchronization.User),
                        ex.Message);
                }
            })
            {IsBackground = true, Priority = ThreadPriority.Lowest}.Start();
        }

        private static void HandleGroupCacheChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    e.NewItems?.OfType<Cache.Group>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheGroup(o, Configuration.HordeDataSynchronizationOption.Add));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    e.OldItems?.OfType<Cache.Group>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheGroup(o, Configuration.HordeDataSynchronizationOption.Remove));
                    break;
            }
        }

        private static void HandleRegionCacheChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    e.NewItems?.OfType<Cache.Region>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheRegion(o, Configuration.HordeDataSynchronizationOption.Add));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    e.OldItems?.OfType<Cache.Region>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheRegion(o, Configuration.HordeDataSynchronizationOption.Remove));
                    break;
            }
        }

        private static void HandleAgentCacheChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    e.NewItems?.OfType<Cache.Agent>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheAgent(o, Configuration.HordeDataSynchronizationOption.Add));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    e.OldItems?.OfType<Cache.Agent>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheAgent(o, Configuration.HordeDataSynchronizationOption.Remove));
                    break;
            }
        }

        private static void HandleMuteCacheChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    e.NewItems?.OfType<MuteEntry>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheMute(o, Configuration.HordeDataSynchronizationOption.Add));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    e.OldItems?.OfType<MuteEntry>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o => HordeDistributeCacheMute(o, Configuration.HordeDataSynchronizationOption.Remove));
                    break;
            }
        }

        private static void HandleGroupMemberJoinPart(object sender, NotifyCollectionChangedEventArgs e)
        {
            var group =
                GroupMembers.FirstOrDefault(
                    o => ReferenceEquals(o.Value, sender as Collections.ObservableHashSet<UUID>));
            if (group.Equals(default(KeyValuePair<UUID, Collections.ObservableHashSet<UUID>>)))
                return;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Add:
                    e.NewItems?.OfType<UUID>().ToList().AsParallel().ForAll(o =>
                    {
                        // Send membership notification if enabled.
                        if (corradeConfiguration.Groups.AsParallel()
                            .Any(
                                p =>
                                    p.UUID.Equals(group.Key) &&
                                    p.NotificationMask.IsMaskFlagSet(Configuration.Notifications.GroupMembership)))
                        {
                            new Thread(p =>
                            {
                                var agentName = string.Empty;
                                var groupName = string.Empty;
                                if (Resolvers.AgentUUIDToName(Client,
                                    o,
                                    corradeConfiguration.ServicesTimeout,
                                    ref agentName) &&
                                    Resolvers.GroupUUIDToName(Client, group.Key,
                                        corradeConfiguration.ServicesTimeout,
                                        ref groupName))
                                {
                                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                        () => SendNotification(
                                            Configuration.Notifications.GroupMembership,
                                            new GroupMembershipEventArgs
                                            {
                                                AgentName = agentName,
                                                AgentUUID = o,
                                                Action = Enumerations.Action.JOINED,
                                                GroupName = groupName,
                                                GroupUUID = group.Key
                                            }),
                                        corradeConfiguration.MaximumNotificationThreads);
                                }
                            })
                            {IsBackground = true}.Start();
                        }

                        var softBan = new SoftBan();
                        lock (GroupSoftBansLock)
                        {
                            if (GroupSoftBans.ContainsKey(group.Key))
                            {
                                softBan = GroupSoftBans[group.Key].AsParallel().FirstOrDefault(p => p.Agent.Equals(o));
                            }
                        }

                        // if the agent has been soft banned, eject them.
                        if (!softBan.Equals(default(SoftBan)))
                        {
                            new Thread(() =>
                            {
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        group.Key,
                                        GroupPowers.Eject,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        group.Key,
                                        GroupPowers.RemoveMember,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        group.Key,
                                        GroupPowers.GroupBanAccess,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    Feedback(
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                    return;
                                }
                                var targetGroup = new Group();
                                if (
                                    !Services.RequestGroup(Client, group.Key,
                                        corradeConfiguration.ServicesTimeout,
                                        ref targetGroup))
                                    return;
                                var GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                                var rolesMembers = new List<KeyValuePair<UUID, UUID>>();
                                var groupRolesMembersRequestUUID = UUID.Zero;
                                EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler =
                                    (s, args) =>
                                    {
                                        if (!groupRolesMembersRequestUUID.Equals(args.RequestID)) return;
                                        rolesMembers = args.RolesMembers;
                                        GroupRoleMembersReplyEvent.Set();
                                    };
                                lock (Locks.ClientInstanceGroupsLock)
                                {
                                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                                    groupRolesMembersRequestUUID = Client.Groups.RequestGroupRolesMembers(group.Key);
                                    if (
                                        !GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                            false))
                                    {
                                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                        Feedback(
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS));
                                        return;
                                    }
                                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                }
                                lock (Locks.ClientInstanceGroupsLock)
                                {
                                    switch (
                                        !rolesMembers.AsParallel()
                                            .Any(p => p.Key.Equals(targetGroup.OwnerRole) && p.Value.Equals(o)))
                                    {
                                        case true:
                                            rolesMembers.AsParallel().Where(
                                                p => p.Value.Equals(o))
                                                .ForAll(
                                                    p => Client.Groups.RemoveFromRole(group.Key, p.Key,
                                                        o));
                                            break;
                                        default:
                                            Feedback(
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ScriptError.CANNOT_EJECT_OWNERS));
                                            return;
                                    }
                                }

                                // No hard time requested so no need to ban.
                                switch (softBan.Time.Equals(0))
                                {
                                    case false:
                                        // Get current group bans.
                                        Dictionary<UUID, DateTime> bannedAgents = null;
                                        if (
                                            !Services.GetGroupBans(Client, group.Key,
                                                corradeConfiguration.ServicesTimeout,
                                                ref bannedAgents) || bannedAgents == null)
                                        {
                                            Feedback(
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                                Reflection.GetDescriptionFromEnumValue(
                                                    Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST));
                                            break;
                                        }

                                        // If the agent is not banned, then ban the agent.
                                        if (!bannedAgents.ContainsKey(o))
                                        {
                                            // Update the soft bans list.
                                            lock (GroupSoftBansLock)
                                            {
                                                if (GroupSoftBans.ContainsKey(group.Key))
                                                {
                                                    GroupSoftBans[group.Key].RemoveWhere(
                                                        p => p.Agent.Equals(softBan.Agent));
                                                    GroupSoftBans[group.Key].Add(new SoftBan
                                                    {
                                                        Agent = softBan.Agent,
                                                        FirstName = softBan.FirstName,
                                                        LastName = softBan.LastName,
                                                        Time = softBan.Time,
                                                        Note = softBan.Note,
                                                        Timestamp = softBan.Timestamp,
                                                        Last =
                                                            DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP)
                                                    });
                                                }
                                            }

                                            // Do not re-add the group hard soft-ban in case it already exists.
                                            if (bannedAgents.ContainsKey(o))
                                                break;

                                            if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                                bannedAgents.Count + 1 >
                                                wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_BANS)
                                            {
                                                Feedback(
                                                    Reflection.GetDescriptionFromEnumValue(
                                                        Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                                    Reflection.GetDescriptionFromEnumValue(
                                                        Enumerations.ScriptError
                                                            .BAN_WOULD_EXCEED_MAXIMUM_BAN_LIST_LENGTH));
                                                break;
                                            }

                                            // Now ban the agent.
                                            lock (Locks.ClientInstanceGroupsLock)
                                            {
                                                var GroupBanEvent = new ManualResetEvent(false);
                                                Client.Groups.RequestBanAction(group.Key,
                                                    GroupBanAction.Ban, new[] {o}, (s, a) => { GroupBanEvent.Set(); });
                                                if (
                                                    !GroupBanEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                        false))
                                                {
                                                    Feedback(
                                                        Reflection.GetDescriptionFromEnumValue(
                                                            Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                                        Reflection.GetDescriptionFromEnumValue(
                                                            Enumerations.ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST));
                                                }
                                            }
                                        }
                                        break;
                                }

                                // Now eject them.
                                var GroupEjectEvent = new ManualResetEvent(false);
                                var succeeded = false;
                                EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (s, args) =>
                                {
                                    succeeded = args.Success;
                                    GroupEjectEvent.Set();
                                };
                                lock (Locks.ClientInstanceGroupsLock)
                                {
                                    Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                                    Client.Groups.EjectUser(group.Key, o);
                                    if (!GroupEjectEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                                        Feedback(
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                            Reflection.GetDescriptionFromEnumValue(
                                                Enumerations.ScriptError.TIMEOUT_EJECTING_AGENT));
                                        return;
                                    }
                                    Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                                }
                                if (!succeeded)
                                {
                                    Feedback(
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ConsoleMessage.UNABLE_TO_APPLY_SOFT_BAN),
                                        Reflection.GetDescriptionFromEnumValue(
                                            Enumerations.ScriptError.COULD_NOT_EJECT_AGENT));
                                }
                            })
                            {IsBackground = true}.Start();
                        }
                    });
                    e.OldItems?.OfType<UUID>().ToList().AsParallel().ForAll(o =>
                    {
                        // Send membership notification if enabled.
                        if (corradeConfiguration.Groups.AsParallel()
                            .Any(
                                p =>
                                    p.UUID.Equals(group.Key) &&
                                    p.NotificationMask.IsMaskFlagSet(Configuration.Notifications.GroupMembership)))
                        {
                            new Thread(p =>
                            {
                                var agentName = string.Empty;
                                var groupName = string.Empty;
                                if (Resolvers.AgentUUIDToName(Client,
                                    o,
                                    corradeConfiguration.ServicesTimeout,
                                    ref agentName) &&
                                    Resolvers.GroupUUIDToName(Client, group.Key,
                                        corradeConfiguration.ServicesTimeout,
                                        ref groupName))
                                {
                                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                        () => SendNotification(
                                            Configuration.Notifications.GroupMembership,
                                            new GroupMembershipEventArgs
                                            {
                                                AgentName = agentName,
                                                AgentUUID = o,
                                                Action = Enumerations.Action.PARTED,
                                                GroupName = groupName,
                                                GroupUUID = group.Key
                                            }),
                                        corradeConfiguration.MaximumNotificationThreads);
                                }
                            })
                            {IsBackground = true}.Start();
                        }
                    });
                    break;
            }
        }

        private static void HandleGroupSoftBansChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var group =
                GroupSoftBans.FirstOrDefault(
                    o => ReferenceEquals(o.Value, sender as Collections.ObservableHashSet<UUID>));
            if (group.Equals(default(KeyValuePair<UUID, Collections.ObservableHashSet<UUID>>)))
                return;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Reset:
                    e.NewItems?.OfType<UUID>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o =>
                        {
                            if (corradeConfiguration.EnableHorde)
                                HordeDistributeGroupSoftBan(group.Key, o,
                                    Configuration.HordeDataSynchronizationOption.Add);
                        });
                    e.OldItems?.OfType<UUID>()
                        .ToList()
                        .AsParallel()
                        .ForAll(o =>
                        {
                            if (corradeConfiguration.EnableHorde)
                                HordeDistributeGroupSoftBan(group.Key, o,
                                    Configuration.HordeDataSynchronizationOption.Remove);
                        });
                    break;
            }
        }

        private static void HandleSynBotLearning(object sender, LearningEventArgs e)
        {
            try
            {
                e.Document.Save(Path.Combine(
                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                    SIML_BOT_CONSTANTS.EVOLVE_DIRECTORY,
                    SIML_BOT_CONSTANTS.LEARNED_FILE));
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SAVING_SIML_BOT_LEARNING_FILE),
                    ex.Message);
            }
        }

        private static void HandleSynBotMemorizing(object sender, MemorizingEventArgs e)
        {
            try
            {
                e.Document.Save(Path.Combine(
                    Directory.GetCurrentDirectory(), SIML_BOT_CONSTANTS.ROOT_DIRECTORY,
                    SIML_BOT_CONSTANTS.EVOLVE_DIRECTORY,
                    SIML_BOT_CONSTANTS.MEMORIZED_FILE));
            }
            catch (Exception ex)
            {
                Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SAVING_SIML_BOT_MEMORIZING_FILE),
                    ex.Message);
            }
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