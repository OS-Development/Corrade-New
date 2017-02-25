///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using wasSharp;

namespace Corrade.Constants
{
    /// <summary>
    ///     Constants used by Corrade.
    /// </summary>
    public class CORRADE_CONSTANTS
    {
        /// <summary>
        ///     Copyright.
        /// </summary>
        public const string COPYRIGHT = @"(c) Copyright 2013 Wizardry and Steamworks";

        public const string WIZARDRY_AND_STEAMWORKS = @"Wizardry and Steamworks";
        public const string CORRADE = @"Corrade";
        public const string WIZARDRY_AND_STEAMWORKS_WEBSITE = @"http://grimore.org";

        /// <summary>
        ///     Censor characters for passwords.
        /// </summary>
        public const string PASSWORD_CENSOR = "***";

        /// <summary>
        ///     Corrade channel sent to the simulator.
        /// </summary>
        public const string CLIENT_CHANNEL = @"[Wizardry and Steamworks]:Corrade";

        public const uint TWITTER_MAXIMUM_TWEET_LENGTH = 140;
        public const string CURRENT_OUTFIT_FOLDER_NAME = @"Current Outfit";
        public const string DEFAULT_SERVICE_NAME = @"Corrade";
        public const string LOG_FACILITY = @"Application";
        public const string WEB_REQUEST = @"Web Request";
        public const string CONFIGURATION_FILE = @"Corrade.ini";
        public const string DATE_TIME_STAMP = @"dd-MM-yyyy HH:mm:ss";
        public const string INVENTORY_CACHE_FILE = @"Inventory.cache";
        public const string AGENT_CACHE_FILE = @"Agent.cache";
        public const string GROUP_CACHE_FILE = @"Group.cache";
        public const string REGION_CACHE_FILE = @"Region.cache";
        public const char PATH_SEPARATOR = '/';
        public const char PATH_SEPARATOR_ESCAPE = '\\';
        public const string ERROR_SEPARATOR = @" : ";
        public const string CACHE_DIRECTORY = @"cache";
        public const string ASSET_CACHE_DIRECTORY = @"assets";
        public const string LOG_FILE_EXTENSION = @"log";
        public const string BAYES_CLASSIFICATION_EXTENSION = @"json";
        public const string TEMPLATES_DIRECTORY = @"templates";
        public const string NOTIFICATIONS_TEMPLATE_DIRECTORY = @"notifications";
        public const string STATE_DIRECTORY = @"state";
        public const string LAST_EXEC_STATE_FILE = @"LastExecution.state";
        public const string NOTIFICATIONS_STATE_FILE = @"Notifications.state";
        public const string GROUP_MEMBERS_STATE_FILE = @"GroupMembers.state";
        public const string GROUP_SCHEDULES_STATE_FILE = @"GroupSchedules.state";
        public const string GROUP_COOKIES_STATE_FILE = @"GroupCookies.state";
        public const string MOVEMENT_STATE_FILE = @"Movement.state";
        public const string FEEDS_STATE_FILE = @"Feeds.state";
        public const string CONFERENCE_STATE_FILE = @"Conferences.state";
        public const string GROUP_SOFT_BAN_STATE_FILE = @"GroupSoftBans.state";
        public const string LIBS_DIRECTORY = @"libs";
        public const string BAYES_DIRECTORY = @"Bayes";
        public const string LANGUAGE_PROFILE_FILE = @"Core14.profile.xml";
        public static readonly Regex OneOrMoRegex = new Regex(@".+?", RegexOptions.Compiled);

        public static readonly Regex InventoryOfferObjectNameRegEx = new Regex(@"^[']{0,1}(.+?)(('\s)|$)",
            RegexOptions.Compiled);

        public static readonly Regex EjectedFromGroupRegEx =
            new Regex(@"You have been ejected from '(.+?)' by .+?\.$", RegexOptions.Compiled);

        public static readonly string GROUP_MESSAGE_LOG_MESSAGE_FORMAT = @"[{0}] {1} {2} : {3}";

        public static readonly Regex GroupMessageLogRegex = new Regex(@"^\[(.+?)\] (.+?) (.+?) : (.+?)$",
            RegexOptions.Compiled);

        public static readonly string REGION_MESSAGE_LOG_MESSAGE_FORMAT = @"[{0}] {1} {2} : {3}";

        public static readonly Regex RegionMessageLogRegex = new Regex(@"^\[(.+?)\] (.+?) (.+?) : (.+?)$",
            RegexOptions.Compiled);

        public static readonly string INSTANT_MESSAGE_LOG_MESSAGE_FORMAT = @"[{0}] {1} {2} : {3}";

        public static readonly Regex InstantMessageLogRegex = new Regex(@"^\[(.+?)\] (.+?) (.+?) : (.+?)$",
            RegexOptions.Compiled);

        public static readonly string CONFERENCE_MESSAGE_LOG_MESSAGE_FORMAT = @"[{0}] {1} {2} : {3}";

        public static readonly Regex ConferenceMessageLogRegex = new Regex(@"^\[(.+?)\] (.+?) (.+?) : (.+?)$",
            RegexOptions.Compiled);

        public static readonly string LOCAL_MESSAGE_LOG_MESSAGE_FORMAT = @"[{0}] {1} {2} ({3}) : {4}";

        public static readonly Regex LocalMessageLogRegex = new Regex(@"^\[(.+?)\] (.+?) (.+?) \((.+?)\) : (.+?)$",
            RegexOptions.Compiled);

        public static readonly Regex HttpPrefixRegex =
            new Regex(
                @"^(?<protocol>https?):\/\/([$\-_\.\+!\*'\(\),a-zA-Z0-9]+):(?<port>[0-9]{1,5}.*)/(?<path>[$\-_\.\+!\*'\(\),a-zA-Z0-9])*$",
                RegexOptions.Compiled);

        public static readonly Regex SHA1Regex = new Regex(@"[a-fA-F0-9]{40}", RegexOptions.Compiled);

        /// <summary>
        ///     Corrade version.
        /// </summary>
        public static readonly string CORRADE_VERSION = Assembly.GetEntryAssembly().GetName().Version.ToString();

        /// <summary>
        ///     Corrade user agent.
        /// </summary>
        public static readonly ProductInfoHeaderValue USER_AGENT = new ProductInfoHeaderValue(CORRADE, CORRADE_VERSION);

        /// <summary>
        ///     Corrade compile date.
        /// </summary>
        public static readonly string CORRADE_COMPILE_DATE = new DateTime(2000, 1, 1).Add(new TimeSpan(
            TimeSpan.TicksPerDay*Assembly.GetEntryAssembly().GetName().Version.Build + // days since 1 January 2000
            TimeSpan.TicksPerSecond*2*Assembly.GetEntryAssembly().GetName().Version.Revision)).ToLongDateString();

        /// <summary>
        ///     Corrade Logo.
        /// </summary>
        public static readonly object[] LOGO =
        {
            @"",
            @"       _..--=--..._  ",
            @"    .-'            '-.  .-.  ",
            @"   /.'              '.\/  /  ",
            @"  |=-     Corrade    -=| (  ",
            @"   \'.              .'/\  \  ",
            @"    '-.._____ _____.-'  '-'  ",
            @"          [_____]=8  ",
            @"               \  ",
            @"                 Good day!  ",
            @""
        };

        public static readonly object[] SUB_LOGO =
        {
            string.Format(Utils.EnUsCulture, "Version: {0}, Compiled: {1}", CORRADE_VERSION, CORRADE_COMPILE_DATE),
            string.Format(Utils.EnUsCulture, "Copyright: {0}", COPYRIGHT)
        };

        public static readonly string HORDE_SHARED_SECRET_HEADER = @"Corrade-Horde-SharedSecret";

        public static readonly Dictionary<string, string> ASSEMBLY_CUSTOM_ATTRIBUTES =
            Assembly.GetEntryAssembly()
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), true)
                .OfType<AssemblyMetadataAttribute>()
                .ToDictionary(o => o.Key, o => o.Value);

        public static readonly string NUCLEUS_ROOT = @"Nucleus";

        public static readonly string NUCLEUS_DEFAULT_DOCUMENT = @"index.html";

        /// <summary>
        ///     Conten-types that Corrade can send and receive.
        /// </summary>
        public struct CONTENT_TYPE
        {
            public const string TEXT_PLAIN = @"text/plain";
            public const string WWW_FORM_URLENCODED = @"application/x-www-form-urlencoded";
        }

        public struct PERMISSIONS
        {
            public const string NONE = @"------------------------------";
        }

        public struct PRIMTIVE_BODIES
        {
            [Reflection.NameAttribute("cube")] public static readonly Primitive.ConstructionData CUBE = new Primitive.
                ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 1.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Square,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("prism")] public static readonly Primitive.ConstructionData PRISM = new Primitive.
                ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 0.0f,
                PathScaleY = 1.0f,
                PathShearX = -0.5f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Square,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("pyramid")] public static readonly Primitive.ConstructionData PYRAMID = new Primitive
                .ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 0.0f,
                PathScaleY = 0.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Square,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("tetrahedron")] public static readonly Primitive.ConstructionData TETRAHEDRON
                = new Primitive.ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 0.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.EqualTriangle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

            [Reflection.NameAttribute("cylinder")] public static readonly Primitive.ConstructionData CYLINDER = new Primitive
                .ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 1.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Circle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("hemicylinder")] public static readonly Primitive.ConstructionData
                HEMICYLINDER = new Primitive.ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.25f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 0.75f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

            [Reflection.NameAttribute("cone")] public static readonly Primitive.ConstructionData CONE = new Primitive.
                ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 0.0f,
                PathScaleY = 0.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Circle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("hemicone")] public static readonly Primitive.ConstructionData HEMICONE = new Primitive
                .ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0f,
                PathCurve = PathCurve.Line,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 0.0f,
                PathScaleY = 0.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.25f,
                ProfileCurve = ProfileCurve.Circle,
                ProfileEnd = 0.75f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("sphere")] public static readonly Primitive.ConstructionData SPHERE = new Primitive
                .ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0.0f,
                PathCurve = PathCurve.Circle,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 1.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.HalfCircle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("hemisphere")] public static readonly Primitive.ConstructionData HEMISPHERE = new Primitive
                .ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0.0f,
                PathCurve = PathCurve.Circle,
                PathEnd = 0.5f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 1.0f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.HalfCircle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("torus")] public static readonly Primitive.ConstructionData TORUS = new Primitive.
                ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0.0f,
                PathCurve = PathCurve.Circle,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 0.25f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.Circle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };

            [Reflection.NameAttribute("ring")] public static readonly Primitive.ConstructionData RING = new Primitive.
                ConstructionData
            {
                AttachmentPoint = AttachmentPoint.Default,
                Material = Material.Wood,
                PathBegin = 0.0f,
                PathCurve = PathCurve.Circle,
                PathEnd = 1.0f,
                PathRadiusOffset = 0.0f,
                PathRevolutions = 1.0f,
                PathScaleX = 1.0f,
                PathScaleY = 0.25f,
                PathShearX = 0.0f,
                PathShearY = 0.0f,
                PathSkew = 0.0f,
                PathTaperX = 0.0f,
                PathTaperY = 0.0f,
                PathTwistBegin = 0.0f,
                PCode = PCode.Prim,
                ProfileBegin = 0.0f,
                ProfileCurve = ProfileCurve.EqualTriangle,
                ProfileEnd = 1.0f,
                ProfileHole = HoleType.Same,
                ProfileHollow = 0.0f,
                State = 0
            };
        }
    }
}