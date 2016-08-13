///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using wasSharp;

namespace CorradeConfiguration
{
    [Serializable]
    public class Configuration
    {
        /// <summary>
        ///     Possible input and output filters.
        /// </summary>
        public enum Filter : uint
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] NONE = 0,
            [XmlEnum(Name = "RFC1738")] [Reflection.NameAttribute("RFC1738")] RFC1738,
            [XmlEnum(Name = "RFC3986")] [Reflection.NameAttribute("RFC3986")] RFC3986,
            [XmlEnum(Name = "ENIGMA")] [Reflection.NameAttribute("ENIGMA")] ENIGMA,
            [XmlEnum(Name = "VIGENERE")] [Reflection.NameAttribute("VIGENERE")] VIGENERE,
            [XmlEnum(Name = "ATBASH")] [Reflection.NameAttribute("ATBASH")] ATBASH,
            [XmlEnum(Name = "BASE64")] [Reflection.NameAttribute("BASE64")] BASE64,
            [XmlEnum(Name = "AES")] [Reflection.NameAttribute("AES")] AES
        }

        /// <summary>
        ///     Corrade horde synchronization options.
        /// </summary>
        [Flags]
        public enum HordeDataSynchronization : ulong
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] None = 0uL,
            [XmlEnum(Name = "agent")] [Reflection.NameAttribute("agent")] Agent = 1uL,
            [XmlEnum(Name = "group")] [Reflection.NameAttribute("group")] Group = 2uL,
            [XmlEnum(Name = "region")] [Reflection.NameAttribute("region")] Region = 4uL,
            [XmlEnum(Name = "asset")] [Reflection.NameAttribute("asset")] Asset = 8uL,
            [XmlEnum(Name = "mute")] [Reflection.NameAttribute("mute")] Mute = 16uL
        }

        /// <summary>
        ///     Data synchronization options.
        /// </summary>
        [Flags]
        public enum HordeDataSynchronizationOption : ulong
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] None = 0uL,
            [XmlEnum(Name = "add")] [Reflection.NameAttribute("add")] Add = 1uL,
            [XmlEnum(Name = "remove")] [Reflection.NameAttribute("remove")] Remove = 2uL
        }

        /// <summary>
        ///     An enumeration of various compression methods
        ///     supproted by Corrade's internal HTTP server.
        /// </summary>
        public enum HTTPCompressionMethod : uint
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] NONE,
            [XmlEnum(Name = "deflate")] [Reflection.NameAttribute("deflate")] DEFLATE,
            [XmlEnum(Name = "gzip")] [Reflection.NameAttribute("gzip")] GZIP
        }

        /// <summary>
        ///     Corrade notification types.
        /// </summary>
        [Flags]
        public enum Notifications : ulong
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] NONE = 0uL,
            [XmlEnum(Name = "alert")] [Reflection.NameAttribute("alert")] AlertMessage = 1uL,
            [XmlEnum(Name = "region")] [Reflection.NameAttribute("region")] RegionMessage = 2uL,
            [XmlEnum(Name = "group")] [Reflection.NameAttribute("group")] GroupMessage = 4uL,
            [XmlEnum(Name = "balance")] [Reflection.NameAttribute("balance")] Balance = 8uL,
            [XmlEnum(Name = "message")] [Reflection.NameAttribute("message")] InstantMessage = 16uL,
            [XmlEnum(Name = "notice")] [Reflection.NameAttribute("notice")] GroupNotice = 32uL,
            [XmlEnum(Name = "local")] [Reflection.NameAttribute("local")] LocalChat = 64uL,
            [XmlEnum(Name = "dialog")] [Reflection.NameAttribute("dialog")] ScriptDialog = 128uL,
            [XmlEnum(Name = "friendship")] [Reflection.NameAttribute("friendship")] Friendship = 256uL,
            [XmlEnum(Name = "inventory")] [Reflection.NameAttribute("inventory")] Inventory = 512uL,
            [XmlEnum(Name = "permission")] [Reflection.NameAttribute("permission")] ScriptPermission = 1024uL,
            [XmlEnum(Name = "lure")] [Reflection.NameAttribute("lure")] TeleportLure = 2048uL,
            [XmlEnum(Name = "effect")] [Reflection.NameAttribute("effect")] ViewerEffect = 4096uL,
            [XmlEnum(Name = "collision")] [Reflection.NameAttribute("collision")] MeanCollision = 8192uL,
            [XmlEnum(Name = "crossing")] [Reflection.NameAttribute("crossing")] RegionCrossed = 16384uL,
            [XmlEnum(Name = "terse")] [Reflection.NameAttribute("terse")] TerseUpdates = 32768uL,
            [XmlEnum(Name = "typing")] [Reflection.NameAttribute("typing")] Typing = 65536uL,
            [XmlEnum(Name = "invite")] [Reflection.NameAttribute("invite")] GroupInvite = 131072uL,
            [XmlEnum(Name = "economy")] [Reflection.NameAttribute("economy")] Economy = 262144uL,
            [XmlEnum(Name = "membership")] [Reflection.NameAttribute("membership")] GroupMembership = 524288uL,
            [XmlEnum(Name = "url")] [Reflection.NameAttribute("url")] LoadURL = 1048576uL,
            [XmlEnum(Name = "ownersay")] [Reflection.NameAttribute("ownersay")] OwnerSay = 2097152uL,
            [XmlEnum(Name = "regionsayto")] [Reflection.NameAttribute("regionsayto")] RegionSayTo = 4194304uL,
            [XmlEnum(Name = "objectim")] [Reflection.NameAttribute("objectim")] ObjectInstantMessage = 8388608uL,
            [XmlEnum(Name = "rlv")] [Reflection.NameAttribute("rlv")] RLVMessage = 16777216uL,
            [XmlEnum(Name = "debug")] [Reflection.NameAttribute("debug")] DebugMessage = 33554432uL,
            [XmlEnum(Name = "avatars")] [Reflection.NameAttribute("avatars")] RadarAvatars = 67108864uL,
            [XmlEnum(Name = "primitives")] [Reflection.NameAttribute("primitives")] RadarPrimitives = 134217728uL,
            [XmlEnum(Name = "control")] [Reflection.NameAttribute("control")] ScriptControl = 268435456uL,
            [XmlEnum(Name = "sit")] [Reflection.NameAttribute("sit")] SitChanged = 536870912uL,
            [XmlEnum(Name = "animation")] [Reflection.NameAttribute("animation")] AnimationsChanged = 1073741824uL,
            [XmlEnum(Name = "outfit")] [Reflection.NameAttribute("outfit")] OutfitChanged = 2147483648uL,
            [XmlEnum(Name = "feed")] [Reflection.NameAttribute("feed")] Feed = 4294967296uL,
            [XmlEnum(Name = "sound")] [Reflection.NameAttribute("sound")] Sound = 8589934592uL,
            [XmlEnum(Name = "conference")] [Reflection.NameAttribute("conference")] Conference = 17179869184ul
        }

        /// <summary>
        ///     Corrade permissions.
        /// </summary>
        [Flags]
        public enum Permissions : ulong
        {
            [XmlEnum(Name = "none")] [Reflection.NameAttribute("none")] None = 0uL,
            [XmlEnum(Name = "movement")] [Reflection.NameAttribute("movement")] Movement = 1uL,
            [XmlEnum(Name = "economy")] [Reflection.NameAttribute("economy")] Economy = 2uL,
            [XmlEnum(Name = "land")] [Reflection.NameAttribute("land")] Land = 4uL,
            [XmlEnum(Name = "grooming")] [Reflection.NameAttribute("grooming")] Grooming = 8uL,
            [XmlEnum(Name = "inventory")] [Reflection.NameAttribute("inventory")] Inventory = 16uL,
            [XmlEnum(Name = "interact")] [Reflection.NameAttribute("interact")] Interact = 32uL,
            [XmlEnum(Name = "mute")] [Reflection.NameAttribute("mute")] Mute = 64uL,
            [XmlEnum(Name = "database")] [Reflection.NameAttribute("database")] Database = 128uL,
            [XmlEnum(Name = "notifications")] [Reflection.NameAttribute("notifications")] Notifications = 256uL,
            [XmlEnum(Name = "talk")] [Reflection.NameAttribute("talk")] Talk = 512uL,
            [XmlEnum(Name = "directory")] [Reflection.NameAttribute("directory")] Directory = 1024uL,
            [XmlEnum(Name = "system")] [Reflection.NameAttribute("system")] System = 2048uL,
            [XmlEnum(Name = "friendship")] [Reflection.NameAttribute("friendship")] Friendship = 4096uL,
            [XmlEnum(Name = "execute")] [Reflection.NameAttribute("execute")] Execute = 8192uL,
            [XmlEnum(Name = "group")] [Reflection.NameAttribute("group")] Group = 16384uL,
            [XmlEnum(Name = "filter")] [Reflection.NameAttribute("filter")] Filter = 32768uL,
            [XmlEnum(Name = "schedule")] [Reflection.NameAttribute("schedule")] Schedule = 65536uL,
            [XmlEnum(Name = "feed")] [Reflection.NameAttribute("feed")] Feed = 131072uL
        }

        private readonly object ClientInstanceConfigurationLock = new object();
        private byte[] _AESIV;
        private byte[] _AESKey;
        private bool _autoActivateGroup;
        private uint _autoActivateGroupDelay = 5000;
        private string _bindIPAddress = string.Empty;
        private uint _callbackQueueLength = 100;
        private uint _callbackThrottle = 1000;
        private uint _callbackTimeout = 5000;
        private UUID _clientIdentificationTag = new UUID(@"0705230f-cbd0-99bd-040b-28eb348b5255");
        private bool _clientLogEnabled = true;
        private string _clientLogFile = @"logs/Corrade.log";
        private string _conferenceMessageLogDirectory = @"logs/conference";
        private bool _conferenceMessageLogEnabled;
        private uint _connectionIdleTime = 900000;
        private uint _connectionLimit = 100;
        private Time.DecayingAlarm.DECAY_TYPE _dataDecayType = Time.DecayingAlarm.DECAY_TYPE.ARITHMETIC;
        private uint _dataTimeout = 2500;
        private string _driveIdentifierHash = string.Empty;
        private bool _enableHorde;
        private bool _enableHTTPServer;
        private bool _enableHTTPServerAuthentication;
        private bool _enableMasterPasswordOverride;
        private bool _enableRLV;
        private bool _enableSIML;
        private bool _enableTCPNotificationsServer;

        private ENIGMA _enigmaConfiguration = new ENIGMA
        {
            rotors = new[] {'3', 'g', '1'},
            plugs = new[] {'z', 'p', 'q'},
            reflector = 'b'
        };

        private int _exitCodeAbnormal = -2;
        private int _exitCodeExpected = -1;
        private uint _feedsUpdateInterval = 60000;
        private string _firstName = string.Empty;
        private uint _groupCreateFee = 100;
        private HashSet<Group> _groups = new HashSet<Group>();
        private HashSet<HordePeer> _hordePeers = new HashSet<HordePeer>();
        private uint _HTTPServerBodyTimeout = 5000;
        private HTTPCompressionMethod _HTTPServerCompression = HTTPCompressionMethod.NONE;
        private uint _HTTPServerDrainTimeout = 10000;
        private uint _HTTPServerHeaderTimeout = 2500;
        private uint _HTTPServerIdleTimeout = 2500;
        private bool _HTTPServerKeepAlive = true;
        private string _HTTPServerPassword = string.Empty;
        private string _HTTPServerPrefix = @"http://+:8080/";
        private uint _HTTPServerQueueTimeout = 10000;
        private uint _HTTPServerTimeout = 5000;
        private string _HTTPServerUsername = string.Empty;
        private List<Filter> _inputFilters = new List<Filter>();
        private string _instantMessageLogDirectory = @"logs/im";
        private bool _instantMessageLogEnabled;
        private string _lastName = string.Empty;
        private string _localMessageLogDirectory = @"logs/local";
        private bool _localMessageLogEnabled;
        private string _loginURL = @"https://login.agni.lindenlab.com/cgi-bin/login.cgi";
        private uint _logoutGrace = 2500;
        private string _masterPasswordOverride = string.Empty;
        private HashSet<Master> _masters = new HashSet<Master>();
        private uint _maximumCommandThreads = 10;
        private uint _maximumInstantMessageThreads = 10;
        private uint _maximumLogThreads = 40;
        private uint _maximumNotificationThreads = 10;
        private uint _maximumPOSTThreads = 25;
        private uint _maximumRLVThreads = 10;
        private uint _membershipSweepInterval = 60000;
        private string _networkCardMAC = string.Empty;
        private uint _notificationQueueLength = 100;
        private uint _notificationThrottle = 1000;
        private uint _notificationTimeout = 5000;
        private List<Filter> _outputFilters = new List<Filter>();
        private string _password = string.Empty;
        private float _range = 64;
        private uint _rebakeDelay = 1000;
        private string _regionMessageLogDirectory = @"logs/region";
        private bool _regionMessageLogEnabled;
        private uint _schedulerExpiration = 60000;
        private uint _schedulesResolution = 1000;
        private uint _servicesTimeout = 60000;
        private string _startLocation = @"last";
        private uint _TCPnotificationQueueLength = 100;
        private string _TCPNotificationsServerAddress = @"0.0.0.0";
        private uint _TCPNotificationsServerPort = 8095;
        private uint _TCPnotificationThrottle = 1000;
        private uint _throttleAsset = 2500000;
        private uint _throttleCloud = 2500000;
        private uint _throttleLand = 2500000;
        private uint _throttleResend = 2500000;
        private uint _throttleTask = 2500000;
        private uint _throttleTexture = 2500000;
        private uint _throttleTotal = 5000000;
        private uint _throttleWind = 2500000;
        private bool _TOSAccepted;
        private bool _useExpect100Continue;
        private bool _useNaggle;
        private string _vigenereSecret = string.Empty;

        public string FirstName
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _firstName;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _firstName = value;
                }
            }
        }

        public string LastName
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _lastName;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _lastName = value;
                }
            }
        }

        public string MasterPasswordOverride
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _masterPasswordOverride;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _masterPasswordOverride = value;
                }
            }
        }

        public bool EnableMasterPasswordOverride
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableMasterPasswordOverride;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableMasterPasswordOverride = value;
                }
            }
        }

        public bool EnableHorde
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableHorde;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableHorde = value;
                }
            }
        }

        public string Password
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _password;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _password = value;
                }
            }
        }

        public string LoginURL
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _loginURL;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _loginURL = value;
                }
            }
        }

        public string InstantMessageLogDirectory
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _instantMessageLogDirectory;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _instantMessageLogDirectory = value;
                }
            }
        }

        public bool InstantMessageLogEnabled
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _instantMessageLogEnabled;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _instantMessageLogEnabled = value;
                }
            }
        }

        public string LocalMessageLogDirectory
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _localMessageLogDirectory;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _localMessageLogDirectory = value;
                }
            }
        }

        public bool LocalMessageLogEnabled
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _localMessageLogEnabled;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _localMessageLogEnabled = value;
                }
            }
        }

        public string ConferenceMessageLogDirectory
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _conferenceMessageLogDirectory;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _conferenceMessageLogDirectory = value;
                }
            }
        }

        public bool ConferenceMessageLogEnabled
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _conferenceMessageLogEnabled;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _conferenceMessageLogEnabled = value;
                }
            }
        }

        public string RegionMessageLogDirectory
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _regionMessageLogDirectory;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _regionMessageLogDirectory = value;
                }
            }
        }

        public bool RegionMessageLogEnabled
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _regionMessageLogEnabled;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _regionMessageLogEnabled = value;
                }
            }
        }

        public bool EnableHTTPServer
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableHTTPServer;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableHTTPServer = value;
                }
            }
        }

        public bool EnableHTTPServerAuthentication
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableHTTPServerAuthentication;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableHTTPServerAuthentication = value;
                }
            }
        }

        public string HTTPServerUsername
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerUsername;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerUsername = value;
                }
            }
        }

        public string HTTPServerPassword
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerPassword;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerPassword = value;
                }
            }
        }

        public bool EnableTCPNotificationsServer
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableTCPNotificationsServer;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableTCPNotificationsServer = value;
                }
            }
        }

        public bool EnableSIML
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableSIML;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableSIML = value;
                }
            }
        }

        public bool EnableRLV
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enableRLV;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enableRLV = value;
                }
            }
        }

        public string HTTPServerPrefix
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerPrefix;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerPrefix = value;
                }
            }
        }

        public uint TCPNotificationsServerPort
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _TCPNotificationsServerPort;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _TCPNotificationsServerPort = value;
                }
            }
        }

        public string TCPNotificationsServerAddress
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _TCPNotificationsServerAddress;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _TCPNotificationsServerAddress = value;
                }
            }
        }

        public uint HTTPServerTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerTimeout = value;
                }
            }
        }

        public uint HTTPServerDrainTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerDrainTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerDrainTimeout = value;
                }
            }
        }

        public uint HTTPServerBodyTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerBodyTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerBodyTimeout = value;
                }
            }
        }

        public uint HTTPServerHeaderTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerHeaderTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerHeaderTimeout = value;
                }
            }
        }

        public uint HTTPServerIdleTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerIdleTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerIdleTimeout = value;
                }
            }
        }

        public uint HTTPServerQueueTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerQueueTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerQueueTimeout = value;
                }
            }
        }

        public HTTPCompressionMethod HTTPServerCompression
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerCompression;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerCompression = value;
                }
            }
        }

        public uint ThrottleTotal
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleTotal;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleTotal = value;
                }
            }
        }

        public uint ThrottleLand
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleLand;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleLand = value;
                }
            }
        }

        public uint ThrottleTask
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleTask;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleTask = value;
                }
            }
        }

        public uint ThrottleTexture
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleTexture;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleTexture = value;
                }
            }
        }

        public uint ThrottleWind
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleWind;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleWind = value;
                }
            }
        }

        public uint ThrottleResend
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleResend;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleResend = value;
                }
            }
        }

        public uint ThrottleAsset
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleAsset;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleAsset = value;
                }
            }
        }

        public uint ThrottleCloud
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _throttleCloud;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _throttleCloud = value;
                }
            }
        }

        public bool HTTPServerKeepAlive
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _HTTPServerKeepAlive;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _HTTPServerKeepAlive = value;
                }
            }
        }

        public uint CallbackTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _callbackTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _callbackTimeout = value;
                }
            }
        }

        public uint CallbackThrottle
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _callbackThrottle;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _callbackThrottle = value;
                }
            }
        }

        public uint CallbackQueueLength
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _callbackQueueLength;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _callbackQueueLength = value;
                }
            }
        }

        public uint NotificationTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _notificationTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _notificationTimeout = value;
                }
            }
        }

        public uint NotificationThrottle
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _notificationThrottle;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _notificationThrottle = value;
                }
            }
        }

        public uint TCPNotificationThrottle
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _TCPnotificationThrottle;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _TCPnotificationThrottle = value;
                }
            }
        }

        public uint NotificationQueueLength
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _notificationQueueLength;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _notificationQueueLength = value;
                }
            }
        }

        public uint TCPNotificationQueueLength
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _TCPnotificationQueueLength;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _TCPnotificationQueueLength = value;
                }
            }
        }

        public uint ConnectionLimit
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _connectionLimit;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _connectionLimit = value;
                }
            }
        }

        public uint ConnectionIdleTime
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _connectionIdleTime;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _connectionIdleTime = value;
                }
            }
        }

        public float Range
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _range;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _range = value;
                }
            }
        }

        public uint SchedulerExpiration
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _schedulerExpiration;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _schedulerExpiration = value;
                }
            }
        }

        public uint MaximumNotificationThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumNotificationThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumNotificationThreads = value;
                }
            }
        }

        public uint MaximumCommandThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumCommandThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumCommandThreads = value;
                }
            }
        }

        public uint MaximumRLVThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumRLVThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumRLVThreads = value;
                }
            }
        }

        public uint MaximumInstantMessageThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumInstantMessageThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumInstantMessageThreads = value;
                }
            }
        }

        public uint MaximumLogThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumLogThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumLogThreads = value;
                }
            }
        }

        public uint MaximumPOSTThreads
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _maximumPOSTThreads;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _maximumPOSTThreads = value;
                }
            }
        }

        public bool UseNaggle
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _useNaggle;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _useNaggle = value;
                }
            }
        }

        public bool UseExpect100Continue
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _useExpect100Continue;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _useExpect100Continue = value;
                }
            }
        }

        public uint ServicesTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _servicesTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _servicesTimeout = value;
                }
            }
        }

        public uint DataTimeout
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _dataTimeout;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _dataTimeout = value;
                }
            }
        }

        public Time.DecayingAlarm.DECAY_TYPE DataDecayType
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _dataDecayType;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _dataDecayType = value;
                }
            }
        }

        public uint RebakeDelay
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _rebakeDelay;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _rebakeDelay = value;
                }
            }
        }

        public uint MembershipSweepInterval
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _membershipSweepInterval;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _membershipSweepInterval = value;
                }
            }
        }

        public uint FeedsUpdateInterval
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _feedsUpdateInterval;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _feedsUpdateInterval = value;
                }
            }
        }

        public bool TOSAccepted
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _TOSAccepted;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _TOSAccepted = value;
                }
            }
        }

        public UUID ClientIdentificationTag
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _clientIdentificationTag;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _clientIdentificationTag = value;
                }
            }
        }

        public string StartLocation
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _startLocation;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _startLocation = value;
                }
            }
        }

        public string BindIPAddress
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _bindIPAddress;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _bindIPAddress = value;
                }
            }
        }

        public string NetworkCardMAC
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _networkCardMAC;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _networkCardMAC = value;
                }
            }
        }

        public string DriveIdentifierHash
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _driveIdentifierHash;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _driveIdentifierHash = value;
                }
            }
        }

        public string ClientLogFile
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _clientLogFile;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _clientLogFile = value;
                }
            }
        }

        public bool ClientLogEnabled
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _clientLogEnabled;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _clientLogEnabled = value;
                }
            }
        }

        public bool AutoActivateGroup
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _autoActivateGroup;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _autoActivateGroup = value;
                }
            }
        }

        public uint AutoActivateGroupDelay
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _autoActivateGroupDelay;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _autoActivateGroupDelay = value;
                }
            }
        }

        public uint GroupCreateFee
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _groupCreateFee;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _groupCreateFee = value;
                }
            }
        }

        public int ExitCodeExpected
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _exitCodeExpected;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _exitCodeExpected = value;
                }
            }
        }

        public int ExitCodeAbnormal
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _exitCodeAbnormal;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _exitCodeAbnormal = value;
                }
            }
        }

        public HashSet<Group> Groups
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _groups;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _groups = value;
                }
            }
        }

        public HashSet<Master> Masters
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _masters;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _masters = value;
                }
            }
        }

        public HashSet<HordePeer> HordePeers
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _hordePeers;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _hordePeers = value;
                }
            }
        }

        public List<Filter> InputFilters
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _inputFilters;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _inputFilters = value;
                }
            }
        }

        public List<Filter> OutputFilters
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _outputFilters;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _outputFilters = value;
                }
            }
        }

        public byte[] AESKey
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _AESKey;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _AESKey = value;
                }
            }
        }

        public byte[] AESIV
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _AESIV;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _AESIV = value;
                }
            }
        }

        public string VIGENERESecret
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _vigenereSecret;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _vigenereSecret = value;
                }
            }
        }

        public ENIGMA ENIGMAConfiguration
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _enigmaConfiguration;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _enigmaConfiguration = value;
                }
            }
        }

        public uint LogoutGrace
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _logoutGrace;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _logoutGrace = value;
                }
            }
        }

        public uint SchedulesResolution
        {
            get
            {
                lock (ClientInstanceConfigurationLock)
                {
                    return _schedulesResolution;
                }
            }
            set
            {
                lock (ClientInstanceConfigurationLock)
                {
                    _schedulesResolution = value;
                }
            }
        }

        public string Read(string file)
        {
            return File.ReadAllText(file, Encoding.UTF8);
        }

        public void Write(string file, string data)
        {
            File.WriteAllText(file, data, Encoding.UTF8);
        }

        public void Write(string file, XmlDocument document)
        {
            using (TextWriter writer = new StreamWriter(file, false, Encoding.UTF8))
            {
                document.Save(writer);
                writer.Flush();
            }
        }

        public void Save(string file, ref Configuration configuration)
        {
            using (var writer = new StreamWriter(file, false, Encoding.UTF8))
            {
                var serializer = new XmlSerializer(typeof (Configuration));
                serializer.Serialize(writer, configuration);
                writer.Flush();
            }
        }

        public void Load(string file, ref Configuration configuration)
        {
            using (var stream = new StreamReader(file, Encoding.UTF8))
            {
                var serializer =
                    new XmlSerializer(typeof (Configuration));
                var loadedConfiguration = (Configuration) serializer.Deserialize(stream);
                configuration = loadedConfiguration;
            }
        }

        public void Load(Stream stream, ref Configuration configuration)
        {
            var serializer = new XmlSerializer(typeof (Configuration));
            var loadedConfiguration = (Configuration) serializer.Deserialize(stream);
            configuration = loadedConfiguration;
        }

        /// <summary>
        ///     Group structure.
        /// </summary>
        public struct Group
        {
            public string ChatLog;
            public bool ChatLogEnabled;
            public string DatabaseFile;
            public string Name;
            public HashSet<Notifications> Notifications;
            public string Password;
            public HashSet<Permissions> Permissions;
            public uint Schedules;
            public UUID UUID;
            public uint Workers;

            public ulong NotificationMask
            {
                get
                {
                    return Notifications != null && Notifications.Any()
                        ? Notifications.Cast<ulong>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }

            public ulong PermissionMask
            {
                get
                {
                    return Permissions != null && Permissions.Any()
                        ? Permissions.Cast<ulong>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }
        }

        /// <summary>
        ///     Masters structure.
        /// </summary>
        public struct Master
        {
            public string FirstName;
            public string LastName;
        }

        /// <summary>
        ///     ENIGMA machine settings.
        /// </summary>
        public struct ENIGMA
        {
            public char[] plugs;
            public char reflector;
            public char[] rotors;
        }

        /// <summary>
        ///     Horde peer.
        /// </summary>
        public class HordePeer
        {
            public string URL;
            public string Username;
            public string Password;
            public string SharedSecret;

            public Collections.SerializableDictionary<HordeDataSynchronization, HordeDataSynchronizationOption>
                DataSynchronization =
                    new Collections.SerializableDictionary<HordeDataSynchronization, HordeDataSynchronizationOption>();

            public ulong SynchronizationMask
            {
                get
                {
                    return DataSynchronization != null && DataSynchronization.Any()
                        ? DataSynchronization.Keys.Cast<ulong>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }

            public bool HasDataSynchronizationOption(HordeDataSynchronization sync, HordeDataSynchronizationOption option)
            {
                return DataSynchronization.ContainsKey(sync) && BitTwiddling.IsMaskFlagSet(DataSynchronization[sync], option);
            }
        }
    }
}