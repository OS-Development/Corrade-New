///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using Timer = System.Timers.Timer;

namespace Configurator
{
    public partial class CorradeConfiguratorForm : Form
    {
        /// <summary>
        ///     Possible input and output filters.
        /// </summary>
        public enum Filter : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
            [XmlEnum(Name = "RFC1738")] [Description("RFC1738")] RFC1738,
            [XmlEnum(Name = "RFC3986")] [Description("RFC3986")] RFC3986,
            [XmlEnum(Name = "ENIGMA")] [Description("ENIGMA")] ENIGMA,
            [XmlEnum(Name = "VIGENERE")] [Description("VIGENERE")] VIGENERE,
            [XmlEnum(Name = "ATBASH")] [Description("ATBASH")] ATBASH,
            [XmlEnum(Name = "BASE64")] [Description("BASE64")] BASE64,
            [XmlEnum(Name = "AES")] [Description("AES")] AES
        }

        /// <summary>
        ///     An enumeration of various compression methods
        ///     supproted by Corrade's internal HTTP server.
        /// </summary>
        public enum HTTPCompressionMethod : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE,
            [XmlEnum(Name = "deflate")] [Description("deflate")] DEFLATE,
            [XmlEnum(Name = "gzip")] [Description("gzip")] GZIP
        }

        /// <summary>
        ///     Corrade notification types.
        /// </summary>
        [Flags]
        public enum Notifications : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
            [XmlEnum(Name = "alert")] [Description("alert")] AlertMessage = 1,
            [XmlEnum(Name = "region")] [Description("region")] RegionMessage = 2,
            [XmlEnum(Name = "group")] [Description("group")] GroupMessage = 4,
            [XmlEnum(Name = "balance")] [Description("balance")] Balance = 8,
            [XmlEnum(Name = "message")] [Description("message")] InstantMessage = 16,
            [XmlEnum(Name = "notice")] [Description("notice")] GroupNotice = 32,
            [XmlEnum(Name = "local")] [Description("local")] LocalChat = 64,
            [XmlEnum(Name = "dialog")] [Description("dialog")] ScriptDialog = 128,
            [XmlEnum(Name = "friendship")] [Description("friendship")] Friendship = 256,
            [XmlEnum(Name = "inventory")] [Description("inventory")] Inventory = 512,
            [XmlEnum(Name = "permission")] [Description("permission")] ScriptPermission = 1024,
            [XmlEnum(Name = "lure")] [Description("lure")] TeleportLure = 2048,
            [XmlEnum(Name = "effect")] [Description("effect")] ViewerEffect = 4096,
            [XmlEnum(Name = "collision")] [Description("collision")] MeanCollision = 8192,
            [XmlEnum(Name = "crossing")] [Description("crossing")] RegionCrossed = 16384,
            [XmlEnum(Name = "terse")] [Description("terse")] TerseUpdates = 32768,
            [XmlEnum(Name = "typing")] [Description("typing")] Typing = 65536,
            [XmlEnum(Name = "invite")] [Description("invite")] GroupInvite = 131072,
            [XmlEnum(Name = "economy")] [Description("economy")] Economy = 262144,
            [XmlEnum(Name = "membership")] [Description("membership")] GroupMembership = 524288,
            [XmlEnum(Name = "url")] [Description("url")] LoadURL = 1048576,
            [XmlEnum(Name = "ownersay")] [Description("ownersay")] OwnerSay = 2097152,
            [XmlEnum(Name = "regionsayto")] [Description("regionsayto")] RegionSayTo = 4194304,
            [XmlEnum(Name = "objectim")] [Description("objectim")] ObjectInstantMessage = 8388608,
            [XmlEnum(Name = "rlv")] [Description("rlv")] RLVMessage = 16777216,
            [XmlEnum(Name = "debug")] [Description("debug")] DebugMessage = 33554432,
            [XmlEnum(Name = "avatars")] [Description("avatars")] RadarAvatars = 67108864,
            [XmlEnum(Name = "primitives")] [Description("primitives")] RadarPrimitives = 134217728,
            [XmlEnum(Name = "control")] [Description("control")] ScriptControl = 268435456
        }

        /// <summary>
        ///     Corrade permissions.
        /// </summary>
        [Flags]
        public enum Permissions : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] None = 0,
            [XmlEnum(Name = "movement")] [Description("movement")] Movement = 1,
            [XmlEnum(Name = "economy")] [Description("economy")] Economy = 2,
            [XmlEnum(Name = "land")] [Description("land")] Land = 4,
            [XmlEnum(Name = "grooming")] [Description("grooming")] Grooming = 8,
            [XmlEnum(Name = "inventory")] [Description("inventory")] Inventory = 16,
            [XmlEnum(Name = "interact")] [Description("interact")] Interact = 32,
            [XmlEnum(Name = "mute")] [Description("mute")] Mute = 64,
            [XmlEnum(Name = "database")] [Description("database")] Database = 128,
            [XmlEnum(Name = "notifications")] [Description("notifications")] Notifications = 256,
            [XmlEnum(Name = "talk")] [Description("talk")] Talk = 512,
            [XmlEnum(Name = "directory")] [Description("directory")] Directory = 1024,
            [XmlEnum(Name = "system")] [Description("system")] System = 2048,
            [XmlEnum(Name = "friendship")] [Description("friendship")] Friendship = 4096,
            [XmlEnum(Name = "execute")] [Description("execute")] Execute = 8192,
            [XmlEnum(Name = "group")] [Description("group")] Group = 16384,
            [XmlEnum(Name = "filter")] [Description("filter")] Filter = 32768,
            [XmlEnum(Name = "schedule")] [Description("schedule")] Schedule = 65536
        }

        private static readonly object ClientInstanceConfigurationLock = new object();
        private static readonly object ConfigurationFileLock = new object();
        private static CorradeConfiguration corradeConfiguration = new CorradeConfiguration();
        private static CorradeConfiguratorForm mainForm;

        private readonly Action GetUserConfiguration = () =>
        {
            // client
            mainForm.Firstname.Text = corradeConfiguration.FirstName;
            mainForm.Lastname.Text = corradeConfiguration.LastName;
            mainForm.Password.Text = corradeConfiguration.Password;
            mainForm.LoginURL.Text = corradeConfiguration.LoginURL;
            mainForm.StartLocation.Text = corradeConfiguration.StartLocation;
            mainForm.TOS.Checked = corradeConfiguration.TOSAccepted;
            mainForm.AutoActivateGroup.Checked = corradeConfiguration.AutoActivateGroup;
            mainForm.GroupCreateFee.Text = corradeConfiguration.GroupCreateFee.ToString();
            mainForm.ExpectedExitCode.Value = corradeConfiguration.ExitCodeExpected < -100 ||
                                              corradeConfiguration.ExitCodeExpected > 100
                ? -1
                : corradeConfiguration.ExitCodeExpected;
            mainForm.AbnomalExitCode.Value = corradeConfiguration.ExitCodeAbnormal < -100 ||
                                             corradeConfiguration.ExitCodeAbnormal > 100
                ? -2
                : corradeConfiguration.ExitCodeAbnormal;
            mainForm.ClientIdentificationTag.Text = corradeConfiguration.ClientIdentificationTag.ToString();

            // logs
            mainForm.ClientLogFile.Text = corradeConfiguration.ClientLogFile;
            mainForm.ClientLogFileEnabled.Checked = corradeConfiguration.ClientLogEnabled;
            mainForm.InstantMessageLogFile.Text = corradeConfiguration.InstantMessageLogDirectory;
            mainForm.InstantMessageLogFileEnabled.Checked = corradeConfiguration.InstantMessageLogEnabled;
            mainForm.LocalLogFile.Text = corradeConfiguration.LocalMessageLogDirectory;
            mainForm.LocalLogFileEnabled.Checked = corradeConfiguration.LocalMessageLogEnabled;
            mainForm.RegionLogFile.Text = corradeConfiguration.RegionMessageLogDirectory;
            mainForm.RegionLogFileEnabled.Checked = corradeConfiguration.RegionMessageLogEnabled;

            // filters
            mainForm.ActiveInputFilters.Items.Clear();
            foreach (Filter filter in corradeConfiguration.InputFilters)
            {
                mainForm.ActiveInputFilters.Items.Add(new ListViewItem
                {
                    Text = wasGetDescriptionFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.Items.Clear();
            mainForm.ActiveInputFilters.DisplayMember = "Text";
            foreach (Filter filter in corradeConfiguration.OutputFilters)
            {
                mainForm.ActiveOutputFilters.Items.Add(new ListViewItem
                {
                    Text = wasGetDescriptionFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.DisplayMember = "Text";

            // cryptography
            mainForm.ENIGMARotorSequence.Items.Clear();
            foreach (char rotor in corradeConfiguration.ENIGMA.rotors)
            {
                mainForm.ENIGMARotorSequence.Items.Add(new ListViewItem
                {
                    Text = rotor.ToString(),
                    Tag = rotor
                });
            }
            mainForm.ENIGMARotorSequence.DisplayMember = "Text";
            mainForm.ENIGMAPlugSequence.Items.Clear();
            foreach (char plug in corradeConfiguration.ENIGMA.plugs)
            {
                mainForm.ENIGMAPlugSequence.Items.Add(new ListViewItem
                {
                    Text = plug.ToString(),
                    Tag = plug
                });
            }
            mainForm.ENIGMAPlugSequence.DisplayMember = "Text";
            mainForm.ENIGMAReflector.Text = corradeConfiguration.ENIGMA.reflector.ToString();
            mainForm.VIGENERESecret.Text = corradeConfiguration.VIGENERESecret;
            if (corradeConfiguration.AESKey != null && !corradeConfiguration.AESKey.Length.Equals(0))
            {
                mainForm.AESKey.Text = Encoding.UTF8.GetString(corradeConfiguration.AESKey);
            }
            if (corradeConfiguration.AESIV != null && !corradeConfiguration.AESIV.Length.Equals(0))
            {
                mainForm.AESIV.Text = Encoding.UTF8.GetString(corradeConfiguration.AESIV);
            }

            // AIML
            mainForm.AIMLEnabled.Checked = corradeConfiguration.EnableAIML;
            // RLV
            mainForm.RLVEnabled.Checked = corradeConfiguration.EnableRLV;

            // network
            mainForm.NetworkBindAddress.Text = corradeConfiguration.BindIPAddress;
            mainForm.NetworkMACAddress.Text = corradeConfiguration.NetworkCardMAC;
            mainForm.NetworkID0.Text = corradeConfiguration.DriveIdentifierHash;
            mainForm.NetworkNaggleEnabled.Checked = corradeConfiguration.UseNaggle;
            mainForm.NetworkExpect100ContinueEnabled.Checked = corradeConfiguration.UseExpect100Continue;

            // throttles
            mainForm.ThrottlesTotalThrottle.Text = corradeConfiguration.ThrottleTotal.ToString();
            mainForm.ThrottlesResendThrottle.Text = corradeConfiguration.ThrottleResend.ToString();
            mainForm.ThrottleLandThrottle.Text = corradeConfiguration.ThrottleLand.ToString();
            mainForm.ThrottleTaskThrottle.Text = corradeConfiguration.ThrottleTask.ToString();
            mainForm.ThrottleTextureThrottle.Text = corradeConfiguration.ThrottleTexture.ToString();
            mainForm.ThrottleWindThrottle.Text = corradeConfiguration.ThrottleWind.ToString();
            mainForm.ThrottleAssetThrottle.Text = corradeConfiguration.ThrottleAsset.ToString();
            mainForm.ThrottleCloudThrottle.Text = corradeConfiguration.ThrottleCloud.ToString();

            // server
            mainForm.HTTPServerEnabled.Checked = corradeConfiguration.EnableHTTPServer;
            mainForm.HTTPServerPrefix.Text = corradeConfiguration.HTTPServerPrefix;
            mainForm.HTTPServerCompression.Text =
                wasGetDescriptionFromEnumValue(corradeConfiguration.HTTPServerCompression);
            mainForm.HTTPServerKeepAliveEnabled.Checked = corradeConfiguration.HTTPServerKeepAlive;

            // TCP
            mainForm.TCPNotificationsServerEnabled.Checked = corradeConfiguration.EnableTCPNotificationsServer;
            mainForm.TCPNotificationsServerAddress.Text = corradeConfiguration.TCPNotificationsServerAddress;
            mainForm.TCPNotificationsServerPort.Text = corradeConfiguration.TCPNotificationsServerPort.ToString();

            // limits
            mainForm.LimitsRange.Text = corradeConfiguration.Range.ToString(CultureInfo.DefaultThreadCurrentCulture);
            mainForm.LimitsPOSTThreads.Text = corradeConfiguration.MaximumPOSTThreads.ToString();
            mainForm.LimitsSchedulerExpiration.Text = corradeConfiguration.SchedulerExpiration.ToString();
            mainForm.LimitsLoggingThreads.Text = corradeConfiguration.MaximumLogThreads.ToString();
            mainForm.LimitsCommandsThreads.Text = corradeConfiguration.MaximumCommandThreads.ToString();
            mainForm.LimitsRLVThreads.Text = corradeConfiguration.MaximumRLVThreads.ToString();
            mainForm.LimitsInstantMessageThreads.Text = corradeConfiguration.MaximumInstantMessageThreads.ToString();
            mainForm.LimitsSchedulesResolution.Text = corradeConfiguration.SchedulesResolution.ToString();
            mainForm.LimitsClientConnections.Text = corradeConfiguration.ConnectionLimit.ToString();
            mainForm.LimitsClientIdle.Text = corradeConfiguration.ConnectionIdleTime.ToString();
            mainForm.LimitsCallbacksTimeout.Text = corradeConfiguration.CallbackTimeout.ToString();
            mainForm.LimitsCallbacksThrottle.Text = corradeConfiguration.CallbackThrottle.ToString();
            mainForm.LimitsCallbackQueue.Text = corradeConfiguration.CallbackQueueLength.ToString();
            mainForm.LimitsNotificationsTimeout.Text = corradeConfiguration.NotificationTimeout.ToString();
            mainForm.LimitsNotificationsThrottle.Text = corradeConfiguration.NotificationThrottle.ToString();
            mainForm.LimitsNotificationsQueue.Text = corradeConfiguration.NotificationQueueLength.ToString();
            mainForm.LimitsNotificationsThreads.Text = corradeConfiguration.MaximumNotificationThreads.ToString();
            mainForm.LimitsTCPNotificationsThrottle.Text = corradeConfiguration.TCPNotificationThrottle.ToString();
            mainForm.LimitsTCPNotificationsQueue.Text = corradeConfiguration.TCPNotificationQueueLength.ToString();
            mainForm.LimitsHTTPServerDrain.Text = corradeConfiguration.HTTPServerDrainTimeout.ToString();
            mainForm.LimitsHTTPServerBody.Text = corradeConfiguration.HTTPServerBodyTimeout.ToString();
            mainForm.LimitsHTTPServerHeader.Text = corradeConfiguration.HTTPServerHeaderTimeout.ToString();
            mainForm.LimitsHTTPServerIdle.Text = corradeConfiguration.HTTPServerIdleTimeout.ToString();
            mainForm.LimitsHTTPServerQueue.Text = corradeConfiguration.HTTPServerQueueTimeout.ToString();
            mainForm.LimitsHTTPServerTimeout.Text = corradeConfiguration.HTTPServerTimeout.ToString();
            mainForm.LimitsServicesTimeout.Text = corradeConfiguration.ServicesTimeout.ToString();
            mainForm.LimitsServicesRebake.Text = corradeConfiguration.RebakeDelay.ToString();
            mainForm.LimitsServicesActivate.Text = corradeConfiguration.ActivateDelay.ToString();
            mainForm.LimitsDataTimeout.Text = corradeConfiguration.DataTimeout.ToString();
            mainForm.LimitsDataDecay.Text = wasGetDescriptionFromEnumValue(corradeConfiguration.DataDecayType);
            mainForm.LimitsMembershipSweep.Text = corradeConfiguration.MembershipSweepInterval.ToString();
            mainForm.LimitsLogoutTimeout.Text = corradeConfiguration.LogoutGrace.ToString();

            // masters
            mainForm.Masters.Items.Clear();
            foreach (Master master in corradeConfiguration.Masters)
            {
                mainForm.Masters.Items.Add(new ListViewItem
                {
                    Text = master.FirstName + @" " + master.LastName,
                    Tag = master
                });
            }
            mainForm.Masters.DisplayMember = "Text";

            // groups
            mainForm.Groups.Items.Clear();
            foreach (Group group in corradeConfiguration.Groups)
            {
                mainForm.Groups.Items.Add(new ListViewItem
                {
                    Text = UnescapeXML(group.Name),
                    Tag = group
                });
            }
            mainForm.Groups.DisplayMember = "Text";
        };

        private readonly Action SetUserConfiguration = () =>
        {
            // client
            corradeConfiguration.FirstName = mainForm.Firstname.Text;
            corradeConfiguration.LastName = mainForm.Lastname.Text;
            // Check if the password is an MD5 - if it is, then set it, otherwise make an MD5
            switch (mainForm.Password.Text.Contains("$1$"))
            {
                case true:
                    corradeConfiguration.Password = mainForm.Password.Text;
                    break;
                default:
                    corradeConfiguration.Password = "$1$" + CalculateMD5Hash(mainForm.Password.Text);
                    break;
            }
            corradeConfiguration.LoginURL = mainForm.LoginURL.Text;
            corradeConfiguration.StartLocation = mainForm.StartLocation.Text;
            corradeConfiguration.TOSAccepted = mainForm.TOS.Checked;
            UUID outUUID;
            if (UUID.TryParse(mainForm.ClientIdentificationTag.Text, out outUUID))
            {
                corradeConfiguration.ClientIdentificationTag = outUUID;
            }
            corradeConfiguration.AutoActivateGroup = mainForm.AutoActivateGroup.Checked;
            uint outUint;
            if (uint.TryParse(mainForm.GroupCreateFee.Text, out outUint))
            {
                corradeConfiguration.GroupCreateFee = outUint;
            }
            corradeConfiguration.ExitCodeExpected = (int) mainForm.ExpectedExitCode.Value;
            corradeConfiguration.ExitCodeAbnormal = (int) mainForm.AbnomalExitCode.Value;

            // logs
            corradeConfiguration.ClientLogFile = mainForm.ClientLogFile.Text;
            corradeConfiguration.ClientLogEnabled = mainForm.ClientLogFileEnabled.Checked;
            corradeConfiguration.InstantMessageLogDirectory = mainForm.InstantMessageLogFile.Text;
            corradeConfiguration.InstantMessageLogEnabled = mainForm.InstantMessageLogFileEnabled.Checked;
            corradeConfiguration.LocalMessageLogDirectory = mainForm.LocalLogFile.Text;
            corradeConfiguration.LocalMessageLogEnabled = mainForm.LocalLogFileEnabled.Checked;
            corradeConfiguration.RegionMessageLogDirectory = mainForm.RegionLogFile.Text;
            corradeConfiguration.RegionMessageLogEnabled = mainForm.RegionLogFileEnabled.Checked;

            // filters
            corradeConfiguration.InputFilters =
                mainForm.ActiveInputFilters.Items.Cast<ListViewItem>().Select(o => (Filter) o.Tag).ToList();
            corradeConfiguration.OutputFilters =
                mainForm.ActiveOutputFilters.Items.Cast<ListViewItem>().Select(o => (Filter) o.Tag).ToList();

            // cryptography
            corradeConfiguration.ENIGMA = new ENIGMA
            {
                rotors = mainForm.ENIGMARotorSequence.Items.Cast<ListViewItem>().Select(o => (char) o.Tag).ToArray(),
                plugs = mainForm.ENIGMAPlugSequence.Items.Cast<ListViewItem>().Select(o => (char) o.Tag).ToArray(),
                reflector = mainForm.ENIGMAReflector.Text[0]
            };

            corradeConfiguration.VIGENERESecret = mainForm.VIGENERESecret.Text;

            byte[] AESKeyBytes = Encoding.UTF8.GetBytes(mainForm.AESKey.Text);
            // Only accept FIPS-197 key-lengths
            switch (AESKeyBytes.Length)
            {
                case 16:
                case 24:
                case 32:
                    corradeConfiguration.AESKey = AESKeyBytes;
                    break;
            }
            byte[] AESIVBytes = Encoding.UTF8.GetBytes(mainForm.AESIV.Text);
            switch (AESIVBytes.Length)
            {
                case 16:
                    corradeConfiguration.AESIV = AESIVBytes;
                    break;
            }

            // AIML
            corradeConfiguration.EnableAIML = mainForm.AIMLEnabled.Checked;
            // RLV
            corradeConfiguration.EnableRLV = mainForm.RLVEnabled.Checked;

            // network
            corradeConfiguration.BindIPAddress = mainForm.NetworkBindAddress.Text;
            corradeConfiguration.NetworkCardMAC = mainForm.NetworkMACAddress.Text;
            corradeConfiguration.DriveIdentifierHash = mainForm.NetworkID0.Text;
            corradeConfiguration.UseNaggle = mainForm.NetworkNaggleEnabled.Checked;
            corradeConfiguration.UseExpect100Continue = mainForm.NetworkExpect100ContinueEnabled.Checked;

            // throttles
            if (uint.TryParse(mainForm.ThrottlesTotalThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleTotal = outUint;
            }
            if (uint.TryParse(mainForm.ThrottlesResendThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleResend = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleLandThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleLand = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleTaskThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleTask = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleTextureThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleTexture = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleWindThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleWind = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleAssetThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleAsset = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleCloudThrottle.Text, out outUint))
            {
                corradeConfiguration.ThrottleCloud = outUint;
            }

            // server
            corradeConfiguration.EnableHTTPServer = mainForm.HTTPServerEnabled.Checked;
            corradeConfiguration.HTTPServerPrefix = mainForm.HTTPServerPrefix.Text;
            corradeConfiguration.HTTPServerCompression =
                wasGetEnumValueFromDescription<HTTPCompressionMethod>(mainForm.HTTPServerCompression.Text);
            corradeConfiguration.HTTPServerKeepAlive = mainForm.HTTPServerKeepAliveEnabled.Checked;

            // TCP
            corradeConfiguration.EnableTCPNotificationsServer = mainForm.TCPNotificationsServerEnabled.Checked;
            corradeConfiguration.TCPNotificationsServerAddress = mainForm.TCPNotificationsServerAddress.Text;
            if (uint.TryParse(mainForm.TCPNotificationsServerPort.Text, out outUint))
            {
                corradeConfiguration.TCPNotificationsServerPort = outUint;
            }

            // limits
            if (uint.TryParse(mainForm.LimitsRange.Text, out outUint))
            {
                corradeConfiguration.Range = outUint;
            }
            if (uint.TryParse(mainForm.LimitsPOSTThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumPOSTThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsSchedulerExpiration.Text, out outUint))
            {
                corradeConfiguration.SchedulerExpiration = outUint;
            }
            if (uint.TryParse(mainForm.LimitsLoggingThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumLogThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCommandsThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumCommandThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsRLVThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumRLVThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsInstantMessageThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumInstantMessageThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsSchedulesResolution.Text, out outUint))
            {
                corradeConfiguration.SchedulesResolution = outUint;
            }
            if (uint.TryParse(mainForm.LimitsClientConnections.Text, out outUint))
            {
                corradeConfiguration.ConnectionLimit = outUint;
            }
            if (uint.TryParse(mainForm.LimitsClientIdle.Text, out outUint))
            {
                corradeConfiguration.ConnectionIdleTime = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbacksTimeout.Text, out outUint))
            {
                corradeConfiguration.CallbackTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbacksThrottle.Text, out outUint))
            {
                corradeConfiguration.CallbackThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbackQueue.Text, out outUint))
            {
                corradeConfiguration.CallbackQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsTimeout.Text, out outUint))
            {
                corradeConfiguration.NotificationTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsThrottle.Text, out outUint))
            {
                corradeConfiguration.NotificationThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsQueue.Text, out outUint))
            {
                corradeConfiguration.NotificationQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsThreads.Text, out outUint))
            {
                corradeConfiguration.MaximumNotificationThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsTCPNotificationsQueue.Text, out outUint))
            {
                corradeConfiguration.TCPNotificationQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsTCPNotificationsThrottle.Text, out outUint))
            {
                corradeConfiguration.TCPNotificationThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerDrain.Text, out outUint))
            {
                corradeConfiguration.HTTPServerDrainTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerBody.Text, out outUint))
            {
                corradeConfiguration.HTTPServerBodyTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerHeader.Text, out outUint))
            {
                corradeConfiguration.HTTPServerHeaderTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerIdle.Text, out outUint))
            {
                corradeConfiguration.HTTPServerIdleTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerQueue.Text, out outUint))
            {
                corradeConfiguration.HTTPServerQueueTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHTTPServerTimeout.Text, out outUint))
            {
                corradeConfiguration.HTTPServerTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsServicesTimeout.Text, out outUint))
            {
                corradeConfiguration.ServicesTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsServicesRebake.Text, out outUint))
            {
                corradeConfiguration.RebakeDelay = outUint;
            }
            if (uint.TryParse(mainForm.LimitsServicesActivate.Text, out outUint))
            {
                corradeConfiguration.ActivateDelay = outUint;
            }
            if (uint.TryParse(mainForm.LimitsDataTimeout.Text, out outUint))
            {
                corradeConfiguration.DataTimeout = outUint;
            }
            corradeConfiguration.DataDecayType =
                wasGetEnumValueFromDescription<wasAdaptiveAlarm.DECAY_TYPE>(mainForm.LimitsDataDecay.Text);
            if (uint.TryParse(mainForm.LimitsMembershipSweep.Text, out outUint))
            {
                corradeConfiguration.MembershipSweepInterval = outUint;
            }
            if (uint.TryParse(mainForm.LimitsLogoutTimeout.Text, out outUint))
            {
                corradeConfiguration.LogoutGrace = outUint;
            }
        };

        public CorradeConfiguratorForm()
        {
            InitializeComponent();
            mainForm = this;
        }

        private static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private void LoadCorradeLegacyConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                switch (mainForm.LoadLegacyConfigurationDialog.ShowDialog())
                {
                    case DialogResult.OK:
                        string file = mainForm.LoadLegacyConfigurationDialog.FileName;
                        new Thread(() =>
                        {
                            mainForm.BeginInvoke((MethodInvoker) (() =>
                            {
                                try
                                {
                                    mainForm.StatusText.Text = @"loading legacy configuration...";
                                    mainForm.StatusProgress.Value = 0;
                                    corradeConfiguration = new CorradeConfiguration();
                                    corradeConfiguration.LoadLegacy(file);
                                    mainForm.StatusText.Text = @"applying settings...";
                                    mainForm.StatusProgress.Value = 50;
                                    GetUserConfiguration.Invoke();
                                    mainForm.StatusText.Text = @"configuration loaded";
                                    mainForm.StatusProgress.Value = 100;
                                }
                                catch (Exception ex)
                                {
                                    mainForm.StatusText.Text = ex.Message;
                                }
                            }));
                        })
                        {IsBackground = true, Priority = ThreadPriority.Normal}.Start();
                        break;
                }
            }));
        }

        private void LoadCorradeConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                switch (mainForm.LoadConfigurationDialog.ShowDialog())
                {
                    case DialogResult.OK:
                        string file = mainForm.LoadConfigurationDialog.FileName;
                        new Thread(() =>
                        {
                            mainForm.BeginInvoke((MethodInvoker) (() =>
                            {
                                try
                                {
                                    mainForm.StatusText.Text = @"loading configuration...";
                                    mainForm.StatusProgress.Value = 0;
                                    CorradeConfiguration.Load(file, ref corradeConfiguration);
                                    mainForm.StatusProgress.Value = 50;
                                    mainForm.StatusText.Text = @"applying settings...";
                                    GetUserConfiguration.Invoke();
                                    mainForm.StatusText.Text = @"configuration loaded";
                                    mainForm.StatusProgress.Value = 100;
                                }
                                catch (Exception ex)
                                {
                                    mainForm.StatusText.Text = ex.Message;
                                }
                            }));
                        })
                        {IsBackground = true, Priority = ThreadPriority.Normal}.Start();
                        break;
                }
            }));
        }

        private void SaveCorradeConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                switch (mainForm.SaveConfigurationDialog.ShowDialog())
                {
                    case DialogResult.OK:
                        string file = mainForm.SaveConfigurationDialog.FileName;
                        new Thread(() =>
                        {
                            mainForm.BeginInvoke((MethodInvoker) (() =>
                            {
                                try
                                {
                                    mainForm.StatusText.Text = @"applying settings...";
                                    mainForm.StatusProgress.Value = 0;
                                    SetUserConfiguration.Invoke();
                                    mainForm.StatusText.Text = @"saving configuration...";
                                    mainForm.StatusProgress.Value = 50;
                                    CorradeConfiguration.Save(file, ref corradeConfiguration);
                                    mainForm.StatusText.Text = @"configuration saved";
                                    mainForm.StatusProgress.Value = 100;
                                }
                                catch (Exception ex)
                                {
                                    mainForm.StatusText.Text = ex.Message;
                                }
                            }));
                        })
                        {IsBackground = true, Priority = ThreadPriority.Normal}.Start();
                        break;
                }
            }));
        }

        private void MasterSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Masters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;
                Master master = (Master) listViewItem.Tag;
                MasterFirstName.Text = master.FirstName;
                MasterLastName.Text = master.LastName;
            }));
        }

        private void GroupSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;

                Group group = (Group) listViewItem.Tag;
                GroupName.Text = group.Name;
                GroupPassword.Text = group.Password;
                GroupUUID.Text = group.UUID.ToString();
                GroupWorkers.Text = group.Workers.ToString();
                GroupSchedules.Text = group.Schedules.ToString();
                GroupDatabaseFile.Text = group.DatabaseFile;
                GroupChatLogEnabled.Checked = group.ChatLogEnabled;
                GroupChatLogFile.Text = group.ChatLog;

                // Permissions
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    switch (
                        !(group.PermissionMask &
                          (uint) wasGetEnumValueFromDescription<Permissions>((string) GroupPermissions.Items[i])).Equals
                            (0))
                    {
                        case true:
                            GroupPermissions.SetItemChecked(i, true);
                            break;
                        default:
                            GroupPermissions.SetItemChecked(i, false);
                            break;
                    }
                }

                // Notifications
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    switch (
                        !(group.NotificationMask &
                          (uint) wasGetEnumValueFromDescription<Notifications>((string) GroupNotifications.Items[i]))
                            .Equals(0))
                    {
                        case true:
                            GroupNotifications.SetItemChecked(i, true);
                            break;
                        default:
                            GroupNotifications.SetItemChecked(i, false);
                            break;
                    }
                }
            }));
        }

        private void PermissionsSelected(object sender, ItemCheckEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;
                Group group = (Group) listViewItem.Tag;
                corradeConfiguration.Groups.Remove(group);

                Permissions permission =
                    wasGetEnumValueFromDescription<Permissions>((string) GroupPermissions.Items[e.Index]);

                switch (e.NewValue)
                {
                    case CheckState.Checked: // add permission
                        if (!group.Permissions.Contains(permission))
                            group.Permissions.Add(permission);
                        break;
                    case CheckState.Unchecked: // remove permission
                        if (group.Permissions.Contains(permission))
                            group.Permissions.Remove(permission);
                        break;
                }

                corradeConfiguration.Groups.Add(group);
                Groups.Items[Groups.SelectedIndex] = new ListViewItem {Text = group.Name, Tag = group};
            }));
        }

        private void SelectedNotifications(object sender, ItemCheckEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;
                Group group = (Group) listViewItem.Tag;
                corradeConfiguration.Groups.Remove(group);

                Notifications notification =
                    wasGetEnumValueFromDescription<Notifications>((string) GroupNotifications.Items[e.Index]);

                switch (e.NewValue)
                {
                    case CheckState.Checked: // add permission
                        if (!group.Notifications.Contains(notification))
                            group.Notifications.Add(notification);
                        break;
                    case CheckState.Unchecked: // remove permission
                        if (group.Notifications.Contains(notification))
                            group.Notifications.Remove(notification);
                        break;
                }

                corradeConfiguration.Groups.Add(group);
                Groups.Items[Groups.SelectedIndex] = new ListViewItem {Text = group.Name, Tag = group};
            }));
        }

        private void DeleteGroupRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;
                Group group = (Group) listViewItem.Tag;
                corradeConfiguration.Groups.Remove(group);
                Groups.Items.RemoveAt(Groups.SelectedIndex);

                // Void all the selected items
                GroupName.Text = string.Empty;
                GroupPassword.Text = string.Empty;
                GroupUUID.Text = string.Empty;
                GroupWorkers.Text = string.Empty;
                GroupSchedules.Text = string.Empty;
                GroupDatabaseFile.Text = string.Empty;
                GroupChatLogEnabled.Checked = false;
                GroupChatLogFile.Text = string.Empty;

                // Permissions
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    GroupPermissions.SetItemChecked(i, false);
                }

                // Notifications
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    GroupNotifications.SetItemChecked(i, false);
                }
            }));
        }

        private void MasterConfigurationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Masters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;

                Master master = (Master) listViewItem.Tag;

                if (string.IsNullOrEmpty(MasterFirstName.Text) || string.IsNullOrEmpty(MasterLastName.Text))
                {
                    MasterFirstName.BackColor = Color.MistyRose;
                    MasterLastName.BackColor = Color.MistyRose;
                    return;
                }

                MasterFirstName.BackColor = Color.Empty;
                MasterLastName.BackColor = Color.Empty;
                corradeConfiguration.Masters.Remove(master);
                master = new Master {FirstName = MasterFirstName.Text, LastName = MasterLastName.Text};
                corradeConfiguration.Masters.Add(master);
                Masters.Items[Masters.SelectedIndex] = new ListViewItem
                {
                    Text = MasterFirstName.Text + @" " + MasterLastName.Text,
                    Tag = master
                };
            }));
        }

        private static string EscapeXML(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            return !SecurityElement.IsValidText(s)
                ? SecurityElement.Escape(s)
                : s;
        }

        private static string UnescapeXML(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            string returnString = s;
            returnString = returnString.Replace("&apos;", "'");
            returnString = returnString.Replace("&quot;", "\"");
            returnString = returnString.Replace("&gt;", ">");
            returnString = returnString.Replace("&lt;", "<");
            returnString = returnString.Replace("&amp;", "&");

            return returnString;
        }

        private void GroupConfigurationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;

                Group group = (Group) listViewItem.Tag;

                if (GroupName.Text.Equals(string.Empty))
                {
                    GroupName.BackColor = Color.MistyRose;
                    return;
                }
                GroupName.BackColor = Color.Empty;

                if (GroupPassword.Text.Equals(string.Empty))
                {
                    GroupPassword.BackColor = Color.MistyRose;
                    return;
                }
                GroupPassword.BackColor = Color.Empty;

                UUID groupUUID;
                if (GroupUUID.Text.Equals(string.Empty) || !UUID.TryParse(GroupUUID.Text, out groupUUID))
                {
                    GroupUUID.BackColor = Color.MistyRose;
                    return;
                }
                GroupUUID.BackColor = Color.Empty;

                uint groupWorkers;
                if (GroupWorkers.Text.Equals(string.Empty) || !uint.TryParse(GroupWorkers.Text, out groupWorkers))
                {
                    GroupWorkers.BackColor = Color.MistyRose;
                    return;
                }
                GroupWorkers.BackColor = Color.Empty;

                uint groupSchedules;
                if (GroupSchedules.Text.Equals(string.Empty) ||
                    !uint.TryParse(GroupSchedules.Text, out groupSchedules))
                {
                    GroupSchedules.BackColor = Color.MistyRose;
                    return;
                }
                GroupSchedules.BackColor = Color.Empty;

                if (GroupDatabaseFile.Text.Equals(string.Empty))
                {
                    GroupDatabaseFile.BackColor = Color.MistyRose;
                    return;
                }
                GroupDatabaseFile.BackColor = Color.Empty;

                if (GroupChatLogFile.Text.Equals(string.Empty))
                {
                    GroupChatLogFile.BackColor = Color.MistyRose;
                    return;
                }
                GroupChatLogFile.BackColor = Color.Empty;

                // Permissions
                HashSet<Permissions> permissions = new HashSet<Permissions>();
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    switch (GroupPermissions.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            permissions.Add(
                                wasGetEnumValueFromDescription<Permissions>((string) GroupPermissions.Items[i]));
                            break;
                    }
                }

                // Notifications
                HashSet<Notifications> notifications = new HashSet<Notifications>();
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    switch (GroupNotifications.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            notifications.Add(
                                wasGetEnumValueFromDescription<Notifications>((string) GroupNotifications.Items[i]));
                            break;
                    }
                }


                corradeConfiguration.Groups.Remove(group);

                group = new Group
                {
                    Name = EscapeXML(GroupName.Text),
                    UUID = groupUUID,
                    Password = EscapeXML(GroupPassword.Text),
                    Workers = groupWorkers,
                    Schedules = groupSchedules,
                    DatabaseFile = GroupDatabaseFile.Text,
                    ChatLog = GroupChatLogFile.Text,
                    ChatLogEnabled = GroupChatLogEnabled.Checked,
                    Permissions = permissions,
                    Notifications = notifications
                };

                corradeConfiguration.Groups.Add(group);
                Groups.Items[Groups.SelectedIndex] = new ListViewItem {Text = GroupName.Text, Tag = group};
            }));
        }

        private void AddGroupRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (GroupName.Text.Equals(string.Empty))
                {
                    GroupName.BackColor = Color.MistyRose;
                    return;
                }
                GroupName.BackColor = Color.Empty;

                if (GroupPassword.Text.Equals(string.Empty))
                {
                    GroupPassword.BackColor = Color.MistyRose;
                    return;
                }
                GroupPassword.BackColor = Color.Empty;

                UUID groupUUID;
                if (GroupUUID.Text.Equals(string.Empty) || !UUID.TryParse(GroupUUID.Text, out groupUUID))
                {
                    GroupUUID.BackColor = Color.MistyRose;
                    return;
                }
                GroupUUID.BackColor = Color.Empty;

                uint groupWorkers;
                if (GroupWorkers.Text.Equals(string.Empty) || !uint.TryParse(GroupWorkers.Text, out groupWorkers))
                {
                    GroupWorkers.BackColor = Color.MistyRose;
                    return;
                }
                GroupWorkers.BackColor = Color.Empty;

                uint groupSchedules;
                if (GroupSchedules.Text.Equals(string.Empty) || !uint.TryParse(GroupSchedules.Text, out groupSchedules))
                {
                    GroupSchedules.BackColor = Color.MistyRose;
                    return;
                }
                GroupSchedules.BackColor = Color.Empty;

                if (GroupDatabaseFile.Text.Equals(string.Empty))
                {
                    GroupDatabaseFile.BackColor = Color.MistyRose;
                    return;
                }
                GroupDatabaseFile.BackColor = Color.Empty;

                if (GroupChatLogFile.Text.Equals(string.Empty))
                {
                    GroupChatLogFile.BackColor = Color.MistyRose;
                    return;
                }
                GroupChatLogFile.BackColor = Color.Empty;

                // Permissions
                HashSet<Permissions> permissions = new HashSet<Permissions>();
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    switch (GroupPermissions.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            permissions.Add(
                                wasGetEnumValueFromDescription<Permissions>((string) GroupPermissions.Items[i]));
                            break;
                    }
                }

                // Notifications
                HashSet<Notifications> notifications = new HashSet<Notifications>();
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    switch (GroupNotifications.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            notifications.Add(
                                wasGetEnumValueFromDescription<Notifications>((string) GroupNotifications.Items[i]));
                            break;
                    }
                }

                Group group = new Group
                {
                    Name = EscapeXML(GroupName.Text),
                    UUID = groupUUID,
                    Password = EscapeXML(GroupPassword.Text),
                    Workers = groupWorkers,
                    Schedules = groupSchedules,
                    DatabaseFile = GroupDatabaseFile.Text,
                    ChatLog = GroupChatLogFile.Text,
                    ChatLogEnabled = GroupChatLogEnabled.Checked,
                    Permissions = permissions,
                    Notifications = notifications
                };

                corradeConfiguration.Groups.Add(group);
                Groups.Items.Add(new ListViewItem {Text = GroupName.Text, Tag = group});
            }));
        }

        private void AddInputDecoderRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(InputDecode.Text))
                {
                    InputDecode.BackColor = Color.MistyRose;
                    return;
                }
                InputDecode.BackColor = Color.Empty;
                ActiveInputFilters.Items.Add(new ListViewItem
                {
                    Text = InputDecode.Text,
                    Tag = wasGetEnumValueFromDescription<Filter>(InputDecode.Text)
                });
            }));
        }

        private void AddInputDecryptionRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(InputDecryption.Text))
                {
                    InputDecryption.BackColor = Color.MistyRose;
                    return;
                }
                InputDecryption.BackColor = Color.Empty;
                ActiveInputFilters.Items.Add(new ListViewItem
                {
                    Text = InputDecryption.Text,
                    Tag = wasGetEnumValueFromDescription<Filter>(InputDecryption.Text)
                });
            }));
        }

        private void AddOutputEncryptionRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(OutputEncrypt.Text))
                {
                    OutputEncrypt.BackColor = Color.MistyRose;
                    return;
                }
                OutputEncrypt.BackColor = Color.Empty;
                ActiveOutputFilters.Items.Add(new ListViewItem
                {
                    Text = OutputEncrypt.Text,
                    Tag = wasGetEnumValueFromDescription<Filter>(OutputEncrypt.Text)
                });
            }));
        }

        private void AddOutputEncoderRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(OutputEncode.Text))
                {
                    OutputEncode.BackColor = Color.MistyRose;
                    return;
                }
                OutputEncode.BackColor = Color.Empty;
                ActiveOutputFilters.Items.Add(new ListViewItem
                {
                    Text = OutputEncode.Text,
                    Tag = wasGetEnumValueFromDescription<Filter>(OutputEncode.Text)
                });
            }));
        }

        private void DeleteSelectedOutputFilterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = ActiveOutputFilters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    ActiveOutputFilters.BackColor = Color.MistyRose;
                    return;
                }
                ActiveOutputFilters.BackColor = Color.Empty;
                ActiveOutputFilters.Items.RemoveAt(ActiveOutputFilters.SelectedIndex);
            }));
        }

        private void DeleteSelectedInputFilterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = ActiveInputFilters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    ActiveInputFilters.BackColor = Color.MistyRose;
                    return;
                }
                ActiveInputFilters.BackColor = Color.Empty;
                ActiveInputFilters.Items.RemoveAt(ActiveInputFilters.SelectedIndex);
            }));
        }

        private void AddENIGMARotorRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(ENIGMARotor.Text))
                {
                    ENIGMARotor.BackColor = Color.MistyRose;
                    return;
                }
                ENIGMARotor.BackColor = Color.Empty;
                ENIGMARotorSequence.Items.Add(new ListViewItem
                {
                    Text = ENIGMARotor.Text,
                    Tag = ENIGMARotor.Text[0]
                });
            }));
        }

        private void DeleteENIGMARotorRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = ENIGMARotorSequence.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    ENIGMARotorSequence.BackColor = Color.MistyRose;
                    return;
                }
                ENIGMARotorSequence.BackColor = Color.Empty;
                ENIGMARotorSequence.Items.RemoveAt(ENIGMARotorSequence.SelectedIndex);
            }));
        }

        private void AddENIGMAPlugRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(ENIGMARing.Text))
                {
                    ENIGMARing.BackColor = Color.MistyRose;
                    return;
                }
                ENIGMARing.BackColor = Color.Empty;
                ENIGMAPlugSequence.Items.Add(new ListViewItem
                {
                    Text = ENIGMARing.Text,
                    Tag = ENIGMARing.Text[0]
                });
            }));
        }

        private void DeleteENIGMAPlugRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = ENIGMAPlugSequence.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    ENIGMAPlugSequence.BackColor = Color.MistyRose;
                    return;
                }
                ENIGMAPlugSequence.BackColor = Color.Empty;
                ENIGMAPlugSequence.Items.RemoveAt(ENIGMAPlugSequence.SelectedIndex);
            }));
        }

        private void AddMasterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                if (string.IsNullOrEmpty(MasterFirstName.Text) || string.IsNullOrEmpty(MasterLastName.Text))
                {
                    MasterFirstName.BackColor = Color.MistyRose;
                    MasterLastName.BackColor = Color.MistyRose;
                    return;
                }
                MasterFirstName.BackColor = Color.Empty;
                MasterLastName.BackColor = Color.Empty;
                Masters.Items.Add(new ListViewItem
                {
                    Text = MasterFirstName.Text + @" " + MasterLastName.Text,
                    Tag = new Master {FirstName = MasterFirstName.Text, LastName = MasterLastName.Text}
                });
                corradeConfiguration.Masters.Add(new Master
                {
                    FirstName = MasterFirstName.Text,
                    LastName = MasterLastName.Text
                });
            }));
        }

        private void DeleteMasterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Masters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    Masters.BackColor = Color.MistyRose;
                    return;
                }
                Masters.BackColor = Color.Empty;
                corradeConfiguration.Masters.Remove((Master) ((ListViewItem) Masters.Items[Masters.SelectedIndex]).Tag);
                Masters.Items.RemoveAt(Masters.SelectedIndex);
            }));
        }

        private void ClearPasswordRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() => { mainForm.Password.Text = string.Empty; }));
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
                .AsParallel().Select(o => wasGetDescriptionFromEnumValue((Enum) o.GetValue(null)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description from an enumeration value.
        /// </summary>
        /// <param name="value">an enumeration value</param>
        /// <returns>the description or the empty string</returns>
        private static string wasGetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof (DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute != null ? attribute.Description : string.Empty;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get enumeration value from its description.
        /// </summary>
        /// <typeparam name="T">the enumeration type</typeparam>
        /// <param name="description">the description of a member</param>
        /// <returns>the value or the default of T if case no description found</returns>
        private static T wasGetEnumValueFromDescription<T>(string description)
        {
            var field = typeof (T).GetFields()
                .AsParallel().SelectMany(f => f.GetCustomAttributes(
                    typeof (DescriptionAttribute), false), (
                        f, a) => new {Field = f, Att = a}).SingleOrDefault(a => ((DescriptionAttribute) a.Att)
                            .Description.Equals(description));
            return field != null ? (T) field.Field.GetRawConstantValue() : default(T);
        }

        private void CorradeConfiguratorShown(object sender, EventArgs e)
        {
            mainForm.Version.Text = @"v" + CORRADE_CONSTANTS.CONFIGURATOR_VERSION;
        }

        /// <summary>
        ///     Constants used by Corrade.
        /// </summary>
        public struct CORRADE_CONSTANTS
        {
            public const string CORRADE = @"Corrade";
            public const string WIZARDRY_AND_STEAMWORKS_WEBSITE = @"http://grimore.org";

            /// <summary>
            ///     Corrade compile date.
            /// </summary>
            public static readonly string CORRADE_CONFIGURATOR_COMPILE_DATE = new DateTime(2000, 1, 1).Add(new TimeSpan(
                TimeSpan.TicksPerDay*Assembly.GetEntryAssembly().GetName().Version.Build + // days since 1 January 2000
                TimeSpan.TicksPerSecond*2*Assembly.GetEntryAssembly().GetName().Version.Revision)).ToLongDateString();

            /// <summary>
            ///     Corrade version.
            /// </summary>
            public static readonly string CONFIGURATOR_VERSION =
                Assembly.GetEntryAssembly().GetName().Version.ToString();

            /// <summary>
            ///     Corrade user agent.
            /// </summary>
            public static readonly string USER_AGENT =
                $"{CORRADE}/{CONFIGURATOR_VERSION} ({WIZARDRY_AND_STEAMWORKS_WEBSITE})";

            /// <summary>
            ///     Conten-types that Corrade can send and receive.
            /// </summary>
            public struct CONTENT_TYPE
            {
                public const string TEXT_PLAIN = @"text/plain";
                public const string WWW_FORM_URLENCODED = @"application/x-www-form-urlencoded";
            }
        }

        [Serializable]
        public class CorradeConfiguration
        {
            private uint _activateDelay = 5000;
            private bool _autoActivateGroup;
            private string _bindIPAddress = string.Empty;
            private uint _callbackQueueLength = 100;
            private uint _callbackThrottle = 1000;
            private uint _callbackTimeout = 5000;
            private UUID _clientIdentificationTag = new UUID("0705230f-cbd0-99bd-040b-28eb348b5255");
            private bool _clientLogEnabled = true;
            private string _clientLogFile = "logs/Corrade.log";
            private uint _connectionIdleTime = 900000;
            private uint _connectionLimit = 100;
            private wasAdaptiveAlarm.DECAY_TYPE _dataDecayType = wasAdaptiveAlarm.DECAY_TYPE.ARITHMETIC;
            private uint _dataTimeout = 2500;
            private string _driveIdentifierHash = string.Empty;
            private bool _enableAIML;
            private bool _enableHTTPServer;
            private bool _enableRLV;
            private bool _enableTCPNotificationsServer;

            private ENIGMA _enigma = new ENIGMA
            {
                rotors = new[] { '3', 'g', '1' },
                plugs = new[] { 'z', 'p', 'q' },
                reflector = 'b'
            };

            private int _exitCodeAbnormal = -2;
            private int _exitCodeExpected = -1;
            private string _firstName = string.Empty;
            private uint _groupCreateFee = 100;
            private HashSet<Group> _groups = new HashSet<Group>();
            private uint _HTTPServerBodyTimeout = 5000;
            private HTTPCompressionMethod _HTTPServerCompression = HTTPCompressionMethod.NONE;
            private uint _HTTPServerDrainTimeout = 10000;
            private uint _HTTPServerHeaderTimeout = 2500;
            private uint _HTTPServerIdleTimeout = 2500;
            private bool _HTTPServerKeepAlive = true;
            private string _HTTPServerPrefix = @"http://+:8080/";
            private uint _HTTPServerQueueTimeout = 10000;
            private uint _HTTPServerTimeout = 5000;
            private List<Filter> _inputFilters = new List<Filter>();
            private string _instantMessageLogDirectory = @"logs/im";
            private bool _instantMessageLogEnabled;
            private string _lastName = string.Empty;
            private string _localMessageLogDirectory = @"logs/local";
            private bool _localMessageLogEnabled;
            private string _loginURL = @"https://login.agni.lindenlab.com/cgi-bin/login.cgi";
            private uint _logoutGrace = 2500;
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
            private string _startLocation = "last";
            private uint _TCPnotificationQueueLength = 100;
            private string _TCPNotificationsServerAddress = @"0.0.0.0";
            private uint _TCPNotificationsServerPort = 8095;
            private uint _TCPnotificationThrottle = 1000;
            private uint _throttleAsset = 100000;
            private uint _throttleCloud = 10000;
            private uint _throttleLand = 80000;
            private uint _throttleResend = 100000;
            private uint _throttleTask = 200000;
            private uint _throttleTexture = 100000;
            private uint _throttleTotal = 600000;
            private uint _throttleWind = 10000;
            private bool _TOSAccepted;
            private bool _useExpect100Continue;
            private bool _useNaggle;
            private string _vigenereSecret = string.Empty;
            private byte[] _AESKey;
            private byte[] _AESIV;

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

            public bool EnableAIML
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enableAIML;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enableAIML = value;
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

            public wasAdaptiveAlarm.DECAY_TYPE DataDecayType
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

            public uint ActivateDelay
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _activateDelay;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _activateDelay = value;
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

            public ENIGMA ENIGMA
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enigma;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enigma = value;
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

            public static void Save(string file, ref CorradeConfiguration configuration)
            {
                lock (ConfigurationFileLock)
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (CorradeConfiguration));
                        serializer.Serialize(writer, configuration);
                        //writer.Flush();
                    }
                }
            }

            public static void Load(string file, ref CorradeConfiguration configuration)
            {
                lock (ConfigurationFileLock)
                {
                    using (StreamReader stream = new StreamReader(file, Encoding.UTF8))
                    {
                        XmlSerializer serializer =
                            new XmlSerializer(typeof (CorradeConfiguration));
                        CorradeConfiguration loadedConfiguration = (CorradeConfiguration) serializer.Deserialize(stream);
                        configuration = loadedConfiguration;
                    }
                }
            }

            public void LoadLegacy(string file)
            {
                mainForm.BeginInvoke(
                    (Action) (() => { mainForm.StatusText.Text = @"loading configuration"; }));
                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 0; }));

                try
                {
                    lock (ConfigurationFileLock)
                    {
                        file = File.ReadAllText(file, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                    return;
                }

                XmlDocument conf = new XmlDocument();
                try
                {
                    conf.LoadXml(file);
                }
                catch (XmlException ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                    return;
                }

                XmlNode root = conf.DocumentElement;
                if (root == null)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = @"invalid configuration file"; }));
                    return;
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 6; }));

                // Process client.
                try
                {
                    foreach (XmlNode client in root.SelectNodes("/config/client/*"))
                        switch (client.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.FIRST_NAME:
                                if (string.IsNullOrEmpty(client.InnerText))
                                {
                                    throw new Exception("error in client section");
                                }
                                FirstName = client.InnerText;
                                break;
                            case ConfigurationKeys.LAST_NAME:
                                if (string.IsNullOrEmpty(client.InnerText))
                                {
                                    throw new Exception("error in client section");
                                }
                                LastName = client.InnerText;
                                break;
                            case ConfigurationKeys.PASSWORD:
                                if (string.IsNullOrEmpty(client.InnerText))
                                {
                                    throw new Exception("error in client section");
                                }
                                Password = client.InnerText;
                                break;
                            case ConfigurationKeys.LOGIN_URL:
                                if (string.IsNullOrEmpty(client.InnerText))
                                {
                                    throw new Exception("error in client section");
                                }
                                LoginURL = client.InnerText;
                                break;
                            case ConfigurationKeys.TOS_ACCEPTED:
                                bool accepted;
                                if (!bool.TryParse(client.InnerText, out accepted))
                                {
                                    throw new Exception("error in client section");
                                }
                                TOSAccepted = accepted;
                                break;
                            case ConfigurationKeys.GROUP_CREATE_FEE:
                                uint groupCreateFee;
                                if (!uint.TryParse(client.InnerText, out groupCreateFee))
                                {
                                    throw new Exception("error in client section");
                                }
                                GroupCreateFee = groupCreateFee;
                                break;
                            case ConfigurationKeys.EXIT_CODE:
                                XmlNodeList exitCodeNodeList = client.SelectNodes("*");
                                if (exitCodeNodeList == null)
                                {
                                    throw new Exception("error in client section");
                                }
                                foreach (XmlNode exitCodeNode in exitCodeNodeList)
                                {
                                    switch (exitCodeNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.EXPECTED:
                                            int exitCodeExpected;
                                            if (!int.TryParse(exitCodeNode.InnerText, out exitCodeExpected))
                                            {
                                                throw new Exception("error in client section");
                                            }
                                            ExitCodeExpected = exitCodeExpected;
                                            break;
                                        case ConfigurationKeys.ABNORMAL:
                                            int exitCodeAbnormal;
                                            if (!int.TryParse(exitCodeNode.InnerText, out exitCodeAbnormal))
                                            {
                                                throw new Exception("error in client section");
                                            }
                                            ExitCodeAbnormal = exitCodeAbnormal;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.AUTO_ACTIVATE_GROUP:
                                bool autoActivateGroup;
                                if (!bool.TryParse(client.InnerText, out autoActivateGroup))
                                {
                                    throw new Exception("error in client section");
                                }
                                AutoActivateGroup = autoActivateGroup;
                                break;
                            case ConfigurationKeys.START_LOCATION:
                                if (string.IsNullOrEmpty(client.InnerText))
                                {
                                    throw new Exception("error in client section");
                                }
                                StartLocation = client.InnerText;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 12; }));

                // Process logs.
                try
                {
                    foreach (XmlNode LogNode in root.SelectNodes("/config/logs/*"))
                        switch (LogNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.IM:
                                XmlNodeList imLogNodeList = LogNode.SelectNodes("*");
                                if (imLogNodeList == null)
                                {
                                    throw new Exception("error in logs section");
                                }
                                foreach (XmlNode imLogNode in imLogNodeList)
                                {
                                    switch (imLogNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENABLE:
                                            bool enable;
                                            if (!bool.TryParse(imLogNode.InnerText, out enable))
                                            {
                                                throw new Exception("error in im logs section");
                                            }
                                            InstantMessageLogEnabled = enable;
                                            break;
                                        case ConfigurationKeys.DIRECTORY:
                                            if (string.IsNullOrEmpty(imLogNode.InnerText))
                                            {
                                                throw new Exception("error in im logs section");
                                            }
                                            InstantMessageLogDirectory = imLogNode.InnerText;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.CLIENT:
                                XmlNodeList clientLogNodeList = LogNode.SelectNodes("*");
                                if (clientLogNodeList == null)
                                {
                                    throw new Exception("error in logs section");
                                }
                                foreach (XmlNode clientLogNode in clientLogNodeList)
                                {
                                    switch (clientLogNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENABLE:
                                            bool enable;
                                            if (!bool.TryParse(clientLogNode.InnerText, out enable))
                                            {
                                                throw new Exception("error in client logs section");
                                            }
                                            ClientLogEnabled = enable;
                                            break;
                                        case ConfigurationKeys.FILE:
                                            if (string.IsNullOrEmpty(clientLogNode.InnerText))
                                            {
                                                throw new Exception("error in client logs section");
                                            }
                                            ClientLogFile = clientLogNode.InnerText;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.LOCAL:
                                XmlNodeList localLogNodeList = LogNode.SelectNodes("*");
                                if (localLogNodeList == null)
                                {
                                    throw new Exception("error in logs section");
                                }
                                foreach (XmlNode localLogNode in localLogNodeList)
                                {
                                    switch (localLogNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENABLE:
                                            bool enable;
                                            if (!bool.TryParse(localLogNode.InnerText, out enable))
                                            {
                                                throw new Exception("error in local logs section");
                                            }
                                            LocalMessageLogEnabled = enable;
                                            break;
                                        case ConfigurationKeys.DIRECTORY:
                                            if (string.IsNullOrEmpty(localLogNode.InnerText))
                                            {
                                                throw new Exception("error in local logs section");
                                            }
                                            LocalMessageLogDirectory = localLogNode.InnerText;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.REGION:
                                XmlNodeList regionLogNodeList = LogNode.SelectNodes("*");
                                if (regionLogNodeList == null)
                                {
                                    throw new Exception("error in logs section");
                                }
                                foreach (XmlNode regionLogNode in regionLogNodeList)
                                {
                                    switch (regionLogNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENABLE:
                                            bool enable;
                                            if (!bool.TryParse(regionLogNode.InnerText, out enable))
                                            {
                                                throw new Exception("error in local logs section");
                                            }
                                            RegionMessageLogEnabled = enable;
                                            break;
                                        case ConfigurationKeys.DIRECTORY:
                                            if (string.IsNullOrEmpty(regionLogNode.InnerText))
                                            {
                                                throw new Exception("error in local logs section");
                                            }
                                            RegionMessageLogDirectory = regionLogNode.InnerText;
                                            break;
                                    }
                                }
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 18; }));

                // Process filters.
                try
                {
                    foreach (XmlNode FilterNode in root.SelectNodes("/config/filters/*"))
                        switch (FilterNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.INPUT:
                                XmlNodeList inputFilterNodeList = FilterNode.SelectNodes("*");
                                if (inputFilterNodeList == null)
                                {
                                    throw new Exception("error in filters section");
                                }
                                InputFilters = new List<Filter>();
                                foreach (XmlNode inputFilterNode in inputFilterNodeList)
                                {
                                    switch (inputFilterNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENCODE:
                                        case ConfigurationKeys.DECODE:
                                        case ConfigurationKeys.ENCRYPT:
                                        case ConfigurationKeys.DECRYPT:
                                            InputFilters.Add(wasGetEnumValueFromDescription<Filter>(
                                                inputFilterNode.InnerText));
                                            break;
                                        default:
                                            throw new Exception("error in input filters section");
                                    }
                                }
                                break;
                            case ConfigurationKeys.OUTPUT:
                                XmlNodeList outputFilterNodeList = FilterNode.SelectNodes("*");
                                if (outputFilterNodeList == null)
                                {
                                    throw new Exception("error in filters section");
                                }
                                OutputFilters = new List<Filter>();
                                foreach (XmlNode outputFilterNode in outputFilterNodeList)
                                {
                                    switch (outputFilterNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ENCODE:
                                        case ConfigurationKeys.DECODE:
                                        case ConfigurationKeys.ENCRYPT:
                                        case ConfigurationKeys.DECRYPT:
                                            OutputFilters.Add(wasGetEnumValueFromDescription<Filter>(
                                                outputFilterNode.InnerText));
                                            break;
                                        default:
                                            throw new Exception("error in output filters section");
                                    }
                                }
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 24; }));

                // Process cryptography.
                try
                {
                    foreach (XmlNode FilterNode in root.SelectNodes("/config/cryptography/*"))
                        switch (FilterNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.ENIGMA:
                                XmlNodeList ENIGMANodeList = FilterNode.SelectNodes("*");
                                if (ENIGMANodeList == null)
                                {
                                    throw new Exception("error in cryptography section");
                                }
                                ENIGMA enigma = new ENIGMA();
                                foreach (XmlNode ENIGMANode in ENIGMANodeList)
                                {
                                    switch (ENIGMANode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.ROTORS:
                                            enigma.rotors = ENIGMANode.InnerText.ToArray();
                                            break;
                                        case ConfigurationKeys.PLUGS:
                                            enigma.plugs = ENIGMANode.InnerText.ToArray();
                                            break;
                                        case ConfigurationKeys.REFLECTOR:
                                            enigma.reflector = ENIGMANode.InnerText.SingleOrDefault();
                                            break;
                                    }
                                }
                                ENIGMA = enigma;
                                break;
                            case ConfigurationKeys.VIGENERE:
                                XmlNodeList VIGENERENodeList = FilterNode.SelectNodes("*");
                                if (VIGENERENodeList == null)
                                {
                                    throw new Exception("error in cryptography section");
                                }
                                foreach (XmlNode VIGENERENode in VIGENERENodeList)
                                {
                                    switch (VIGENERENode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.SECRET:
                                            VIGENERESecret = VIGENERENode.InnerText;
                                            break;
                                    }
                                }
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 30; }));

                // Process AIML.
                try
                {
                    foreach (XmlNode AIMLNode in root.SelectNodes("/config/aiml/*"))
                        switch (AIMLNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.ENABLE:
                                bool enable;
                                if (!bool.TryParse(AIMLNode.InnerText, out enable))
                                {
                                    throw new Exception("error in AIML section");
                                }
                                EnableAIML = enable;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 36; }));

                // Process RLV.
                try
                {
                    foreach (XmlNode RLVNode in root.SelectNodes("/config/rlv/*"))
                        switch (RLVNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.ENABLE:
                                bool enable;
                                if (!bool.TryParse(RLVNode.InnerText, out enable))
                                {
                                    throw new Exception("error in RLV section");
                                }
                                EnableRLV = enable;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 42; }));

                // Process server.
                try
                {
                    foreach (XmlNode serverNode in root.SelectNodes("/config/server/*"))
                        switch (serverNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.HTTP:
                                bool enableHTTPServer;
                                if (!bool.TryParse(serverNode.InnerText, out enableHTTPServer))
                                {
                                    throw new Exception("error in server section");
                                }
                                EnableHTTPServer = enableHTTPServer;
                                break;
                            case ConfigurationKeys.PREFIX:
                                if (string.IsNullOrEmpty(serverNode.InnerText))
                                {
                                    throw new Exception("error in server section");
                                }
                                HTTPServerPrefix = serverNode.InnerText;
                                break;
                            case ConfigurationKeys.COMPRESSION:
                                HTTPServerCompression =
                                    wasGetEnumValueFromDescription<HTTPCompressionMethod>(serverNode.InnerText);
                                break;
                            case ConfigurationKeys.KEEP_ALIVE:
                                bool HTTPKeepAlive;
                                if (!bool.TryParse(serverNode.InnerText, out HTTPKeepAlive))
                                {
                                    throw new Exception("error in server section");
                                }
                                HTTPServerKeepAlive = HTTPKeepAlive;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 48; }));

                // Process network.
                try
                {
                    foreach (XmlNode networkNode in root.SelectNodes("/config/network/*"))
                        switch (networkNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.BIND:
                                if (!string.IsNullOrEmpty(networkNode.InnerText))
                                {
                                    BindIPAddress = networkNode.InnerText;
                                }
                                break;
                            case ConfigurationKeys.MAC:
                                if (!string.IsNullOrEmpty(networkNode.InnerText))
                                {
                                    NetworkCardMAC = networkNode.InnerText;
                                }
                                break;
                            case ConfigurationKeys.ID0:
                                if (!string.IsNullOrEmpty(networkNode.InnerText))
                                {
                                    DriveIdentifierHash = networkNode.InnerText;
                                }
                                break;
                            case ConfigurationKeys.NAGGLE:
                                bool useNaggle;
                                if (!bool.TryParse(networkNode.InnerText, out useNaggle))
                                {
                                    throw new Exception("error in network section");
                                }
                                UseNaggle = useNaggle;
                                break;
                            case ConfigurationKeys.EXPECT100CONTINUE:
                                bool useExpect100Continue;
                                if (!bool.TryParse(networkNode.InnerText, out useExpect100Continue))
                                {
                                    throw new Exception("error in network section");
                                }
                                UseExpect100Continue = useExpect100Continue;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 54; }));

                // Process throttles
                try
                {
                    foreach (XmlNode throttlesNode in root.SelectNodes("/config/throttles/*"))
                        switch (throttlesNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.TOTAL:
                                uint throttleTotal;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleTotal))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleTotal = throttleTotal;
                                break;
                            case ConfigurationKeys.LAND:
                                uint throttleLand;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleLand))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleLand = throttleLand;
                                break;
                            case ConfigurationKeys.TASK:
                                uint throttleTask;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleTask))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleLand = throttleTask;
                                break;
                            case ConfigurationKeys.TEXTURE:
                                uint throttleTexture;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleTexture))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleTexture = throttleTexture;
                                break;
                            case ConfigurationKeys.WIND:
                                uint throttleWind;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleWind))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleWind = throttleWind;
                                break;
                            case ConfigurationKeys.RESEND:
                                uint throttleResend;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleResend))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleResend = throttleResend;
                                break;
                            case ConfigurationKeys.ASSET:
                                uint throttleAsset;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleAsset))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleAsset = throttleAsset;
                                break;
                            case ConfigurationKeys.CLOUD:
                                uint throttleCloud;
                                if (!uint.TryParse(throttlesNode.InnerText,
                                    out throttleCloud))
                                {
                                    throw new Exception("error in throttles section");
                                }
                                ThrottleCloud = throttleCloud;
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 60; }));

                // Process limits.
                try
                {
                    foreach (XmlNode limitsNode in root.SelectNodes("/config/limits/*"))
                        switch (limitsNode.Name.ToLowerInvariant())
                        {
                            case ConfigurationKeys.RANGE:
                                float range;
                                if (!float.TryParse(limitsNode.InnerText,
                                    out range))
                                {
                                    throw new Exception("error in range limits section");
                                }
                                Range = range;
                                break;
                            case ConfigurationKeys.RLV:
                                XmlNodeList rlvLimitNodeList = limitsNode.SelectNodes("*");
                                if (rlvLimitNodeList == null)
                                {
                                    throw new Exception("error in RLV limits section");
                                }
                                foreach (XmlNode rlvLimitNode in rlvLimitNodeList)
                                {
                                    switch (rlvLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint maximumRLVThreads;
                                            if (
                                                !uint.TryParse(rlvLimitNode.InnerText,
                                                    out maximumRLVThreads))
                                            {
                                                throw new Exception("error in RLV limits section");
                                            }
                                            MaximumRLVThreads = maximumRLVThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.COMMANDS:
                                XmlNodeList commandsLimitNodeList = limitsNode.SelectNodes("*");
                                if (commandsLimitNodeList == null)
                                {
                                    throw new Exception("error in commands limits section");
                                }
                                foreach (XmlNode commandsLimitNode in commandsLimitNodeList)
                                {
                                    switch (commandsLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint maximumCommandThreads;
                                            if (
                                                !uint.TryParse(commandsLimitNode.InnerText,
                                                    out maximumCommandThreads))
                                            {
                                                throw new Exception("error in commands limits section");
                                            }
                                            MaximumCommandThreads = maximumCommandThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.IM:
                                XmlNodeList instantMessageLimitNodeList = limitsNode.SelectNodes("*");
                                if (instantMessageLimitNodeList == null)
                                {
                                    throw new Exception("error in instant message limits section");
                                }
                                foreach (XmlNode instantMessageLimitNode in instantMessageLimitNodeList)
                                {
                                    switch (instantMessageLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint maximumInstantMessageThreads;
                                            if (
                                                !uint.TryParse(instantMessageLimitNode.InnerText,
                                                    out maximumInstantMessageThreads))
                                            {
                                                throw new Exception("error in instant message limits section");
                                            }
                                            MaximumInstantMessageThreads = maximumInstantMessageThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.SCHEDULER:
                                XmlNodeList schedulerLimitNodeList = limitsNode.SelectNodes("*");
                                if (schedulerLimitNodeList == null)
                                {
                                    throw new Exception("error in scheduler limits section");
                                }
                                foreach (XmlNode schedulerLimitNode in schedulerLimitNodeList)
                                {
                                    switch (schedulerLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint expiration;
                                            if (
                                                !uint.TryParse(schedulerLimitNode.InnerText,
                                                    out expiration))
                                            {
                                                throw new Exception("error in scheduler limits section");
                                            }
                                            SchedulerExpiration = expiration;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.LOG:
                                XmlNodeList logLimitNodeList = limitsNode.SelectNodes("*");
                                if (logLimitNodeList == null)
                                {
                                    throw new Exception("error in log limits section");
                                }
                                foreach (XmlNode logLimitNode in logLimitNodeList)
                                {
                                    switch (logLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint maximumLogThreads;
                                            if (
                                                !uint.TryParse(logLimitNode.InnerText,
                                                    out maximumLogThreads))
                                            {
                                                throw new Exception("error in log limits section");
                                            }
                                            MaximumLogThreads = maximumLogThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.POST:
                                XmlNodeList postLimitNodeList = limitsNode.SelectNodes("*");
                                if (postLimitNodeList == null)
                                {
                                    throw new Exception("error in post limits section");
                                }
                                foreach (XmlNode postLimitNode in postLimitNodeList)
                                {
                                    switch (postLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.THREADS:
                                            uint maximumPOSTThreads;
                                            if (
                                                !uint.TryParse(postLimitNode.InnerText,
                                                    out maximumPOSTThreads))
                                            {
                                                throw new Exception("error in post limits section");
                                            }
                                            MaximumPOSTThreads = maximumPOSTThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.CLIENT:
                                XmlNodeList clientLimitNodeList = limitsNode.SelectNodes("*");
                                if (clientLimitNodeList == null)
                                {
                                    throw new Exception("error in client limits section");
                                }
                                foreach (XmlNode clientLimitNode in clientLimitNodeList)
                                {
                                    switch (clientLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.CONNECTIONS:
                                            uint connectionLimit;
                                            if (
                                                !uint.TryParse(clientLimitNode.InnerText,
                                                    out connectionLimit))
                                            {
                                                throw new Exception("error in client limits section");
                                            }
                                            ConnectionLimit = connectionLimit;
                                            break;
                                        case ConfigurationKeys.IDLE:
                                            uint connectionIdleTime;
                                            if (
                                                !uint.TryParse(clientLimitNode.InnerText,
                                                    out connectionIdleTime))
                                            {
                                                throw new Exception("error in client limits section");
                                            }
                                            ConnectionIdleTime = connectionIdleTime;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.CALLBACKS:
                                XmlNodeList callbackLimitNodeList = limitsNode.SelectNodes("*");
                                if (callbackLimitNodeList == null)
                                {
                                    throw new Exception("error in callback limits section");
                                }
                                foreach (XmlNode callbackLimitNode in callbackLimitNodeList)
                                {
                                    switch (callbackLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint callbackTimeout;
                                            if (!uint.TryParse(callbackLimitNode.InnerText, out callbackTimeout))
                                            {
                                                throw new Exception("error in callback limits section");
                                            }
                                            CallbackTimeout = callbackTimeout;
                                            break;
                                        case ConfigurationKeys.THROTTLE:
                                            uint callbackThrottle;
                                            if (
                                                !uint.TryParse(callbackLimitNode.InnerText, out callbackThrottle))
                                            {
                                                throw new Exception("error in callback limits section");
                                            }
                                            CallbackThrottle = callbackThrottle;
                                            break;
                                        case ConfigurationKeys.QUEUE_LENGTH:
                                            uint callbackQueueLength;
                                            if (
                                                !uint.TryParse(callbackLimitNode.InnerText,
                                                    out callbackQueueLength))
                                            {
                                                throw new Exception("error in callback limits section");
                                            }
                                            CallbackQueueLength = callbackQueueLength;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.NOTIFICATIONS:
                                XmlNodeList notificationLimitNodeList = limitsNode.SelectNodes("*");
                                if (notificationLimitNodeList == null)
                                {
                                    throw new Exception("error in notification limits section");
                                }
                                foreach (XmlNode notificationLimitNode in notificationLimitNodeList)
                                {
                                    switch (notificationLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint notificationTimeout;
                                            if (
                                                !uint.TryParse(notificationLimitNode.InnerText,
                                                    out notificationTimeout))
                                            {
                                                throw new Exception("error in notification limits section");
                                            }
                                            NotificationTimeout = notificationTimeout;
                                            break;
                                        case ConfigurationKeys.THROTTLE:
                                            uint notificationThrottle;
                                            if (
                                                !uint.TryParse(notificationLimitNode.InnerText,
                                                    out notificationThrottle))
                                            {
                                                throw new Exception("error in notification limits section");
                                            }
                                            NotificationThrottle = notificationThrottle;
                                            break;
                                        case ConfigurationKeys.QUEUE_LENGTH:
                                            uint notificationQueueLength;
                                            if (
                                                !uint.TryParse(notificationLimitNode.InnerText,
                                                    out notificationQueueLength))
                                            {
                                                throw new Exception("error in notification limits section");
                                            }
                                            NotificationQueueLength = notificationQueueLength;
                                            break;
                                        case ConfigurationKeys.THREADS:
                                            uint maximumNotificationThreads;
                                            if (
                                                !uint.TryParse(notificationLimitNode.InnerText,
                                                    out maximumNotificationThreads))
                                            {
                                                throw new Exception("error in notification limits section");
                                            }
                                            MaximumNotificationThreads = maximumNotificationThreads;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.SERVER:
                                XmlNodeList HTTPServerLimitNodeList = limitsNode.SelectNodes("*");
                                if (HTTPServerLimitNodeList == null)
                                {
                                    throw new Exception("error in server limits section");
                                }
                                foreach (XmlNode HTTPServerLimitNode in HTTPServerLimitNodeList)
                                {
                                    switch (HTTPServerLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint HTTPServerTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerTimeout = HTTPServerTimeoutValue;
                                            break;
                                        case ConfigurationKeys.DRAIN:
                                            uint HTTPServerDrainTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerDrainTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerDrainTimeout = HTTPServerDrainTimeoutValue;
                                            break;
                                        case ConfigurationKeys.BODY:
                                            uint HTTPServerBodyTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerBodyTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerBodyTimeout = HTTPServerBodyTimeoutValue;
                                            break;
                                        case ConfigurationKeys.HEADER:
                                            uint HTTPServerHeaderTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerHeaderTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerHeaderTimeout = HTTPServerHeaderTimeoutValue;
                                            break;
                                        case ConfigurationKeys.IDLE:
                                            uint HTTPServerIdleTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerIdleTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerIdleTimeout = HTTPServerIdleTimeoutValue;
                                            break;
                                        case ConfigurationKeys.QUEUE:
                                            uint HTTPServerQueueTimeoutValue;
                                            if (
                                                !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                    out HTTPServerQueueTimeoutValue))
                                            {
                                                throw new Exception("error in server limits section");
                                            }
                                            HTTPServerQueueTimeout = HTTPServerQueueTimeoutValue;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.SERVICES:
                                XmlNodeList servicesLimitNodeList = limitsNode.SelectNodes("*");
                                if (servicesLimitNodeList == null)
                                {
                                    throw new Exception("error in services limits section");
                                }
                                foreach (XmlNode servicesLimitNode in servicesLimitNodeList)
                                {
                                    switch (servicesLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint servicesTimeout;
                                            if (
                                                !uint.TryParse(servicesLimitNode.InnerText,
                                                    out servicesTimeout))
                                            {
                                                throw new Exception("error in services limits section");
                                            }
                                            ServicesTimeout = servicesTimeout;
                                            break;
                                        case ConfigurationKeys.REBAKE:
                                            uint rebakeDelay;
                                            if (!uint.TryParse(servicesLimitNode.InnerText, out rebakeDelay))
                                            {
                                                throw new Exception("error in services limits section");
                                            }
                                            RebakeDelay = rebakeDelay;
                                            break;
                                        case ConfigurationKeys.ACTIVATE:
                                            uint activateDelay;
                                            if (
                                                !uint.TryParse(servicesLimitNode.InnerText,
                                                    out activateDelay))
                                            {
                                                throw new Exception("error in services limits section");
                                            }
                                            ActivateDelay = activateDelay;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.DATA:
                                XmlNodeList dataLimitNodeList = limitsNode.SelectNodes("*");
                                if (dataLimitNodeList == null)
                                {
                                    throw new Exception("error in data limits section");
                                }
                                foreach (XmlNode dataLimitNode in dataLimitNodeList)
                                {
                                    switch (dataLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint dataTimeout;
                                            if (
                                                !uint.TryParse(dataLimitNode.InnerText,
                                                    out dataTimeout))
                                            {
                                                throw new Exception("error in data limits section");
                                            }
                                            DataTimeout = dataTimeout;
                                            break;
                                        case ConfigurationKeys.DECAY:
                                            DataDecayType =
                                                wasGetEnumValueFromDescription<wasAdaptiveAlarm.DECAY_TYPE>(
                                                    dataLimitNode.InnerText);
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.MEMBERSHIP:
                                XmlNodeList membershipLimitNodeList = limitsNode.SelectNodes("*");
                                if (membershipLimitNodeList == null)
                                {
                                    throw new Exception("error in membership limits section");
                                }
                                foreach (XmlNode servicesLimitNode in membershipLimitNodeList)
                                {
                                    switch (servicesLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.SWEEP:
                                            uint membershipSweepInterval;
                                            if (
                                                !uint.TryParse(servicesLimitNode.InnerText,
                                                    out membershipSweepInterval))
                                            {
                                                throw new Exception("error in membership limits section");
                                            }
                                            MembershipSweepInterval = membershipSweepInterval;
                                            break;
                                    }
                                }
                                break;
                            case ConfigurationKeys.LOGOUT:
                                XmlNodeList logoutLimitNodeList = limitsNode.SelectNodes("*");
                                if (logoutLimitNodeList == null)
                                {
                                    throw new Exception("error in logout limits section");
                                }
                                foreach (XmlNode logoutLimitNode in logoutLimitNodeList)
                                {
                                    switch (logoutLimitNode.Name.ToLowerInvariant())
                                    {
                                        case ConfigurationKeys.TIMEOUT:
                                            uint logoutGrace;
                                            if (
                                                !uint.TryParse(logoutLimitNode.InnerText,
                                                    out logoutGrace))
                                            {
                                                throw new Exception("error in logout limits section");
                                            }
                                            LogoutGrace = logoutGrace;
                                            break;
                                    }
                                }
                                break;
                        }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 66; }));

                // Process masters.
                try
                {
                    foreach (XmlNode mastersNode in root.SelectNodes("/config/masters/*"))
                    {
                        Master configMaster = new Master();
                        foreach (XmlNode masterNode in mastersNode.ChildNodes)
                        {
                            switch (masterNode.Name.ToLowerInvariant())
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
                        Masters.Add(configMaster);
                    }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 80; }));

                // Process groups.
                try
                {
                    foreach (XmlNode groupsNode in root.SelectNodes("/config/groups/*"))
                    {
                        Group configGroup = new Group
                        {
                            ChatLog = string.Empty,
                            ChatLogEnabled = false,
                            DatabaseFile = string.Empty,
                            Name = string.Empty,
                            Notifications = new HashSet<Notifications>(),
                            Password = string.Empty,
                            Permissions = new HashSet<Permissions>(),
                            UUID = UUID.Zero,
                            Workers = 5
                        };
                        foreach (XmlNode groupNode in groupsNode.ChildNodes)
                        {
                            switch (groupNode.Name.ToLowerInvariant())
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
                                case ConfigurationKeys.WORKERS:
                                    if (!uint.TryParse(groupNode.InnerText, out configGroup.Workers))
                                    {
                                        throw new Exception("error in group section");
                                    }
                                    break;
                                case ConfigurationKeys.CHATLOG:
                                    XmlNodeList groupChatLogNodeList = groupNode.SelectNodes("*");
                                    if (groupChatLogNodeList == null)
                                    {
                                        throw new Exception("error in group section");
                                    }
                                    foreach (XmlNode groupChatLogNode in groupChatLogNodeList)
                                    {
                                        switch (groupChatLogNode.Name.ToLowerInvariant())
                                        {
                                            case ConfigurationKeys.ENABLE:
                                                bool enable;
                                                if (!bool.TryParse(groupChatLogNode.InnerText, out enable))
                                                {
                                                    throw new Exception("error in group chat logs section");
                                                }
                                                configGroup.ChatLogEnabled = enable;
                                                break;
                                            case ConfigurationKeys.FILE:
                                                if (string.IsNullOrEmpty(groupChatLogNode.InnerText))
                                                {
                                                    throw new Exception("error in group chat logs section");
                                                }
                                                configGroup.ChatLog = groupChatLogNode.InnerText;
                                                break;
                                        }
                                    }
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
                                    HashSet<Permissions> permissions = new HashSet<Permissions>();
                                    foreach (XmlNode permissioNode in permissionNodeList)
                                    {
                                        XmlNode node = permissioNode;
                                        object LockObject = new object();
                                        Parallel.ForEach(
                                            wasGetEnumDescriptions<Permissions>()
                                                .AsParallel().Where(name => name.Equals(node.Name,
                                                    StringComparison.Ordinal)), name =>
                                                    {
                                                        bool granted;
                                                        if (!bool.TryParse(node.InnerText, out granted))
                                                        {
                                                            throw new Exception(
                                                                "error in group permission section");
                                                        }
                                                        if (granted)
                                                        {
                                                            lock (LockObject)
                                                            {
                                                                permissions.Add(
                                                                    wasGetEnumValueFromDescription<Permissions>(name));
                                                            }
                                                        }
                                                    });
                                    }
                                    configGroup.Permissions = permissions;
                                    break;
                                case ConfigurationKeys.NOTIFICATIONS:
                                    XmlNodeList notificationNodeList = groupNode.SelectNodes("*");
                                    if (notificationNodeList == null)
                                    {
                                        throw new Exception("error in group notification section");
                                    }
                                    HashSet<Notifications> notifications = new HashSet<Notifications>();
                                    foreach (XmlNode notificationNode in notificationNodeList)
                                    {
                                        XmlNode node = notificationNode;
                                        object LockObject = new object();
                                        Parallel.ForEach(
                                            wasGetEnumDescriptions<Notifications>()
                                                .AsParallel().Where(name => name.Equals(node.Name,
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
                                                            lock (LockObject)
                                                            {
                                                                notifications.Add(
                                                                    wasGetEnumValueFromDescription<Notifications>(name));
                                                            }
                                                        }
                                                    });
                                    }
                                    configGroup.Notifications = notifications;
                                    break;
                            }
                        }
                        Groups.Add(configGroup);
                    }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke(
                        (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
                }

                mainForm.BeginInvoke((Action) (() =>
                {
                    mainForm.StatusText.Text = @"read configuration file";
                    mainForm.StatusProgress.Value = 100;
                }));
            }
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
        ///     Configuration keys.
        /// </summary>
        private struct ConfigurationKeys
        {
            public const string FIRST_NAME = @"firstname";
            public const string LAST_NAME = @"lastname";
            public const string LOGIN_URL = @"loginurl";
            public const string HTTP = @"http";
            public const string PREFIX = @"prefix";
            public const string TIMEOUT = @"timeout";
            public const string THROTTLE = @"throttle";
            public const string SERVICES = @"services";
            public const string TOS_ACCEPTED = @"tosaccepted";
            public const string AUTO_ACTIVATE_GROUP = @"autoactivategroup";
            public const string GROUP_CREATE_FEE = @"groupcreatefee";
            public const string START_LOCATION = @"startlocation";
            public const string LOG = @"log";
            public const string NAME = @"name";
            public const string UUID = @"uuid";
            public const string PASSWORD = @"password";
            public const string CHATLOG = @"chatlog";
            public const string DATABASE = @"database";
            public const string PERMISSIONS = @"permissions";
            public const string NOTIFICATIONS = @"notifications";
            public const string CALLBACKS = @"callbacks";
            public const string QUEUE_LENGTH = @"queuelength";
            public const string CLIENT = @"client";
            public const string NAGGLE = @"naggle";
            public const string CONNECTIONS = @"connections";
            public const string EXPECT100CONTINUE = @"expect100continue";
            public const string MAC = @"MAC";
            public const string ID0 = @"ID0";
            public const string SERVER = @"server";
            public const string MEMBERSHIP = @"membership";
            public const string SWEEP = @"sweep";
            public const string ENABLE = @"enable";
            public const string REBAKE = @"rebake";
            public const string ACTIVATE = @"activate";
            public const string DATA = @"data";
            public const string THREADS = @"threads";
            public const string COMMANDS = @"commands";
            public const string RLV = @"rlv";
            public const string WORKERS = @"workers";
            public const string ENCODE = @"encode";
            public const string DECODE = @"decode";
            public const string ENCRYPT = @"encrypt";
            public const string DECRYPT = @"decrypt";
            public const string INPUT = @"input";
            public const string OUTPUT = @"output";
            public const string ENIGMA = @"enigma";
            public const string ROTORS = @"rotors";
            public const string PLUGS = @"plugs";
            public const string REFLECTOR = @"reflector";
            public const string SECRET = @"secret";
            public const string VIGENERE = @"vigenere";
            public const string IM = @"im";
            public const string RANGE = @"range";
            public const string DECAY = @"decay";
            public const string LOGOUT = @"logout";
            public const string FILE = @"file";
            public const string DIRECTORY = @"directory";
            public const string LOCAL = @"local";
            public const string REGION = @"region";
            public const string BIND = @"bind";
            public const string IDLE = @"idle";
            public const string COMPRESSION = @"compression";
            public const string EXIT_CODE = @"exitcode";
            public const string EXPECTED = @"expected";
            public const string ABNORMAL = @"abnormal";
            public const string KEEP_ALIVE = @"keepalive";
            public const string SCHEDULER = @"scheduler";
            public const string POST = @"post";
            public const string TOTAL = @"total";
            public const string LAND = @"land";
            public const string TASK = @"task";
            public const string TEXTURE = @"texture";
            public const string WIND = @"wind";
            public const string RESEND = @"resend";
            public const string ASSET = @"asset";
            public const string CLOUD = @"cloud";
            public const string DRAIN = @"drain";
            public const string BODY = @"body";
            public const string HEADER = @"header";
            public const string QUEUE = @"queue";
        }

        /// <summary>
        ///     Group structure.
        /// </summary>
        [Serializable]
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

            public uint NotificationMask
            {
                get
                {
                    return Notifications != null && Notifications.Any()
                        ? Notifications.Cast<uint>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }

            public uint PermissionMask
            {
                get
                {
                    return Permissions != null && Permissions.Any()
                        ? Permissions.Cast<uint>()
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

        private void GenerateAESKeyIVRequested(object sender, EventArgs e)
        {
            string readableCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!\"#$%&'()*+,-./:;<=>?@[\\]^_`{}~0123456789";
            Random random = new Random();
            mainForm.BeginInvoke(
                (Action) (() =>
                {
                    mainForm.AESKey.Text = new string(Enumerable.Repeat(readableCharacters, 32)
                        .Select(s => s[random.Next(s.Length)]).ToArray());
                    mainForm.AESIV.Text = new string(Enumerable.Repeat(readableCharacters, 16)
                        .Select(s => s[random.Next(s.Length)]).ToArray());
                }));
        }

        private void AESKeyChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
                {
                    if (string.IsNullOrEmpty(mainForm.AESKey.Text))
                    {
                        mainForm.AESKey.BackColor = Color.MistyRose;
                        return;
                    }
                    byte[] AESKeyBytes = Encoding.UTF8.GetBytes(mainForm.AESKey.Text);
                    switch (AESKeyBytes.Length)
                    {
                        case 16:
                        case 24:
                        case 32:
                            mainForm.AESKey.BackColor = Color.Empty;
                            break;
                        default:
                            mainForm.AESKey.BackColor = Color.MistyRose;
                            break;
                    }
                }));
        }

        private void AESIVChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
                {
                    if (string.IsNullOrEmpty(mainForm.AESIV.Text))
                    {
                        mainForm.AESIV.BackColor = Color.MistyRose;
                        return;
                    }
                    byte[] AESIVBytes = Encoding.UTF8.GetBytes(mainForm.AESIV.Text);
                    switch (AESIVBytes.Length)
                    {
                        case 16:
                            mainForm.AESIV.BackColor = Color.Empty;
                            break;
                        default:
                            mainForm.AESIV.BackColor = Color.MistyRose;
                            break;
                    }
                }));
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    //  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
    ///////////////////////////////////////////////////////////////////////////
    /// <summary>
    ///     An alarm class similar to the UNIX alarm with the added benefit
    ///     of a decaying timer that tracks the time between rescheduling.
    /// </summary>
    /// <remarks>
    ///     (C) Wizardry and Steamworks 2013 - License: GNU GPLv3
    /// </remarks>
    public class wasAdaptiveAlarm : IDisposable
    {
        [Flags]
        public enum DECAY_TYPE
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
            [XmlEnum(Name = "arithmetic")] [Description("arithmetic")] ARITHMETIC = 1,
            [XmlEnum(Name = "geometric")] [Description("geometric")] GEOMETRIC = 2,
            [XmlEnum(Name = "harmonic")] [Description("harmonic")] HARMONIC = 4,
            [XmlEnum(Name = "weighted")] [Description("weighted")] WEIGHTED = 5
        }

        private readonly DECAY_TYPE decay = DECAY_TYPE.NONE;
        private readonly Stopwatch elapsed = new Stopwatch();
        private readonly object LockObject = new object();
        private readonly HashSet<double> times = new HashSet<double>();
        private Timer alarm;

        /// <summary>
        ///     The default constructor using no decay.
        /// </summary>
        public wasAdaptiveAlarm()
        {
            Signal = new ManualResetEvent(false);
        }

        /// <summary>
        ///     The constructor for the wasAdaptiveAlarm class taking as parameter a decay type.
        /// </summary>
        /// <param name="decay">the type of decay: arithmetic, geometric, harmonic, heronian or quadratic</param>
        public wasAdaptiveAlarm(DECAY_TYPE decay)
        {
            Signal = new ManualResetEvent(false);
            this.decay = decay;
        }

        public ManualResetEvent Signal { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Alarm(double deadline)
        {
            lock (LockObject)
            {
                switch (alarm == null)
                {
                    case true:
                        alarm = new Timer(deadline);
                        alarm.Elapsed += (o, p) =>
                        {
                            lock (LockObject)
                            {
                                Signal.Set();
                                elapsed.Stop();
                                times.Clear();
                                alarm = null;
                            }
                        };
                        elapsed.Start();
                        alarm.Start();
                        return;
                    case false:
                        elapsed.Stop();
                        times.Add(elapsed.ElapsedMilliseconds);
                        switch (decay)
                        {
                            case DECAY_TYPE.ARITHMETIC:
                                alarm.Interval = (deadline + times.Aggregate((a, b) => b + a))/(1f + times.Count);
                                break;
                            case DECAY_TYPE.GEOMETRIC:
                                alarm.Interval = Math.Pow(deadline*times.Aggregate((a, b) => b*a),
                                    1f/(1f + times.Count));
                                break;
                            case DECAY_TYPE.HARMONIC:
                                alarm.Interval = (1f + times.Count)/
                                                 (1f/deadline + times.Aggregate((a, b) => 1f/b + 1f/a));
                                break;
                            case DECAY_TYPE.WEIGHTED:
                                HashSet<double> d = new HashSet<double>(times) {deadline};
                                double total = d.Aggregate((a, b) => b + a);
                                alarm.Interval = d.Aggregate((a, b) => Math.Pow(a, 2)/total + Math.Pow(b, 2)/total);
                                break;
                            default:
                                alarm.Interval = deadline;
                                break;
                        }
                        elapsed.Reset();
                        elapsed.Start();
                        break;
                }
            }
        }

        protected virtual void Dispose(bool dispose)
        {
            if (alarm != null)
            {
                alarm.Dispose();
                alarm = null;
            }
        }
    }
}