///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;

using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Security;
using System.Windows.Forms;
using System.Xml;
using Configurator.Properties;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;
using wasSharp.Collections.Generic;
using wasSharp.Timers;

namespace Configurator
{
    public partial class CorradeConfiguratorForm : Form
    {
        private static readonly object ConfigurationFileLock = new object();
        private static Configuration corradeConfiguration = new Configuration();
        private static CorradeConfiguratorForm mainForm;
        private static bool isConfigurationSaved;

        private readonly Action GetUserConfiguration = () =>
        {
            // client
            mainForm.Firstname.Text = corradeConfiguration.FirstName;
            mainForm.Lastname.Text = corradeConfiguration.LastName;
            mainForm.Password.Text = corradeConfiguration.Password;
            mainForm.LoginURL.Text = corradeConfiguration.LoginURL;

            // start location
            mainForm.StartLocations.Items.Clear();
            foreach (var location in corradeConfiguration.StartLocations)
            {
                mainForm.StartLocations.Items.Add(new ListViewItem
                {
                    Text = location,
                    Tag = location
                });
            }
            mainForm.StartLocations.DisplayMember = "Text";

            mainForm.TOS.Checked = corradeConfiguration.TOSAccepted;
            mainForm.EnableMultipleSimulators.Checked = corradeConfiguration.EnableMultipleSimulators;
            mainForm.AutoScriptedAgentStatus.Checked = corradeConfiguration.AutoScriptedAgentStatus;
            mainForm.AutoActivateGroup.Checked = corradeConfiguration.AutoActivateGroup;
            mainForm.AutoActivateGroupDelay.Text = corradeConfiguration.AutoActivateGroupDelay.ToString();
            mainForm.AutoPruneCache.Checked = corradeConfiguration.CacheEnableAutoPrune;
            mainForm.AutoPruneCacheInterval.Text = corradeConfiguration.CacheAutoPruneInterval.ToString();

            // language
            mainForm.ClientLanguageAdvertise.Checked = corradeConfiguration.AdvertiseClientLanguage;
            switch (string.IsNullOrEmpty(corradeConfiguration.ClientLanguage))
            {
                case true:
                    mainForm.ClientLanguage.Text = @"en";
                    break;

                default:
                    mainForm.ClientLanguage.Text = corradeConfiguration.ClientLanguage;
                    break;
            }

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
            mainForm.ConferenceMessageLogFile.Text = corradeConfiguration.ConferenceMessageLogDirectory;
            mainForm.ConferenceMessageLogFileEnabled.Checked = corradeConfiguration.ConferenceMessageLogEnabled;

            // filters
            mainForm.ActiveInputFilters.Items.Clear();
            foreach (var filter in corradeConfiguration.InputFilters)
            {
                mainForm.ActiveInputFilters.Items.Add(new ListViewItem
                {
                    Text = Reflection.GetNameFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.Items.Clear();
            mainForm.ActiveInputFilters.DisplayMember = "Text";
            foreach (var filter in corradeConfiguration.OutputFilters)
            {
                mainForm.ActiveOutputFilters.Items.Add(new ListViewItem
                {
                    Text = Reflection.GetNameFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.DisplayMember = "Text";

            // cryptography
            mainForm.ENIGMARotorSequence.Items.Clear();
            foreach (var rotor in corradeConfiguration.ENIGMAConfiguration.rotors)
            {
                mainForm.ENIGMARotorSequence.Items.Add(new ListViewItem
                {
                    Text = rotor.ToString(),
                    Tag = rotor
                });
            }
            mainForm.ENIGMARotorSequence.DisplayMember = "Text";
            mainForm.ENIGMAPlugSequence.Items.Clear();
            foreach (var plug in corradeConfiguration.ENIGMAConfiguration.plugs)
            {
                mainForm.ENIGMAPlugSequence.Items.Add(new ListViewItem
                {
                    Text = plug.ToString(),
                    Tag = plug
                });
            }
            mainForm.ENIGMAPlugSequence.DisplayMember = "Text";
            mainForm.ENIGMAReflector.Text = corradeConfiguration.ENIGMAConfiguration.reflector.ToString();
            mainForm.VIGENERESecret.Text = corradeConfiguration.VIGENERESecret;
            if (!string.IsNullOrEmpty(corradeConfiguration.AESKey))
            {
                mainForm.AESKey.Text = corradeConfiguration.AESKey;
            }

            // SIML
            mainForm.SIMLEnabled.Checked = corradeConfiguration.EnableSIML;
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

            // HTTP server
            mainForm.HTTPServerEnabled.Checked = corradeConfiguration.EnableHTTPServer;
            mainForm.HTTPServerPrefix.Text = corradeConfiguration.HTTPServerPrefix;
            mainForm.HTTPServerAuthenticationEnabled.Checked = corradeConfiguration.EnableHTTPServerAuthentication;
            mainForm.HTTPServerUsername.Text = corradeConfiguration.HTTPServerUsername;
            mainForm.HTTPServerPassword.Text = corradeConfiguration.HTTPServerPassword;

            // Nucleus
            mainForm.NucleusServerEnabled.Checked = corradeConfiguration.EnableNucleusServer;
            mainForm.NucleusServerPrefix.Text = corradeConfiguration.NucleusServerPrefix;
            mainForm.NucleusServerUsername.Text = corradeConfiguration.NucleusServerUsername;
            mainForm.NucleusServerPassword.Text = corradeConfiguration.NucleusServerPassword;
            mainForm.NucleusServerCacheEnabled.Checked = corradeConfiguration.EnableNucleusServerCache;
            mainForm.NucleusServerCachePurgeInterval.Text = corradeConfiguration.NucleusServerCachePurgeInterval.ToString();
            mainForm.NucleusServerNotificationQueueLength.Text = corradeConfiguration.NucleusServerNotificationQueueLength.ToString();

            // Nucleus Server Group.
            mainForm.NucleusServerGroup.Items.Clear();
            foreach (var configuredGroup in corradeConfiguration.Groups)
            {
                var nucleusGroupItem = new ListViewItem
                {
                    Text = XML.UnescapeXML(configuredGroup.Name),
                    Tag = configuredGroup
                };
                mainForm.NucleusServerGroup.Items.Add(nucleusGroupItem);

                // Set the group as selected if it can be found in the configuration.
                if (string.Equals(configuredGroup.Name, corradeConfiguration.NucleusServerGroup))
                {
                    mainForm.NucleusServerGroup.SelectedIndex = mainForm.NucleusServerGroup.Items.IndexOf(nucleusGroupItem);
                    mainForm.NucleusServerGroup.SelectedItem = nucleusGroupItem;
                }
            }
            mainForm.NucleusServerGroup.DisplayMember = "Text";
            // Nucleus Blessed Files.
            mainForm.NucleusServerBlessings.Items.Clear();
            foreach (var location in corradeConfiguration.NucleusServerBlessings)
            {
                mainForm.NucleusServerBlessings.Items.Add(new ListViewItem
                {
                    Text = location,
                    Tag = location
                });
            }
            mainForm.NucleusServerBlessings.DisplayMember = "Text";

            // TCP
            mainForm.TCPNotificationsServerEnabled.Checked = corradeConfiguration.EnableTCPNotificationsServer;
            mainForm.TCPNotificationsServerAddress.Text = corradeConfiguration.TCPNotificationsServerAddress;
            mainForm.TCPNotificationsServerPort.Text = corradeConfiguration.TCPNotificationsServerPort.ToString();
            mainForm.TCPNotificationsServerCertificatePath.Text = corradeConfiguration.TCPNotificationsCertificatePath;
            mainForm.TCPNotificationsServerCertificatePassword.Text =
                corradeConfiguration.TCPNotificationsCertificatePassword;
            switch (string.IsNullOrEmpty(corradeConfiguration.TCPNotificationsSSLProtocol))
            {
                case true:
                    mainForm.TCPNotificationsServerSSLProtocol.Text = Enum.GetName(typeof(SslProtocols),
                        SslProtocols.Tls12);
                    break;

                default:
                    mainForm.TCPNotificationsServerSSLProtocol.Text = corradeConfiguration.TCPNotificationsSSLProtocol;
                    break;
            }

            // limits
            mainForm.LimitsRange.Text = corradeConfiguration.Range.ToString(CultureInfo.DefaultThreadCurrentCulture);
            mainForm.LimitsPOSTThreads.Text = corradeConfiguration.MaximumPOSTThreads.ToString();
            mainForm.LimitsSchedulerExpiration.Text = corradeConfiguration.SchedulerExpiration.ToString();
            mainForm.LimitsLoggingThreads.Text = corradeConfiguration.MaximumLogThreads.ToString();
            mainForm.LimitsCommandsThreads.Text = corradeConfiguration.MaximumCommandThreads.ToString();
            mainForm.LimitsRLVThreads.Text = corradeConfiguration.MaximumRLVThreads.ToString();
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
            mainForm.LimitsServicesTimeout.Text = corradeConfiguration.ServicesTimeout.ToString();
            mainForm.LimitsServicesRebake.Text = corradeConfiguration.RebakeDelay.ToString();
            mainForm.LimitsDataTimeout.Text = corradeConfiguration.DataTimeout.ToString();
            mainForm.LimitsDataDecay.Text = Reflection.GetNameFromEnumValue(corradeConfiguration.DataDecayType);
            mainForm.LimitsMembershipSweep.Text = corradeConfiguration.MembershipSweepInterval.ToString();
            mainForm.LimitsFeedsUpdate.Text = corradeConfiguration.FeedsUpdateInterval.ToString();
            mainForm.LimitsLogoutTimeout.Text = corradeConfiguration.LogoutGrace.ToString();
            mainForm.LimitsHeartbeatLogInterval.Text = corradeConfiguration.HeartbeatLogInterval.ToString();

            // masters
            mainForm.Masters.Items.Clear();
            foreach (var master in corradeConfiguration.Masters)
            {
                mainForm.Masters.Items.Add(new ListViewItem
                {
                    Text = master.FirstName + @" " + master.LastName,
                    Tag = master
                });
            }
            mainForm.Masters.DisplayMember = "Text";
            mainForm.MasterPasswordOverrideEnabled.Checked = corradeConfiguration.EnableMasterPasswordOverride;
            mainForm.MasterPasswordOverride.Text = corradeConfiguration.MasterPasswordOverride;

            // horde peers
            mainForm.HordePeers.Items.Clear();
            foreach (var cachePeer in corradeConfiguration.HordePeers)
            {
                mainForm.HordePeers.Items.Add(new ListViewItem
                {
                    Text = cachePeer.Name,
                    Tag = cachePeer
                });
            }
            mainForm.HordePeers.DisplayMember = "Text";
            mainForm.HordeEnabled.Checked = corradeConfiguration.EnableHorde;

            // groups
            mainForm.Groups.Items.Clear();
            foreach (var group in corradeConfiguration.Groups)
            {
                mainForm.Groups.Items.Add(new ListViewItem
                {
                    Text = XML.UnescapeXML(group.Name),
                    Tag = group
                });
            }
            mainForm.Groups.DisplayMember = "Text";
        };

        private readonly Action SetExperienceLevel = () =>
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   var experienceComboBox = mainForm.ExperienceLevel;
                   if (experienceComboBox == null) return;
                   mainForm.Tabs.Enabled = false;
                   switch ((string)experienceComboBox.SelectedItem)
                   {
                       case "Basic":
                           /* Hide non-basic experience tabs. */
                           mainForm.LogsTabPage.Enabled = false;
                           mainForm.FiltersTabPage.Enabled = false;
                           mainForm.CryptographyTabPage.Enabled = false;
                           mainForm.SIMLTabPage.Enabled = false;
                           mainForm.RLVTabPage.Enabled = false;
                           mainForm.HTTPTabPage.Enabled = false;
                           mainForm.NucleusTabPage.Enabled = false;
                           mainForm.HordeTabPage.Enabled = false;
                           mainForm.TCPTabPage.Enabled = false;
                           mainForm.NetworkTabPage.Enabled = false;
                           mainForm.ThrottlesTabPage.Enabled = false;
                           mainForm.LimitsTabPage.Enabled = false;
                           /* Hide non-basic experience group boxes. */
                           mainForm.AutoActivateGroupBox.Visible = false;
                           mainForm.AutoPruneCacheBox.Visible = false;
                           mainForm.GroupCreateFeeBox.Visible = false;
                           mainForm.ClientIdentificationTagBox.Visible = false;
                           mainForm.ExpectedExitCodeBox.Visible = false;
                           mainForm.AbnormalExitCodeBox.Visible = false;
                           mainForm.AutoSASBox.Visible = false;
                           break;

                       case "Intermediary":
                           /* Hide non-advanced experience tabs. */
                           mainForm.LogsTabPage.Enabled = false;
                           mainForm.FiltersTabPage.Enabled = false;
                           mainForm.CryptographyTabPage.Enabled = false;
                           mainForm.SIMLTabPage.Enabled = true;
                           mainForm.RLVTabPage.Enabled = true;
                           mainForm.HTTPTabPage.Enabled = true;
                           mainForm.NucleusTabPage.Enabled = true;
                           mainForm.HordeTabPage.Enabled = true;
                           mainForm.TCPTabPage.Enabled = false;
                           mainForm.NetworkTabPage.Enabled = false;
                           mainForm.ThrottlesTabPage.Enabled = false;
                           mainForm.LimitsTabPage.Enabled = false;
                           /* Hide non-advanced experience group boxes. */
                           mainForm.AutoActivateGroupBox.Visible = true;
                           mainForm.AutoPruneCacheBox.Visible = true;
                           mainForm.GroupCreateFeeBox.Visible = false;
                           mainForm.ClientIdentificationTagBox.Visible = false;
                           mainForm.ExpectedExitCodeBox.Visible = false;
                           mainForm.AbnormalExitCodeBox.Visible = false;
                           mainForm.AutoSASBox.Visible = false;
                           break;

                       case "Advanced":
                           /* Show everything. */
                           mainForm.LogsTabPage.Enabled = true;
                           mainForm.FiltersTabPage.Enabled = true;
                           mainForm.CryptographyTabPage.Enabled = true;
                           mainForm.SIMLTabPage.Enabled = true;
                           mainForm.RLVTabPage.Enabled = true;
                           mainForm.HTTPTabPage.Enabled = true;
                           mainForm.NucleusTabPage.Enabled = true;
                           mainForm.HordeTabPage.Enabled = true;
                           mainForm.TCPTabPage.Enabled = true;
                           mainForm.NetworkTabPage.Enabled = true;
                           mainForm.ThrottlesTabPage.Enabled = true;
                           mainForm.LimitsTabPage.Enabled = true;
                           /* Show everything. */
                           mainForm.AutoActivateGroupBox.Visible = true;
                           mainForm.AutoPruneCacheBox.Visible = true;
                           mainForm.GroupCreateFeeBox.Visible = true;
                           mainForm.ClientIdentificationTagBox.Visible = true;
                           mainForm.ExpectedExitCodeBox.Visible = true;
                           mainForm.AbnormalExitCodeBox.Visible = true;
                           mainForm.AutoSASBox.Visible = true;
                           break;
                   }
                   mainForm.Tabs.Enabled = true;

                   // Save form settings.
                   Settings.Default["ExperienceLevel"] = (string)experienceComboBox.SelectedItem;
                   Settings.Default.Save();
               }));
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
                    if (mainForm.Password.Text.Length > 16)
                    {
                        corradeConfiguration.Password = "$1$" +
                                                        CalculateMD5Hash(mainForm.Password.Text.Substring(0, 16));
                        break;
                    }
                    corradeConfiguration.Password = "$1$" + CalculateMD5Hash(mainForm.Password.Text);
                    break;
            }
            corradeConfiguration.LoginURL = mainForm.LoginURL.Text;
            // start locations
            corradeConfiguration.StartLocations =
                new List<string>(mainForm.StartLocations.Items.OfType<ListViewItem>().Select(o => o.Tag.ToString()));
            corradeConfiguration.TOSAccepted = mainForm.TOS.Checked;
            corradeConfiguration.EnableMultipleSimulators = mainForm.EnableMultipleSimulators.Checked;
            corradeConfiguration.AutoScriptedAgentStatus = mainForm.AutoScriptedAgentStatus.Checked;
            UUID outUUID;
            if (UUID.TryParse(mainForm.ClientIdentificationTag.Text, out outUUID))
            {
                corradeConfiguration.ClientIdentificationTag = outUUID;
            }
            corradeConfiguration.AutoActivateGroup = mainForm.AutoActivateGroup.Checked;
            uint outUint;
            if (uint.TryParse(mainForm.AutoActivateGroupDelay.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.AutoActivateGroupDelay = outUint;
            }
            corradeConfiguration.CacheEnableAutoPrune = mainForm.AutoPruneCache.Checked;
            double outDouble;
            if (double.TryParse(mainForm.AutoPruneCacheInterval.Text, NumberStyles.Float, Utils.EnUsCulture,
                out outDouble))
            {
                corradeConfiguration.CacheAutoPruneInterval = outDouble;
            }
            if (uint.TryParse(mainForm.GroupCreateFee.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.GroupCreateFee = outUint;
            }
            corradeConfiguration.ExitCodeExpected = (int)mainForm.ExpectedExitCode.Value;
            corradeConfiguration.ExitCodeAbnormal = (int)mainForm.AbnomalExitCode.Value;

            // language
            corradeConfiguration.AdvertiseClientLanguage = mainForm.ClientLanguageAdvertise.Checked;
            switch (mainForm.ClientLanguage.SelectedIndex)
            {
                case -1:
                    corradeConfiguration.ClientLanguage = @"en";
                    break;

                default:
                    corradeConfiguration.ClientLanguage = mainForm.ClientLanguage.Text;
                    break;
            }

            // logs
            corradeConfiguration.ClientLogFile = mainForm.ClientLogFile.Text;
            corradeConfiguration.ClientLogEnabled = mainForm.ClientLogFileEnabled.Checked;
            corradeConfiguration.InstantMessageLogDirectory = mainForm.InstantMessageLogFile.Text;
            corradeConfiguration.InstantMessageLogEnabled = mainForm.InstantMessageLogFileEnabled.Checked;
            corradeConfiguration.LocalMessageLogDirectory = mainForm.LocalLogFile.Text;
            corradeConfiguration.LocalMessageLogEnabled = mainForm.LocalLogFileEnabled.Checked;
            corradeConfiguration.RegionMessageLogDirectory = mainForm.RegionLogFile.Text;
            corradeConfiguration.RegionMessageLogEnabled = mainForm.RegionLogFileEnabled.Checked;
            corradeConfiguration.ConferenceMessageLogDirectory = mainForm.ConferenceMessageLogFile.Text;
            corradeConfiguration.ConferenceMessageLogEnabled = mainForm.ConferenceMessageLogFileEnabled.Checked;

            // filters
            corradeConfiguration.InputFilters =
                mainForm.ActiveInputFilters.Items.OfType<ListViewItem>()
                    .Select(o => (Configuration.Filter)o.Tag)
                    .ToList();
            corradeConfiguration.OutputFilters =
                mainForm.ActiveOutputFilters.Items.OfType<ListViewItem>()
                    .Select(o => (Configuration.Filter)o.Tag)
                    .ToList();

            // cryptography
            corradeConfiguration.ENIGMAConfiguration = new Configuration.ENIGMA
            {
                rotors = mainForm.ENIGMARotorSequence.Items.OfType<ListViewItem>().Select(o => (char)o.Tag).ToArray(),
                plugs = mainForm.ENIGMAPlugSequence.Items.OfType<ListViewItem>().Select(o => (char)o.Tag).ToArray(),
                reflector = mainForm.ENIGMAReflector.Text[0]
            };

            corradeConfiguration.VIGENERESecret = mainForm.VIGENERESecret.Text;

            var AESKeyBytes = Encoding.UTF8.GetBytes(mainForm.AESKey.Text);
            // Only accept FIPS-197 key-lengths
            switch (AESKeyBytes.Length)
            {
                case 16:
                case 24:
                case 32:
                    corradeConfiguration.AESKey = mainForm.AESKey.Text;
                    break;
            }

            // SIML
            corradeConfiguration.EnableSIML = mainForm.SIMLEnabled.Checked;
            // RLV
            corradeConfiguration.EnableRLV = mainForm.RLVEnabled.Checked;

            // network
            corradeConfiguration.BindIPAddress = mainForm.NetworkBindAddress.Text;
            corradeConfiguration.NetworkCardMAC = mainForm.NetworkMACAddress.Text;
            corradeConfiguration.DriveIdentifierHash = mainForm.NetworkID0.Text;
            corradeConfiguration.UseNaggle = mainForm.NetworkNaggleEnabled.Checked;
            corradeConfiguration.UseExpect100Continue = mainForm.NetworkExpect100ContinueEnabled.Checked;

            // throttles
            if (uint.TryParse(mainForm.ThrottlesTotalThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleTotal = outUint;
            }
            if (uint.TryParse(mainForm.ThrottlesResendThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.ThrottleResend = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleLandThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleLand = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleTaskThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleTask = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleTextureThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.ThrottleTexture = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleWindThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleWind = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleAssetThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleAsset = outUint;
            }
            if (uint.TryParse(mainForm.ThrottleCloudThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ThrottleCloud = outUint;
            }

            // HTTP server
            corradeConfiguration.EnableHTTPServer = mainForm.HTTPServerEnabled.Checked;
            corradeConfiguration.HTTPServerPrefix = mainForm.HTTPServerPrefix.Text;
            corradeConfiguration.EnableHTTPServerAuthentication = mainForm.HTTPServerAuthenticationEnabled.Checked;
            corradeConfiguration.HTTPServerUsername = mainForm.HTTPServerUsername.Text;
            // Hash HTTP password.
            switch (Regex.IsMatch(mainForm.HTTPServerPassword.Text, "[a-fA-F0-9]{40}"))
            {
                case false:
                    corradeConfiguration.HTTPServerPassword =
                        string.IsNullOrEmpty(mainForm.HTTPServerPassword.Text)
                            ? mainForm.HTTPServerPassword.Text
                            : Utils.SHA1String(mainForm.HTTPServerPassword.Text);
                    break;
            }

            // Nucleus
            corradeConfiguration.EnableNucleusServer = mainForm.NucleusServerEnabled.Checked;
            corradeConfiguration.NucleusServerPrefix = mainForm.NucleusServerPrefix.Text;
            corradeConfiguration.NucleusServerUsername = mainForm.NucleusServerUsername.Text;
            // Nucleus Server Group
            var selectedNucleusGroupItem = (ListViewItem)mainForm.NucleusServerGroup.SelectedItem;
            if (selectedNucleusGroupItem != null)
            {
                var configurationGroup = (Configuration.Group)selectedNucleusGroupItem.Tag;
                if (configurationGroup != null && !configurationGroup.Equals(default(Configuration.Group)))
                {
                    corradeConfiguration.NucleusServerGroup = configurationGroup.Name;
                }
            }
            // Nucleus Blessed Files
            corradeConfiguration.NucleusServerBlessings =
                new HashSet<string>(mainForm.NucleusServerBlessings.Items.OfType<ListViewItem>().Select(o => o.Tag.ToString()));

            // Hash HTTP password.
            switch (Regex.IsMatch(mainForm.NucleusServerPassword.Text, "[a-fA-F0-9]{40}"))
            {
                case false:
                    corradeConfiguration.NucleusServerPassword =
                        string.IsNullOrEmpty(mainForm.NucleusServerPassword.Text)
                            ? mainForm.HTTPServerPassword.Text
                            : Utils.SHA1String(mainForm.NucleusServerPassword.Text);
                    break;
            }
            corradeConfiguration.EnableNucleusServerCache = mainForm.NucleusServerCacheEnabled.Checked;
            if (uint.TryParse(mainForm.NucleusServerCachePurgeInterval.Text, out outUint))
            {
                corradeConfiguration.NucleusServerCachePurgeInterval = outUint;
            }
            if (uint.TryParse(mainForm.NucleusServerNotificationQueueLength.Text, out outUint))
            {
                corradeConfiguration.NucleusServerNotificationQueueLength = outUint;
            }

            // TCP
            corradeConfiguration.EnableTCPNotificationsServer = mainForm.TCPNotificationsServerEnabled.Checked;
            corradeConfiguration.TCPNotificationsServerAddress = mainForm.TCPNotificationsServerAddress.Text;
            if (uint.TryParse(mainForm.TCPNotificationsServerPort.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.TCPNotificationsServerPort = outUint;
            }
            corradeConfiguration.TCPNotificationsCertificatePath = mainForm.TCPNotificationsServerCertificatePath.Text;
            corradeConfiguration.TCPNotificationsCertificatePassword =
                mainForm.TCPNotificationsServerCertificatePassword.Text;
            switch (string.IsNullOrEmpty(mainForm.TCPNotificationsServerSSLProtocol.Text))
            {
                case true:
                    corradeConfiguration.TCPNotificationsSSLProtocol = Enum.GetName(typeof(SslProtocols),
                        SslProtocols.Tls12);
                    break;

                default:
                    corradeConfiguration.TCPNotificationsSSLProtocol = mainForm.TCPNotificationsServerSSLProtocol.Text;
                    break;
            }

            // limits
            if (uint.TryParse(mainForm.LimitsRange.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.Range = outUint;
            }
            if (uint.TryParse(mainForm.LimitsPOSTThreads.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.MaximumPOSTThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsSchedulerExpiration.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.SchedulerExpiration = outUint;
            }
            if (uint.TryParse(mainForm.LimitsLoggingThreads.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.MaximumLogThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCommandsThreads.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.MaximumCommandThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsRLVThreads.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.MaximumRLVThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsSchedulesResolution.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.SchedulesResolution = outUint;
            }
            if (uint.TryParse(mainForm.LimitsClientConnections.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.ConnectionLimit = outUint;
            }
            if (uint.TryParse(mainForm.LimitsClientIdle.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ConnectionIdleTime = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbacksTimeout.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.CallbackTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbacksThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.CallbackThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsCallbackQueue.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.CallbackQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsTimeout.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.NotificationTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.NotificationThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsQueue.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.NotificationQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsNotificationsThreads.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.MaximumNotificationThreads = outUint;
            }
            if (uint.TryParse(mainForm.LimitsTCPNotificationsQueue.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.TCPNotificationQueueLength = outUint;
            }
            if (uint.TryParse(mainForm.LimitsTCPNotificationsThrottle.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.TCPNotificationThrottle = outUint;
            }
            if (uint.TryParse(mainForm.LimitsServicesTimeout.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.ServicesTimeout = outUint;
            }
            if (uint.TryParse(mainForm.LimitsServicesRebake.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.RebakeDelay = outUint;
            }
            if (uint.TryParse(mainForm.LimitsDataTimeout.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.DataTimeout = outUint;
            }
            corradeConfiguration.DataDecayType =
                Reflection.GetEnumValueFromName<DecayingAlarm.DECAY_TYPE>(mainForm.LimitsDataDecay.Text);
            if (uint.TryParse(mainForm.LimitsMembershipSweep.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.MembershipSweepInterval = outUint;
            }
            if (uint.TryParse(mainForm.LimitsFeedsUpdate.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.FeedsUpdateInterval = outUint;
            }
            if (uint.TryParse(mainForm.LimitsLogoutTimeout.Text, NumberStyles.Integer, Utils.EnUsCulture, out outUint))
            {
                corradeConfiguration.LogoutGrace = outUint;
            }
            if (uint.TryParse(mainForm.LimitsHeartbeatLogInterval.Text, NumberStyles.Integer, Utils.EnUsCulture,
                out outUint))
            {
                corradeConfiguration.HeartbeatLogInterval = outUint;
            }

            // Hash the group passwords using SHA1
            foreach (ListViewItem item in mainForm.Groups.Items)
            {
                var group = (Configuration.Group)item.Tag;
                switch (Regex.IsMatch(group.Password, "[a-fA-F0-9]{40}"))
                {
                    case false:
                        corradeConfiguration.Groups.Remove(group);
                        group.Password = Utils.SHA1String(group.Password);
                        corradeConfiguration.Groups.Add(group);
                        break;
                }
            }

            // Hash the cache peer passwords using SHA1 and trim trailing slashes from URLs
            foreach (ListViewItem item in mainForm.HordePeers.Items)
            {
                var hordePeer = (Configuration.HordePeer)item.Tag;
                switch (Regex.IsMatch(hordePeer.Password, "[a-fA-F0-9]{40}"))
                {
                    case false:
                        corradeConfiguration.HordePeers.Remove(hordePeer);
                        hordePeer.Password = Utils.SHA1String(hordePeer.Password);
                        corradeConfiguration.HordePeers.Add(hordePeer);
                        break;
                }
                switch (!hordePeer.URL[hordePeer.URL.Length - 1].Equals('/'))
                {
                    case false:
                        corradeConfiguration.HordePeers.Remove(hordePeer);
                        hordePeer.URL = hordePeer.URL.TrimEnd('/') + @"/";
                        corradeConfiguration.HordePeers.Add(hordePeer);
                        break;
                }
            }

            // Hash the master password override using SHA1
            corradeConfiguration.EnableMasterPasswordOverride = mainForm.MasterPasswordOverrideEnabled.Checked;
            switch (Regex.IsMatch(mainForm.MasterPasswordOverride.Text, "[a-fA-F0-9]{40}"))
            {
                case false:
                    corradeConfiguration.MasterPasswordOverride =
                        string.IsNullOrEmpty(mainForm.MasterPasswordOverride.Text)
                            ? mainForm.MasterPasswordOverride.Text
                            : Utils.SHA1String(mainForm.MasterPasswordOverride.Text);
                    break;
            }

            corradeConfiguration.EnableHorde = mainForm.HordeEnabled.Checked;
        };

        public CorradeConfiguratorForm()
        {
            InitializeComponent();
            var deselectEventHandler = new EventHandler((sender, args) =>
            {
                // Clear start locations
                StartLocations.ClearSelected();
                StartLocationTextBox.Text = string.Empty;

                // Clear masters
                Masters.ClearSelected();
                MasterFirstName.Text = string.Empty;
                MasterLastName.Text = string.Empty;

                // Clear groups
                Groups.ClearSelected();
                GroupName.Text = string.Empty;
                GroupPassword.Text = string.Empty;
                GroupUUID.Text = string.Empty;
                GroupWorkers.Text = string.Empty;
                GroupSchedules.Text = string.Empty;
                GroupDatabaseFile.Text = string.Empty;
                GroupChatLogEnabled.Checked = false;
                GroupChatLogFile.Text = string.Empty;
                // Permissions
                for (var i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    GroupPermissions.SetItemChecked(i, false);
                }

                // Notifications
                for (var i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    GroupNotifications.SetItemChecked(i, false);
                }

                // Clear horde.
                HordePeers.ClearSelected();
                HordePeerUsername.Text = string.Empty;
                HordePeerPassword.Text = string.Empty;
                HordePeerURL.Text = string.Empty;
                HordePeerName.Text = string.Empty;
                HordePeerSharedSecret.Text = string.Empty;

                // Synchronization
                foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
                {
                    var addCheckBox = dataRow.Cells["Add"] as DataGridViewCheckBoxCell;
                    if (addCheckBox != null)
                    {
                        addCheckBox.Value = 0;
                    }
                    var removeCheckBox = dataRow.Cells["Remove"] as DataGridViewCheckBoxCell;
                    if (removeCheckBox != null)
                    {
                        removeCheckBox.Value = 0;
                    }
                }
            });
            Click += deselectEventHandler;
            AddClickHandlerRecursive<StatusStrip>(this, deselectEventHandler);
            AddClickHandlerRecursive<GroupBox>(this, deselectEventHandler);
            AddClickHandlerRecursive<TabControl>(this, deselectEventHandler);
            AddClickHandlerRecursive<TabPage>(this, deselectEventHandler);
            mainForm = this;
        }

        private void AddClickHandlerRecursive<T>(Control parent, EventHandler handler)
        {
            foreach (Control c in parent.Controls)
            {
                if (c.GetType() == typeof(T))
                {
                    c.Click += handler;
                    continue;
                }
                AddClickHandlerRecursive<T>(c, handler);
            }
        }

        private static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            var md5 = MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private void LoadCorradeLegacyConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               mainForm.LoadLegacyConfigurationDialog.InitialDirectory = Directory.GetCurrentDirectory();
               switch (mainForm.LoadLegacyConfigurationDialog.ShowDialog())
               {
                   case DialogResult.OK:
                       var file = mainForm.LoadLegacyConfigurationDialog.FileName;
                       new Thread(() =>
                       {
                           mainForm.BeginInvoke((MethodInvoker)(() =>
                           {
                               try
                               {
                                   mainForm.StatusText.Text = @"loading legacy configuration...";
                                   mainForm.StatusProgress.Value = 0;
                                   corradeConfiguration = new Configuration();
                                   LoadLegacy(file, ref corradeConfiguration);
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
                       { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
                       break;
               }
           }));
        }

        private void LoadCorradeConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               mainForm.LoadConfigurationDialog.InitialDirectory = Directory.GetCurrentDirectory();
               switch (mainForm.LoadConfigurationDialog.ShowDialog())
               {
                   case DialogResult.OK:
                       var file = mainForm.LoadConfigurationDialog.FileName;
                       new Thread(() =>
                       {
                           mainForm.BeginInvoke((MethodInvoker)(() =>
                           {
                               try
                               {
                                   mainForm.StatusText.Text = @"loading configuration...";
                                   mainForm.StatusProgress.Value = 0;
                                   using (var fileStream = new FileStream(file, FileMode.Open))
                                   {
                                       corradeConfiguration.Load(fileStream, ref corradeConfiguration);
                                   }
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
                       { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
                       break;
               }
           }));
        }

        private void SaveCorradeConfigurationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               mainForm.SaveConfigurationDialog.InitialDirectory = Directory.GetCurrentDirectory();
               switch (mainForm.SaveConfigurationDialog.ShowDialog())
               {
                   case DialogResult.OK:
                       var file = mainForm.SaveConfigurationDialog.FileName;
                       new Thread(() =>
                       {
                           mainForm.BeginInvoke((MethodInvoker)(() =>
                           {
                               try
                               {
                                   mainForm.StatusText.Text = @"applying settings...";
                                   mainForm.StatusProgress.Value = 0;
                                   SetUserConfiguration.Invoke();
                                   mainForm.StatusText.Text = @"saving configuration...";
                                   mainForm.StatusProgress.Value = 50;
                                   using (var fileStream = new FileStream(file, FileMode.Create))
                                   {
                                       corradeConfiguration.Save(fileStream, ref corradeConfiguration);
                                   }
                                   mainForm.StatusText.Text = @"configuration saved";
                                   mainForm.StatusProgress.Value = 100;
                                   isConfigurationSaved = true;
                               }
                               catch (Exception ex)
                               {
                                   mainForm.StatusText.Text = ex.Message;
                               }
                           }));
                       })
                       { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
                       break;
               }
           }));
        }

        private void MasterSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Masters.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var master = (Configuration.Master)listViewItem.Tag;
               MasterFirstName.Text = master.FirstName;
               MasterLastName.Text = master.LastName;
           }));
        }

        private void GroupSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Groups.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;

               var group = (Configuration.Group)listViewItem.Tag;
               GroupName.Text = group.Name;
               GroupPassword.Text = group.Password;
               GroupUUID.Text = group.UUID.ToString();
               GroupWorkers.Text = group.Workers.ToString();
               GroupSchedules.Text = group.Schedules.ToString();
               GroupDatabaseFile.Text = group.DatabaseFile;
               GroupChatLogEnabled.Checked = group.ChatLogEnabled;
               GroupChatLogFile.Text = group.ChatLog;

               // Permissions
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   switch (
                       group.PermissionMask.IsMaskFlagSet(Reflection.GetEnumValueFromName<Configuration.Permissions>(
                           (string)GroupPermissions.Items[i])))
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
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   switch (
                       group.NotificationMask.IsMaskFlagSet(Reflection
                           .GetEnumValueFromName<Configuration.Notifications>(
                               (string)GroupNotifications.Items[i]))
                                                                   /*!(group.NotificationMask &
                                                                     (ulong)
                                                                         Reflection.GetEnumValueFromName<Configuration.Notifications>(
                                                                             (string) GroupNotifications.Items[i]))
                                                                       .Equals(0)*/)
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Groups.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var group = (Configuration.Group)listViewItem.Tag;
               corradeConfiguration.Groups.Remove(group);

               var permission =
                   Reflection.GetEnumValueFromName<Configuration.Permissions>(
                       (string)GroupPermissions.Items[e.Index]);

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
               Groups.Items[Groups.SelectedIndex] = new ListViewItem { Text = group.Name, Tag = group };
           }));
        }

        private void SelectedNotifications(object sender, ItemCheckEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Groups.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var group = (Configuration.Group)listViewItem.Tag;
               corradeConfiguration.Groups.Remove(group);

               var notification =
                   Reflection.GetEnumValueFromName<Configuration.Notifications>(
                       (string)GroupNotifications.Items[e.Index]);

               switch (e.NewValue)
               {
                   case CheckState.Checked: // add notification
                       if (!group.Notifications.Contains(notification))
                           group.Notifications.Add(notification);
                       break;

                   case CheckState.Unchecked: // remove notification
                       if (group.Notifications.Contains(notification))
                           group.Notifications.Remove(notification);
                       break;
               }

               corradeConfiguration.Groups.Add(group);
               Groups.Items[Groups.SelectedIndex] = new ListViewItem { Text = group.Name, Tag = group };
           }));
        }

        private void DeleteGroupRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Groups.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var group = (Configuration.Group)listViewItem.Tag;
               corradeConfiguration.Groups.Remove(group);
               Groups.Items.RemoveAt(Groups.SelectedIndex);

               // Add Nucleus server groups.
               mainForm.NucleusServerGroup.Items.Clear();
               foreach (var configuredGroup in corradeConfiguration.Groups)
               {
                   mainForm.NucleusServerGroup.Items.Add(new ListViewItem
                   {
                       Text = XML.UnescapeXML(configuredGroup.Name),
                       Tag = configuredGroup
                   });
               }
               mainForm.NucleusServerGroup.DisplayMember = "Text";

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
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   GroupPermissions.SetItemChecked(i, false);
               }

               // Notifications
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   GroupNotifications.SetItemChecked(i, false);
               }
           }));
        }

        private void MasterConfigurationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Masters.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;

               var master = (Configuration.Master)listViewItem.Tag;

               if (string.IsNullOrEmpty(MasterFirstName.Text) || string.IsNullOrEmpty(MasterLastName.Text))
               {
                   MasterFirstName.BackColor = Color.MistyRose;
                   MasterLastName.BackColor = Color.MistyRose;
                   return;
               }

               MasterFirstName.BackColor = Color.Empty;
               MasterLastName.BackColor = Color.Empty;
               corradeConfiguration.Masters.Remove(master);
               master = new Configuration.Master { FirstName = MasterFirstName.Text, LastName = MasterLastName.Text };
               corradeConfiguration.Masters.Add(master);
               Masters.Items[Masters.SelectedIndex] = new ListViewItem
               {
                   Text = MasterFirstName.Text + @" " + MasterLastName.Text,
                   Tag = master
               };
           }));
        }

        private void GroupConfigurationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Groups.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;

               var group = (Configuration.Group)listViewItem.Tag;

               if (GroupName.Text.Equals(string.Empty))
               {
                   GroupName.BackColor = Color.MistyRose;
                   return;
               }
               GroupName.BackColor = Color.Empty;

               // Do not accept collisions with the master password override.
               if (GroupPassword.Text.Equals(string.Empty) ||
                  GroupPassword.Text.Equals(MasterPasswordOverride.Text, StringComparison.Ordinal))
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
               if (GroupWorkers.Text.Equals(string.Empty) ||
                   !uint.TryParse(GroupWorkers.Text, NumberStyles.Integer, Utils.EnUsCulture, out groupWorkers))
               {
                   GroupWorkers.BackColor = Color.MistyRose;
                   return;
               }
               GroupWorkers.BackColor = Color.Empty;

               uint groupSchedules;
               if (GroupSchedules.Text.Equals(string.Empty) ||
                   !uint.TryParse(GroupSchedules.Text, NumberStyles.Integer, Utils.EnUsCulture, out groupSchedules))
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
               var permissions = new HashSet<Configuration.Permissions>();
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   switch (GroupPermissions.GetItemCheckState(i))
                   {
                       case CheckState.Checked:
                           permissions.Add(
                               Reflection.GetEnumValueFromName<Configuration.Permissions>(
                                   (string)GroupPermissions.Items[i]));
                           break;
                   }
               }

               // Notifications
               var notifications = new HashSet<Configuration.Notifications>();
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   switch (GroupNotifications.GetItemCheckState(i))
                   {
                       case CheckState.Checked:
                           notifications.Add(
                               Reflection.GetEnumValueFromName<Configuration.Notifications>(
                                   (string)GroupNotifications.Items[i]));
                           break;
                   }
               }

               corradeConfiguration.Groups.Remove(group);

               group = new Configuration.Group
               {
                   Name = GroupName.Text,
                   UUID = groupUUID,
                   Password = GroupPassword.Text,
                   Workers = groupWorkers,
                   Schedules = groupSchedules,
                   DatabaseFile = GroupDatabaseFile.Text,
                   ChatLog = GroupChatLogFile.Text,
                   ChatLogEnabled = GroupChatLogEnabled.Checked,
                   Permissions = permissions,
                   Notifications = notifications
               };

               corradeConfiguration.Groups.Add(group);
               Groups.Items[Groups.SelectedIndex] = new ListViewItem { Text = GroupName.Text, Tag = group };

               // Add Nucleus server groups.
               mainForm.NucleusServerGroup.Items.Clear();
               foreach (var configuredGroup in corradeConfiguration.Groups)
               {
                   mainForm.NucleusServerGroup.Items.Add(new ListViewItem
                   {
                       Text = XML.UnescapeXML(configuredGroup.Name),
                       Tag = configuredGroup
                   });
               }
               mainForm.NucleusServerGroup.DisplayMember = "Text";
           }));
        }

        private void AddGroupRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
               if (GroupWorkers.Text.Equals(string.Empty) ||
                   !uint.TryParse(GroupWorkers.Text, NumberStyles.Integer, Utils.EnUsCulture, out groupWorkers))
               {
                   GroupWorkers.BackColor = Color.MistyRose;
                   return;
               }
               GroupWorkers.BackColor = Color.Empty;

               uint groupSchedules;
               if (GroupSchedules.Text.Equals(string.Empty) ||
                   !uint.TryParse(GroupSchedules.Text, NumberStyles.Integer, Utils.EnUsCulture, out groupSchedules))
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
               var permissions = new HashSet<Configuration.Permissions>();
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   switch (GroupPermissions.GetItemCheckState(i))
                   {
                       case CheckState.Checked:
                           permissions.Add(
                               Reflection.GetEnumValueFromName<Configuration.Permissions>(
                                   (string)GroupPermissions.Items[i]));
                           break;
                   }
               }

               // Notifications
               var notifications = new HashSet<Configuration.Notifications>();
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   switch (GroupNotifications.GetItemCheckState(i))
                   {
                       case CheckState.Checked:
                           notifications.Add(
                               Reflection.GetEnumValueFromName<Configuration.Notifications>(
                                   (string)GroupNotifications.Items[i]));
                           break;
                   }
               }

               var group = new Configuration.Group
               {
                   Name = GroupName.Text,
                   UUID = groupUUID,
                   Password = GroupPassword.Text,
                   Workers = groupWorkers,
                   Schedules = groupSchedules,
                   DatabaseFile = GroupDatabaseFile.Text,
                   ChatLog = GroupChatLogFile.Text,
                   ChatLogEnabled = GroupChatLogEnabled.Checked,
                   Permissions = permissions,
                   Notifications = notifications
               };

               corradeConfiguration.Groups.Add(group);
               Groups.Items.Add(new ListViewItem { Text = GroupName.Text, Tag = group });

               // Add Nucleus server groups.
               mainForm.NucleusServerGroup.Items.Clear();
               foreach (var configuredGroup in corradeConfiguration.Groups)
               {
                   mainForm.NucleusServerGroup.Items.Add(new ListViewItem
                   {
                       Text = XML.UnescapeXML(configuredGroup.Name),
                       Tag = configuredGroup
                   });
               }
               mainForm.NucleusServerGroup.DisplayMember = "Text";
           }));
        }

        private void AddInputDecoderRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
                   Tag = Reflection.GetEnumValueFromName<Configuration.Filter>(InputDecode.Text)
               });
           }));
        }

        private void AddInputDecryptionRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
                   Tag = Reflection.GetEnumValueFromName<Configuration.Filter>(InputDecryption.Text)
               });
           }));
        }

        private void AddOutputEncryptionRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
                   Tag = Reflection.GetEnumValueFromName<Configuration.Filter>(OutputEncrypt.Text)
               });
           }));
        }

        private void AddOutputEncoderRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
                   Tag = Reflection.GetEnumValueFromName<Configuration.Filter>(OutputEncode.Text)
               });
           }));
        }

        private void DeleteSelectedOutputFilterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = ActiveOutputFilters.SelectedItem as ListViewItem;
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = ActiveInputFilters.SelectedItem as ListViewItem;
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = ENIGMARotorSequence.SelectedItem as ListViewItem;
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = ENIGMAPlugSequence.SelectedItem as ListViewItem;
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
            mainForm.BeginInvoke((MethodInvoker)(() =>
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
                   Tag = new Configuration.Master { FirstName = MasterFirstName.Text, LastName = MasterLastName.Text }
               });
               corradeConfiguration.Masters.Add(new Configuration.Master
               {
                   FirstName = MasterFirstName.Text,
                   LastName = MasterLastName.Text
               });
           }));
        }

        private void DeleteMasterRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = Masters.SelectedItem as ListViewItem;
               if (listViewItem == null)
               {
                   Masters.BackColor = Color.MistyRose;
                   return;
               }
               Masters.BackColor = Color.Empty;
               corradeConfiguration.Masters.Remove(
                   (Configuration.Master)((ListViewItem)Masters.Items[Masters.SelectedIndex]).Tag);
               Masters.Items.RemoveAt(Masters.SelectedIndex);
           }));
        }

        private void ClearPasswordRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() => { mainForm.Password.Text = string.Empty; }));
        }

        private void ClearNucleusServerPasswordRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
            {
                mainForm.NucleusServerPassword.Text = string.Empty;
            }));
        }

        private void ClearGroupPasswordRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() => { mainForm.GroupPassword.Text = string.Empty; }));
        }

        private void CorradeConfiguratorShown(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               mainForm.Version.Text = @"v" +
                                       CORRADE_CONSTANTS
                                           .CONFIGURATOR_VERSION;

               // add Horde data synchronization options.
               foreach (var sync in Reflection.GetEnumNames<Configuration.HordeDataSynchronization>())
               {
                   switch (Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(sync))
                   {
                       case Configuration.HordeDataSynchronization.None:
                           break;

                       default:
                           HordeSynchronizationDataGridView.Rows.Add(sync, false, false);
                           break;
                   }
               }

               // add TCP SSL protocols.
               foreach (var protocol in Enum.GetNames(typeof(SslProtocols)))
               {
                   TCPNotificationsServerSSLProtocol.Items.Add(protocol);
               }

               foreach (var language in CultureInfo.GetCultures(CultureTypes.AllCultures).Where(o => !(o.CultureTypes & CultureTypes.UserCustomCulture).Equals(CultureTypes.UserCustomCulture)))
               {
                   mainForm.ClientLanguage.Items.Add(language.TwoLetterISOLanguageName);
               }
           }));

            switch (File.Exists("Corrade.ini"))
            {
                case true:
                    new Thread(() =>
                    {
                        mainForm.BeginInvoke((MethodInvoker)(() =>
                       {
                           try
                           {
                               mainForm.StatusText.Text = @"loading configuration...";
                               mainForm.StatusProgress.Value = 0;
                               using (var fileStream = new FileStream("Corrade.ini", FileMode.Open))
                               {
                                   corradeConfiguration.Load(fileStream, ref corradeConfiguration);
                               }
                               mainForm.StatusProgress.Value = 50;
                               mainForm.StatusText.Text = @"applying settings...";
                               GetUserConfiguration.Invoke();
                               mainForm.StatusText.Text = @"configuration loaded";
                               mainForm.StatusProgress.Value = 100;

                               var experienceLevel = Settings.Default["ExperienceLevel"];
                               mainForm.ExperienceLevel.SelectedIndex =
                                   mainForm.ExperienceLevel.Items.IndexOf(experienceLevel);
                               mainForm.ExperienceLevel.SelectedItem = experienceLevel;
                           }
                           catch (Exception ex)
                           {
                               mainForm.StatusText.Text = ex.Message;
                           }
                       }));
                    })
                    { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
                    break;

                default:
                    // Just load defaults here or people will end up using partially configured bots.
                    LoadDefaults(null, null);
                    break;
            }
        }

        private void GenerateAESKeyIVRequested(object sender, EventArgs e)
        {
            var readableCharacters =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!\"#$%&'()*+,-./:;<=>?@[\\]^_`{}~0123456789";
            var random = new Random();
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   mainForm.AESKey.Text = new string(Enumerable.Repeat(readableCharacters, 32)
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
                   var AESKeyBytes = Encoding.UTF8.GetBytes(mainForm.AESKey.Text);
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

        public void LoadLegacy(string file, ref Configuration corradeConfiguration)
        {
            mainForm.BeginInvoke(
                (Action)(() => { mainForm.StatusText.Text = @"loading configuration"; }));
            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 0; }));

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
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
                return;
            }

            var conf = new XmlDocument();
            try
            {
                conf.LoadXml(file);
            }
            catch (XmlException ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
                return;
            }

            XmlNode root = conf.DocumentElement;
            if (root == null)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = @"invalid configuration file"; }));
                return;
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 6; }));

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
                            corradeConfiguration.FirstName = client.InnerText;
                            break;

                        case ConfigurationKeys.LAST_NAME:
                            if (string.IsNullOrEmpty(client.InnerText))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.LastName = client.InnerText;
                            break;

                        case ConfigurationKeys.PASSWORD:
                            if (string.IsNullOrEmpty(client.InnerText))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.Password = client.InnerText;
                            break;

                        case ConfigurationKeys.LOGIN_URL:
                            if (string.IsNullOrEmpty(client.InnerText))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.LoginURL = client.InnerText;
                            break;

                        case ConfigurationKeys.TOS_ACCEPTED:
                            bool accepted;
                            if (!bool.TryParse(client.InnerText, out accepted))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.TOSAccepted = accepted;
                            break;

                        case ConfigurationKeys.GROUP_CREATE_FEE:
                            uint groupCreateFee;
                            if (
                                !uint.TryParse(client.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                    out groupCreateFee))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.GroupCreateFee = groupCreateFee;
                            break;

                        case ConfigurationKeys.EXIT_CODE:
                            var exitCodeNodeList = client.SelectNodes("*");
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
                                        if (
                                            !int.TryParse(exitCodeNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture, out exitCodeExpected))
                                        {
                                            throw new Exception("error in client section");
                                        }
                                        corradeConfiguration.ExitCodeExpected = exitCodeExpected;
                                        break;

                                    case ConfigurationKeys.ABNORMAL:
                                        int exitCodeAbnormal;
                                        if (
                                            !int.TryParse(exitCodeNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture, out exitCodeAbnormal))
                                        {
                                            throw new Exception("error in client section");
                                        }
                                        corradeConfiguration.ExitCodeAbnormal = exitCodeAbnormal;
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
                            corradeConfiguration.AutoActivateGroup = autoActivateGroup;
                            break;

                        case ConfigurationKeys.START_LOCATION:
                            if (string.IsNullOrEmpty(client.InnerText))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.StartLocations = new List<string>(new[] { client.InnerText });
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 12; }));

            // Process logs.
            try
            {
                foreach (XmlNode LogNode in root.SelectNodes("/config/logs/*"))
                    switch (LogNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.IM:
                            var imLogNodeList = LogNode.SelectNodes("*");
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
                                        corradeConfiguration.InstantMessageLogEnabled = enable;
                                        break;

                                    case ConfigurationKeys.DIRECTORY:
                                        if (string.IsNullOrEmpty(imLogNode.InnerText))
                                        {
                                            throw new Exception("error in im logs section");
                                        }
                                        corradeConfiguration.InstantMessageLogDirectory = imLogNode.InnerText;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.CLIENT:
                            var clientLogNodeList = LogNode.SelectNodes("*");
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
                                        corradeConfiguration.ClientLogEnabled = enable;
                                        break;

                                    case ConfigurationKeys.FILE:
                                        if (string.IsNullOrEmpty(clientLogNode.InnerText))
                                        {
                                            throw new Exception("error in client logs section");
                                        }
                                        corradeConfiguration.ClientLogFile = clientLogNode.InnerText;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.LOCAL:
                            var localLogNodeList = LogNode.SelectNodes("*");
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
                                        corradeConfiguration.LocalMessageLogEnabled = enable;
                                        break;

                                    case ConfigurationKeys.DIRECTORY:
                                        if (string.IsNullOrEmpty(localLogNode.InnerText))
                                        {
                                            throw new Exception("error in local logs section");
                                        }
                                        corradeConfiguration.LocalMessageLogDirectory = localLogNode.InnerText;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.REGION:
                            var regionLogNodeList = LogNode.SelectNodes("*");
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
                                        corradeConfiguration.RegionMessageLogEnabled = enable;
                                        break;

                                    case ConfigurationKeys.DIRECTORY:
                                        if (string.IsNullOrEmpty(regionLogNode.InnerText))
                                        {
                                            throw new Exception("error in local logs section");
                                        }
                                        corradeConfiguration.RegionMessageLogDirectory = regionLogNode.InnerText;
                                        break;
                                }
                            }
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 18; }));

            // Process filters.
            try
            {
                foreach (XmlNode FilterNode in root.SelectNodes("/config/filters/*"))
                    switch (FilterNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.INPUT:
                            var inputFilterNodeList = FilterNode.SelectNodes("*");
                            if (inputFilterNodeList == null)
                            {
                                throw new Exception("error in filters section");
                            }
                            corradeConfiguration.InputFilters = new List<Configuration.Filter>();
                            foreach (XmlNode inputFilterNode in inputFilterNodeList)
                            {
                                switch (inputFilterNode.Name.ToLowerInvariant())
                                {
                                    case ConfigurationKeys.ENCODE:
                                    case ConfigurationKeys.DECODE:
                                    case ConfigurationKeys.ENCRYPT:
                                    case ConfigurationKeys.DECRYPT:
                                        corradeConfiguration.InputFilters.Add(Reflection
                                            .GetEnumValueFromName<Configuration.Filter>(
                                                inputFilterNode.InnerText));
                                        break;

                                    default:
                                        throw new Exception("error in input filters section");
                                }
                            }
                            break;

                        case ConfigurationKeys.OUTPUT:
                            var outputFilterNodeList = FilterNode.SelectNodes("*");
                            if (outputFilterNodeList == null)
                            {
                                throw new Exception("error in filters section");
                            }
                            corradeConfiguration.OutputFilters = new List<Configuration.Filter>();
                            foreach (XmlNode outputFilterNode in outputFilterNodeList)
                            {
                                switch (outputFilterNode.Name.ToLowerInvariant())
                                {
                                    case ConfigurationKeys.ENCODE:
                                    case ConfigurationKeys.DECODE:
                                    case ConfigurationKeys.ENCRYPT:
                                    case ConfigurationKeys.DECRYPT:
                                        corradeConfiguration.OutputFilters.Add(Reflection
                                            .GetEnumValueFromName<Configuration.Filter>(
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
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 24; }));

            // Process cryptography.
            try
            {
                foreach (XmlNode FilterNode in root.SelectNodes("/config/cryptography/*"))
                    switch (FilterNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.ENIGMA:
                            var ENIGMANodeList = FilterNode.SelectNodes("*");
                            if (ENIGMANodeList == null)
                            {
                                throw new Exception("error in cryptography section");
                            }
                            var enigma = new Configuration.ENIGMA();
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
                            corradeConfiguration.ENIGMAConfiguration = enigma;
                            break;

                        case ConfigurationKeys.VIGENERE:
                            var VIGENERENodeList = FilterNode.SelectNodes("*");
                            if (VIGENERENodeList == null)
                            {
                                throw new Exception("error in cryptography section");
                            }
                            foreach (XmlNode VIGENERENode in VIGENERENodeList)
                            {
                                switch (VIGENERENode.Name.ToLowerInvariant())
                                {
                                    case ConfigurationKeys.SECRET:
                                        corradeConfiguration.VIGENERESecret = VIGENERENode.InnerText;
                                        break;
                                }
                            }
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 30; }));

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
                            corradeConfiguration.EnableSIML = enable;
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 36; }));

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
                            corradeConfiguration.EnableRLV = enable;
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 42; }));

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
                            corradeConfiguration.EnableHTTPServer = enableHTTPServer;
                            break;

                        case ConfigurationKeys.PREFIX:
                            if (string.IsNullOrEmpty(serverNode.InnerText))
                            {
                                throw new Exception("error in server section");
                            }
                            corradeConfiguration.HTTPServerPrefix = serverNode.InnerText;
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 48; }));

            // Process network.
            try
            {
                foreach (XmlNode networkNode in root.SelectNodes("/config/network/*"))
                    switch (networkNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.BIND:
                            if (!string.IsNullOrEmpty(networkNode.InnerText))
                            {
                                corradeConfiguration.BindIPAddress = networkNode.InnerText;
                            }
                            break;

                        case ConfigurationKeys.MAC:
                            if (!string.IsNullOrEmpty(networkNode.InnerText))
                            {
                                corradeConfiguration.NetworkCardMAC = networkNode.InnerText;
                            }
                            break;

                        case ConfigurationKeys.ID0:
                            if (!string.IsNullOrEmpty(networkNode.InnerText))
                            {
                                corradeConfiguration.DriveIdentifierHash = networkNode.InnerText;
                            }
                            break;

                        case ConfigurationKeys.NAGGLE:
                            bool useNaggle;
                            if (!bool.TryParse(networkNode.InnerText, out useNaggle))
                            {
                                throw new Exception("error in network section");
                            }
                            corradeConfiguration.UseNaggle = useNaggle;
                            break;

                        case ConfigurationKeys.EXPECT100CONTINUE:
                            bool useExpect100Continue;
                            if (!bool.TryParse(networkNode.InnerText, out useExpect100Continue))
                            {
                                throw new Exception("error in network section");
                            }
                            corradeConfiguration.UseExpect100Continue = useExpect100Continue;
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 54; }));

            // Process throttles
            try
            {
                foreach (XmlNode throttlesNode in root.SelectNodes("/config/throttles/*"))
                    switch (throttlesNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.TOTAL:
                            uint throttleTotal;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleTotal))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleTotal = throttleTotal;
                            break;

                        case ConfigurationKeys.LAND:
                            uint throttleLand;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleLand))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleLand = throttleLand;
                            break;

                        case ConfigurationKeys.TASK:
                            uint throttleTask;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleTask))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleLand = throttleTask;
                            break;

                        case ConfigurationKeys.TEXTURE:
                            uint throttleTexture;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleTexture))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleTexture = throttleTexture;
                            break;

                        case ConfigurationKeys.WIND:
                            uint throttleWind;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleWind))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleWind = throttleWind;
                            break;

                        case ConfigurationKeys.RESEND:
                            uint throttleResend;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleResend))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleResend = throttleResend;
                            break;

                        case ConfigurationKeys.ASSET:
                            uint throttleAsset;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleAsset))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleAsset = throttleAsset;
                            break;

                        case ConfigurationKeys.CLOUD:
                            uint throttleCloud;
                            if (!uint.TryParse(throttlesNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                out throttleCloud))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleCloud = throttleCloud;
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 60; }));

            // Process limits.
            try
            {
                foreach (XmlNode limitsNode in root.SelectNodes("/config/limits/*"))
                    switch (limitsNode.Name.ToLowerInvariant())
                    {
                        case ConfigurationKeys.RANGE:
                            float range;
                            if (!float.TryParse(limitsNode.InnerText, NumberStyles.Float, Utils.EnUsCulture,
                                out range))
                            {
                                throw new Exception("error in range limits section");
                            }
                            corradeConfiguration.Range = range;
                            break;

                        case ConfigurationKeys.RLV:
                            var rlvLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(rlvLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out maximumRLVThreads))
                                        {
                                            throw new Exception("error in RLV limits section");
                                        }
                                        corradeConfiguration.MaximumRLVThreads = maximumRLVThreads;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.COMMANDS:
                            var commandsLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(commandsLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out maximumCommandThreads))
                                        {
                                            throw new Exception("error in commands limits section");
                                        }
                                        corradeConfiguration.MaximumCommandThreads = maximumCommandThreads;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.SCHEDULER:
                            var schedulerLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(schedulerLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out expiration))
                                        {
                                            throw new Exception("error in scheduler limits section");
                                        }
                                        corradeConfiguration.SchedulerExpiration = expiration;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.LOG:
                            var logLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(logLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out maximumLogThreads))
                                        {
                                            throw new Exception("error in log limits section");
                                        }
                                        corradeConfiguration.MaximumLogThreads = maximumLogThreads;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.POST:
                            var postLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(postLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out maximumPOSTThreads))
                                        {
                                            throw new Exception("error in post limits section");
                                        }
                                        corradeConfiguration.MaximumPOSTThreads = maximumPOSTThreads;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.CLIENT:
                            var clientLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(clientLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out connectionLimit))
                                        {
                                            throw new Exception("error in client limits section");
                                        }
                                        corradeConfiguration.ConnectionLimit = connectionLimit;
                                        break;

                                    case ConfigurationKeys.IDLE:
                                        uint connectionIdleTime;
                                        if (
                                            !uint.TryParse(clientLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out connectionIdleTime))
                                        {
                                            throw new Exception("error in client limits section");
                                        }
                                        corradeConfiguration.ConnectionIdleTime = connectionIdleTime;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.CALLBACKS:
                            var callbackLimitNodeList = limitsNode.SelectNodes("*");
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
                                        if (
                                            !uint.TryParse(callbackLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture, out callbackTimeout))
                                        {
                                            throw new Exception("error in callback limits section");
                                        }
                                        corradeConfiguration.CallbackTimeout = callbackTimeout;
                                        break;

                                    case ConfigurationKeys.THROTTLE:
                                        uint callbackThrottle;
                                        if (
                                            !uint.TryParse(callbackLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture, out callbackThrottle))
                                        {
                                            throw new Exception("error in callback limits section");
                                        }
                                        corradeConfiguration.CallbackThrottle = callbackThrottle;
                                        break;

                                    case ConfigurationKeys.QUEUE_LENGTH:
                                        uint callbackQueueLength;
                                        if (
                                            !uint.TryParse(callbackLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out callbackQueueLength))
                                        {
                                            throw new Exception("error in callback limits section");
                                        }
                                        corradeConfiguration.CallbackQueueLength = callbackQueueLength;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.NOTIFICATIONS:
                            var notificationLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(notificationLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out notificationTimeout))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.NotificationTimeout = notificationTimeout;
                                        break;

                                    case ConfigurationKeys.THROTTLE:
                                        uint notificationThrottle;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out notificationThrottle))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.NotificationThrottle = notificationThrottle;
                                        break;

                                    case ConfigurationKeys.QUEUE_LENGTH:
                                        uint notificationQueueLength;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out notificationQueueLength))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.NotificationQueueLength = notificationQueueLength;
                                        break;

                                    case ConfigurationKeys.THREADS:
                                        uint maximumNotificationThreads;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out maximumNotificationThreads))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.MaximumNotificationThreads = maximumNotificationThreads;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.SERVER:
                            var HTTPServerLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(HTTPServerLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out HTTPServerTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerTimeout = HTTPServerTimeoutValue;
                                        break;

                                    case ConfigurationKeys.QUEUE:
                                        uint HTTPServerQueueTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out HTTPServerQueueTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerQueueTimeout = HTTPServerQueueTimeoutValue;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.SERVICES:
                            var servicesLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(servicesLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out servicesTimeout))
                                        {
                                            throw new Exception("error in services limits section");
                                        }
                                        corradeConfiguration.ServicesTimeout = servicesTimeout;
                                        break;

                                    case ConfigurationKeys.REBAKE:
                                        uint rebakeDelay;
                                        if (
                                            !uint.TryParse(servicesLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture, out rebakeDelay))
                                        {
                                            throw new Exception("error in services limits section");
                                        }
                                        corradeConfiguration.RebakeDelay = rebakeDelay;
                                        break;

                                    case ConfigurationKeys.ACTIVATE:
                                        uint activateDelay;
                                        if (
                                            !uint.TryParse(servicesLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out activateDelay))
                                        {
                                            throw new Exception("error in services limits section");
                                        }
                                        corradeConfiguration.AutoActivateGroupDelay = activateDelay;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.DATA:
                            var dataLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(dataLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out dataTimeout))
                                        {
                                            throw new Exception("error in data limits section");
                                        }
                                        corradeConfiguration.DataTimeout = dataTimeout;
                                        break;

                                    case ConfigurationKeys.DECAY:
                                        corradeConfiguration.DataDecayType =
                                            Reflection.GetEnumValueFromName<DecayingAlarm.DECAY_TYPE>(
                                                dataLimitNode.InnerText);
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.MEMBERSHIP:
                            var membershipLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(servicesLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out membershipSweepInterval))
                                        {
                                            throw new Exception("error in membership limits section");
                                        }
                                        corradeConfiguration.MembershipSweepInterval = membershipSweepInterval;
                                        break;
                                }
                            }
                            break;

                        case ConfigurationKeys.LOGOUT:
                            var logoutLimitNodeList = limitsNode.SelectNodes("*");
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
                                            !uint.TryParse(logoutLimitNode.InnerText, NumberStyles.Integer,
                                                Utils.EnUsCulture,
                                                out logoutGrace))
                                        {
                                            throw new Exception("error in logout limits section");
                                        }
                                        corradeConfiguration.LogoutGrace = logoutGrace;
                                        break;
                                }
                            }
                            break;
                    }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 66; }));

            // Process masters.
            try
            {
                foreach (XmlNode mastersNode in root.SelectNodes("/config/masters/*"))
                {
                    var configMaster = new Configuration.Master();
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
                    corradeConfiguration.Masters.Add(configMaster);
                }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() => { mainForm.StatusProgress.Value = 80; }));

            // Process groups.
            try
            {
                foreach (XmlNode groupsNode in root.SelectNodes("/config/groups/*"))
                {
                    var configGroup = new Configuration.Group
                    {
                        ChatLog = string.Empty,
                        ChatLogEnabled = false,
                        DatabaseFile = string.Empty,
                        Name = string.Empty,
                        Notifications = new HashSet<Configuration.Notifications>(),
                        Password = string.Empty,
                        Permissions = new HashSet<Configuration.Permissions>(),
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
                                UUID configGroupUUID;
                                if (!UUID.TryParse(groupNode.InnerText, out configGroupUUID))
                                {
                                    throw new Exception("error in group section");
                                }
                                configGroup.UUID = configGroupUUID;
                                break;

                            case ConfigurationKeys.PASSWORD:
                                if (string.IsNullOrEmpty(groupNode.InnerText))
                                {
                                    throw new Exception("error in group section");
                                }
                                configGroup.Password = groupNode.InnerText;
                                break;

                            case ConfigurationKeys.WORKERS:
                                if (
                                    !uint.TryParse(groupNode.InnerText, NumberStyles.Integer, Utils.EnUsCulture,
                                        out configGroup.Workers))
                                {
                                    throw new Exception("error in group section");
                                }
                                break;

                            case ConfigurationKeys.CHATLOG:
                                var groupChatLogNodeList = groupNode.SelectNodes("*");
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
                                var permissionNodeList = groupNode.SelectNodes("*");
                                if (permissionNodeList == null)
                                {
                                    throw new Exception("error in group permission section");
                                }
                                var permissions =
                                    new HashSet<Configuration.Permissions>();
                                foreach (XmlNode permissioNode in permissionNodeList)
                                {
                                    var node = permissioNode;
                                    var LockObject = new object();
                                    Parallel.ForEach(
                                        Reflection.GetEnumNames<Configuration.Permissions>()
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
                                                                Reflection
                                                                    .GetEnumValueFromName<Configuration.Permissions>(
                                                                        name));
                                                        }
                                                    }
                                                });
                                }
                                configGroup.Permissions = permissions;
                                break;

                            case ConfigurationKeys.NOTIFICATIONS:
                                var notificationNodeList = groupNode.SelectNodes("*");
                                if (notificationNodeList == null)
                                {
                                    throw new Exception("error in group notification section");
                                }
                                var notifications =
                                    new HashSet<Configuration.Notifications>();
                                foreach (XmlNode notificationNode in notificationNodeList)
                                {
                                    var node = notificationNode;
                                    var LockObject = new object();
                                    Parallel.ForEach(
                                        Reflection.GetEnumNames<Configuration.Notifications>()
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
                                                                Reflection
                                                                    .GetEnumValueFromName
                                                                    <Configuration.Notifications>(
                                                                        name));
                                                        }
                                                    }
                                                });
                                }
                                configGroup.Notifications = notifications;
                                break;
                        }
                    }
                    corradeConfiguration.Groups.Add(configGroup);
                }
            }
            catch (Exception ex)
            {
                mainForm.BeginInvoke(
                    (Action)(() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action)(() =>
           {
               mainForm.StatusText.Text = @"read configuration file";
               mainForm.StatusProgress.Value = 100;
           }));
        }

        private void ShowToolTip(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   var pictureBox = sender as PictureBox;
                   if (pictureBox != null)
                   {
                       var help = toolTip1.GetToolTip(pictureBox);
                       if (!string.IsNullOrEmpty(help))
                           toolTip1.Show(help, pictureBox);
                   }
               }));
        }

        private void SaveCheck(object sender, FormClosingEventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   // Save form settings.
                   Settings.Default["ExperienceLevel"] = (string)mainForm.ExperienceLevel.SelectedItem;
                   Settings.Default.Save();
               }));

            // Prompt for saving Corrade configuration.
            switch (isConfigurationSaved)
            {
                case false:
                    if (
                        MessageBox.Show(@"Configuration not saved, are you sure you want to quit?", @"Configurator",
                            MessageBoxButtons.YesNo) == DialogResult.No)
                        e.Cancel = true;
                    break;
            }
        }

        private void LoadDefaults(object sender, EventArgs e)
        {
            new Thread(() =>
            {
                mainForm.BeginInvoke((MethodInvoker)(() =>
               {
                   try
                   {
                       using (
                           var stream =
                               Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream(@"Configurator.Corrade.ini.default"))
                       {
                           mainForm.StatusText.Text = @"loading configuration...";
                           mainForm.StatusProgress.Value = 0;
                           corradeConfiguration.Load(stream, ref corradeConfiguration);
                           mainForm.StatusProgress.Value = 50;
                           mainForm.StatusText.Text = @"applying settings...";
                           GetUserConfiguration.Invoke();

                           var experienceLevel = Settings.Default["ExperienceLevel"];
                           mainForm.ExperienceLevel.SelectedIndex =
                               mainForm.ExperienceLevel.Items.IndexOf(experienceLevel);
                           mainForm.ExperienceLevel.SelectedItem = experienceLevel;

                           mainForm.StatusText.Text = @"configuration loaded";
                           mainForm.StatusProgress.Value = 100;
                       }
                   }
                   catch (Exception ex)
                   {
                       mainForm.StatusText.Text = ex.Message;
                   }
               }));
            })
            { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
        }

        private void ExperienceLevelChanged(object sender, EventArgs e)
        {
            SetExperienceLevel.Invoke();
        }

        private void MasterPasswordOverrideChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   // Check if the master password override is empty.
                   if (string.IsNullOrEmpty(mainForm.MasterPasswordOverride.Text))
                   {
                       mainForm.MasterPasswordOverrideEnabled.Checked = false;
                       mainForm.MasterPasswordOverride.BackColor = Color.MistyRose;
                       return;
                   }

                   // Hash the group passwords using SHA1
                   foreach (ListViewItem item in mainForm.Groups.Items)
                   {
                       var group = (Configuration.Group)item.Tag;
                       if (group.Password.Equals(mainForm.MasterPasswordOverride.Text))
                       {
                           mainForm.MasterPasswordOverrideEnabled.Checked = false;
                           mainForm.MasterPasswordOverride.BackColor = Color.MistyRose;
                           return;
                       }
                   }
                   mainForm.MasterPasswordOverride.BackColor = Color.Empty;
               }));
        }

        private void EnableMasterPasswordOverrideRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   // Check if the master password override is empty.
                   if (string.IsNullOrEmpty(mainForm.MasterPasswordOverride.Text))
                   {
                       mainForm.MasterPasswordOverrideEnabled.Checked = false;
                       mainForm.MasterPasswordOverride.BackColor = Color.MistyRose;
                       return;
                   }

                   // Check if the master password override matches any group password.
                   foreach (ListViewItem item in mainForm.Groups.Items)
                   {
                       var group = (Configuration.Group)item.Tag;
                       if (group.Password.Equals(mainForm.MasterPasswordOverride.Text))
                       {
                           mainForm.MasterPasswordOverrideEnabled.Checked = false;
                           mainForm.MasterPasswordOverride.BackColor = Color.MistyRose;
                           return;
                       }
                   }
                   mainForm.MasterPasswordOverride.BackColor = Color.Empty;
               }));
        }

        private void HordePeerSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = HordePeers.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var hordePeer = (Configuration.HordePeer)listViewItem.Tag;

               // Synchronization
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var data = Reflection
                       .GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                           dataRow.Cells["Data"].Value as string);

                   var addCheckBox = dataRow.Cells["Add"] as DataGridViewCheckBoxCell;

                   if (addCheckBox != null)
                   {
                       addCheckBox.Value = hordePeer.HasDataSynchronizationOption(data,
                           Configuration.HordeDataSynchronizationOption.Add)
                           ? 1
                           : 0;
                   }

                   var removeCheckBox = dataRow.Cells["Remove"] as DataGridViewCheckBoxCell;

                   if (removeCheckBox != null)
                   {
                       removeCheckBox.Value = hordePeer.HasDataSynchronizationOption(data,
                           Configuration.HordeDataSynchronizationOption.Remove)
                           ? 1
                           : 0;
                   }
               }

               HordePeerURL.Text = hordePeer.URL;
               HordePeerName.Text = hordePeer.Name;
               HordePeerUsername.Text = hordePeer.Username;
               HordePeerPassword.Text = hordePeer.Password;
               HordePeerSharedSecret.Text = hordePeer.SharedSecret;
           }));
        }

        private void HordePeerConfigurationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = HordePeers.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;

               var hordePeer = (Configuration.HordePeer)listViewItem.Tag;
               if (string.IsNullOrEmpty(HordePeerUsername.Text) ||
                   string.IsNullOrEmpty(HordePeerPassword.Text) ||
                   string.IsNullOrEmpty(HordePeerURL.Text) ||
                   string.IsNullOrEmpty(HordePeerSharedSecret.Text) ||
                   string.IsNullOrEmpty(HordePeerName.Text) ||
                   corradeConfiguration.HordePeers.AsParallel().Where(o => !o.Equals(hordePeer))
                       .Any(
                           o =>
                               !string.IsNullOrEmpty(o.SharedSecret) &&
                               o.SharedSecret.Equals(HordePeerSharedSecret.Text)))
               {
                   HordePeerUsername.BackColor = Color.MistyRose;
                   HordePeerPassword.BackColor = Color.MistyRose;
                   HordePeerURL.BackColor = Color.MistyRose;
                   HordePeerName.BackColor = Color.MistyRose;
                   HordePeerSharedSecret.BackColor = Color.MistyRose;
                   return;
               }

               HordePeerUsername.BackColor = Color.Empty;
               HordePeerPassword.BackColor = Color.Empty;
               HordePeerURL.BackColor = Color.Empty;
               HordePeerName.BackColor = Color.Empty;
               HordePeerSharedSecret.BackColor = Color.Empty;

               // Synchronization
               var synchronization =
                  new SerializableDictionary
                      <Configuration.HordeDataSynchronization, Configuration.HordeDataSynchronizationOption>();
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var data = Reflection
                       .GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                           dataRow.Cells["Data"].Value as string);

                   var synchronizationOption = Configuration.HordeDataSynchronizationOption.None;

                   switch (Convert.ToBoolean(dataRow.Cells["Add"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;
                   }
                   switch (Convert.ToBoolean(dataRow.Cells["Remove"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;
                   }
                   switch (synchronization.ContainsKey(data))
                   {
                       case true:
                           synchronization[data] = synchronizationOption;
                           break;

                       default:
                           synchronization.Add(data, synchronizationOption);
                           break;
                   }
               }

               corradeConfiguration.HordePeers.Remove(hordePeer);
               hordePeer = new Configuration.HordePeer
               {
                   URL = HordePeerURL.Text,
                   Name = HordePeerName.Text,
                   Username = HordePeerUsername.Text,
                   Password = HordePeerPassword.Text,
                   DataSynchronization = synchronization,
                   SharedSecret = HordePeerSharedSecret.Text
               };
               corradeConfiguration.HordePeers.Add(hordePeer);
               HordePeers.Items[HordePeers.SelectedIndex] = new ListViewItem
               {
                   Text = HordePeerName.Text,
                   Tag = hordePeer
               };
           }));
        }

        private void AddHordePeerRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               if (string.IsNullOrEmpty(HordePeerUsername.Text) ||
                   string.IsNullOrEmpty(HordePeerPassword.Text) ||
                   string.IsNullOrEmpty(HordePeerURL.Text) || string.IsNullOrEmpty(HordePeerSharedSecret.Text) ||
                   string.IsNullOrEmpty(HordePeerName.Text) ||
                   corradeConfiguration.HordePeers.AsParallel()
                       .Any(
                           o =>
                               !string.IsNullOrEmpty(o.SharedSecret) &&
                               o.SharedSecret.Equals(HordePeerSharedSecret.Text)))
               {
                   HordePeerUsername.BackColor = Color.MistyRose;
                   HordePeerPassword.BackColor = Color.MistyRose;
                   HordePeerURL.BackColor = Color.MistyRose;
                   HordePeerName.BackColor = Color.MistyRose;
                   HordePeerSharedSecret.BackColor = Color.MistyRose;
                   return;
               }

               HordePeerUsername.BackColor = Color.Empty;
               HordePeerPassword.BackColor = Color.Empty;
               HordePeerURL.BackColor = Color.Empty;
               HordePeerName.BackColor = Color.Empty;
               HordePeerSharedSecret.BackColor = Color.Empty;

               // Synchronization
               var synchronization =
                  new SerializableDictionary
                      <Configuration.HordeDataSynchronization, Configuration.HordeDataSynchronizationOption>
                      ();
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var data = Reflection
                       .GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                           dataRow.Cells["Data"].Value as string);

                   var synchronizationOption = Configuration.HordeDataSynchronizationOption.None;

                   switch (Convert.ToBoolean(dataRow.Cells["Add"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;
                   }
                   switch (Convert.ToBoolean(dataRow.Cells["Remove"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;
                   }

                   switch (synchronization.ContainsKey(data))
                   {
                       case true:
                           synchronization[data] = synchronizationOption;
                           break;

                       default:
                           synchronization.Add(data, synchronizationOption);
                           break;
                   }
               }

               var hordePeer = new Configuration.HordePeer
               {
                   URL = HordePeerURL.Text,
                   Name = HordePeerName.Text,
                   Username = HordePeerUsername.Text,
                   Password = HordePeerPassword.Text,
                   DataSynchronization = synchronization,
                   SharedSecret = HordePeerSharedSecret.Text
               };

               HordePeers.Items.Add(new ListViewItem
               {
                   Text = HordePeerName.Text,
                   Tag = hordePeer
               });
               corradeConfiguration.HordePeers.Add(hordePeer);
           }));
        }

        private void RemoveHordePeerRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = HordePeers.SelectedItem as ListViewItem;
               if (listViewItem == null)
               {
                   HordePeers.BackColor = Color.MistyRose;
                   return;
               }
               HordePeers.BackColor = Color.Empty;

               // Synchronization
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var addCheckBox = dataRow.Cells["Add"] as DataGridViewCheckBoxCell;
                   if (addCheckBox != null)
                   {
                       addCheckBox.Value = 0;
                   }
                   var removeCheckBox = dataRow.Cells["Remove"] as DataGridViewCheckBoxCell;
                   if (removeCheckBox != null)
                   {
                       removeCheckBox.Value = 0;
                   }
               }

               corradeConfiguration.HordePeers.Remove(
                   (Configuration.HordePeer)((ListViewItem)HordePeers.Items[HordePeers.SelectedIndex]).Tag);
               HordePeers.Items.RemoveAt(HordePeers.SelectedIndex);
           }));
        }

        private void HordePeersClicked(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               if (e.Y < HordePeers.ItemHeight * HordePeers.Items.Count)
                   return;
               HordePeers.ClearSelected();
               HordePeerUsername.Text = string.Empty;
               HordePeerPassword.Text = string.Empty;
               HordePeerURL.Text = string.Empty;
               HordePeerName.Text = string.Empty;
               HordePeerSharedSecret.Text = string.Empty;

               // Synchronization
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var addCheckBox = dataRow.Cells["Add"] as DataGridViewCheckBoxCell;
                   if (addCheckBox != null)
                   {
                       addCheckBox.Value = 0;
                   }
                   var removeCheckBox = dataRow.Cells["Remove"] as DataGridViewCheckBoxCell;
                   if (removeCheckBox != null)
                   {
                       removeCheckBox.Value = 0;
                   }
               }
           }));
        }

        private void MastersClicked(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               if (e.Y < Masters.ItemHeight * Masters.Items.Count)
                   return;
               Masters.ClearSelected();
               MasterFirstName.Text = string.Empty;
               MasterLastName.Text = string.Empty;
           }));
        }

        private void GroupsClicked(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               if (e.Y < Groups.ItemHeight * Groups.Items.Count)
                   return;
               Groups.ClearSelected();
               GroupName.Text = string.Empty;
               GroupPassword.Text = string.Empty;
               GroupUUID.Text = string.Empty;
               GroupWorkers.Text = string.Empty;
               GroupSchedules.Text = string.Empty;
               GroupDatabaseFile.Text = string.Empty;
               GroupChatLogEnabled.Checked = false;
               GroupChatLogFile.Text = string.Empty;
               // Permissions
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   GroupPermissions.SetItemChecked(i, false);
               }

               // Notifications
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   GroupNotifications.SetItemChecked(i, false);
               }
           }));
        }

        private void GenerateHordePeerSharedSecretRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (MethodInvoker)(() => { HordePeerSharedSecret.Text = Membership.GeneratePassword(128, 64); }));
        }

        private void SynchronizationDataChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = HordePeers.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;
               var hordePeer = (Configuration.HordePeer)listViewItem.Tag;
               corradeConfiguration.HordePeers.Remove(hordePeer);

               // Synchronization
               foreach (DataGridViewRow dataRow in HordeSynchronizationDataGridView.Rows)
               {
                   var data = Reflection
                       .GetEnumValueFromName<Configuration.HordeDataSynchronization>(
                           dataRow.Cells["Data"].Value as string);

                   if (!hordePeer.DataSynchronization.ContainsKey(data)) continue;

                   var synchronizationOption = Configuration.HordeDataSynchronizationOption.None;

                   switch (Convert.ToBoolean(dataRow.Cells["Add"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Add);
                           break;
                   }
                   switch (Convert.ToBoolean(dataRow.Cells["Remove"].Value))
                   {
                       case true:
                           BitTwiddling.SetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;

                       default:
                           BitTwiddling.UnsetMaskFlag(ref synchronizationOption,
                               Configuration.HordeDataSynchronizationOption.Remove);
                           break;
                   }
                   hordePeer.DataSynchronization[data] = synchronizationOption;
               }

               corradeConfiguration.HordePeers.Add(hordePeer);
               HordePeers.Items[HordePeers.SelectedIndex] = new ListViewItem { Text = hordePeer.Name, Tag = hordePeer };
           }));
        }

        private void SynchronizationDataClick(object sender, DataGridViewCellEventArgs e)
        {
            HordeSynchronizationDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void LoadTCPNofitifcationsServerCertificateFileRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               mainForm.LoadTCPNotificationsServerCertificateFileDialog.InitialDirectory =
                   Directory.GetCurrentDirectory();
               switch (mainForm.LoadTCPNotificationsServerCertificateFileDialog.ShowDialog())
               {
                   case DialogResult.OK:
                       var file = mainForm.LoadTCPNotificationsServerCertificateFileDialog.FileName;
                       new Thread(() =>
                       {
                           mainForm.BeginInvoke((MethodInvoker)(() =>
                           {
                               try
                               {
                                   mainForm.StatusText.Text = @"loading TCP notifications server certificate...";
                                   mainForm.StatusProgress.Value = 0;
                                   mainForm.TCPNotificationsServerCertificatePath.Text = file;
                                   mainForm.StatusText.Text = @"TCP notifications certificate loaded";
                                   mainForm.TCPNotificationsServerCertificatePath.BackColor = Color.Empty;
                                   mainForm.StatusProgress.Value = 100;
                               }
                               catch (Exception ex)
                               {
                                   mainForm.StatusText.Text = ex.Message;
                               }
                           }));
                       })
                       { IsBackground = true, Priority = ThreadPriority.Normal }.Start();
                       break;
               }
           }));
        }

        private void EnableTCPNotificationsServerRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   if (string.IsNullOrEmpty(mainForm.TCPNotificationsServerAddress.Text))
                   {
                       mainForm.TCPNotificationsServerEnabled.Checked = false;
                       mainForm.TCPNotificationsServerAddress.BackColor = Color.MistyRose;
                       return;
                   }
                   mainForm.TCPNotificationsServerAddress.BackColor = Color.Empty;

                   if (string.IsNullOrEmpty(mainForm.TCPNotificationsServerPort.Text))
                   {
                       mainForm.TCPNotificationsServerEnabled.Checked = false;
                       mainForm.TCPNotificationsServerPort.BackColor = Color.MistyRose;
                       return;
                   }
                   mainForm.TCPNotificationsServerPort.BackColor = Color.Empty;

                   if (string.IsNullOrEmpty(mainForm.TCPNotificationsServerCertificatePath.Text))
                   {
                       mainForm.TCPNotificationsServerEnabled.Checked = false;
                       mainForm.TCPNotificationsServerCertificatePath.BackColor = Color.MistyRose;
                       return;
                   }
                   mainForm.TCPNotificationsServerCertificatePath.BackColor = Color.Empty;
               }));
        }

        private void StartLocationSelected(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
               {
                   var listViewItem = StartLocations.SelectedItem as ListViewItem;
                   if (listViewItem == null)
                       return;
                   var location = listViewItem.Tag.ToString();
                   mainForm.StartLocationTextBox.Text = location;
               }));
        }

        private void StartLocationChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = StartLocations.SelectedItem as ListViewItem;
               if (listViewItem == null)
                   return;

               if (string.IsNullOrEmpty(StartLocationTextBox.Text))
               {
                   StartLocationTextBox.BackColor = Color.MistyRose;
                   return;
               }

               StartLocationTextBox.BackColor = Color.Empty;

               var location = listViewItem.Tag.ToString();
               corradeConfiguration.StartLocations.Remove(location);
               location = StartLocationTextBox.Text;
               corradeConfiguration.StartLocations.Add(location);

               StartLocations.Items[StartLocations.SelectedIndex] = new ListViewItem
               {
                   Text = location,
                   Tag = location
               };
           }));
        }

        private void LocationsClicked(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               if (e.Y < StartLocations.ItemHeight * StartLocations.Items.Count)
                   return;
               StartLocations.ClearSelected();
               StartLocationTextBox.Text = string.Empty;
           }));
        }

        private void AddStartLocationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               // If no location is entered then refuse to continue.
               if (string.IsNullOrEmpty(StartLocationTextBox.Text))
               {
                   StartLocationTextBox.BackColor = Color.MistyRose;
                   return;
               }

               // Check if the start location is properly formatted.
               if (!new wasOpenMetaverse.Helpers.GridLocation(StartLocationTextBox.Text).isValid)
               {
                   StartLocationTextBox.BackColor = Color.MistyRose;
                   return;
               }

               // Add the start location to the list.
               StartLocationTextBox.BackColor = Color.Empty;
               StartLocations.Items.Add(new ListViewItem
               {
                   Text = StartLocationTextBox.Text,
                   Tag = StartLocationTextBox.Text
               });
               corradeConfiguration.StartLocations.Add(StartLocationTextBox.Text);
           }));
        }

        private void DeleteStartLocationRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = StartLocations.SelectedItem as ListViewItem;
               if (listViewItem == null)
               {
                   StartLocations.BackColor = Color.MistyRose;
                   return;
               }
               StartLocations.BackColor = Color.Empty;
               corradeConfiguration.StartLocations.Remove(
                   ((ListViewItem)StartLocations.Items[StartLocations.SelectedIndex]).Tag.ToString());
               StartLocations.Items.RemoveAt(StartLocations.SelectedIndex);
           }));
        }

        private void UpArrowMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   StartLocationsUpArrowButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.up.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void UpArrowMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   StartLocationsUpArrowButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly()
                           .GetManifestResourceStream("Configurator.img.up-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void DownArrowMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   StartLocationsDownArrowButton.Image =
                       new Bitmap(
                           Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.down-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void DownArrowMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   StartLocationsDownArrowButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.down.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void MoveStartLocationUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = StartLocations.SelectedItem as ListViewItem;
               if (listViewItem == null)
               {
                   StartLocations.BackColor = Color.MistyRose;
                   return;
               }
               StartLocations.BackColor = Color.Empty;

               var clickIndex = StartLocations.SelectedIndex;
               if (clickIndex <= 0)
                   return;

               var clickItem = (ListViewItem)StartLocations.Items[clickIndex];
               var aboveItem = (ListViewItem)StartLocations.Items[clickIndex - 1];

               corradeConfiguration.StartLocations[clickIndex - 1] = clickItem.Tag.ToString();
               StartLocations.Items[clickIndex - 1] = clickItem;

               corradeConfiguration.StartLocations[clickIndex] = aboveItem.Tag.ToString();
               StartLocations.Items[clickIndex] = aboveItem;

               StartLocations.SelectedIndex = clickIndex - 1;
           }));
        }

        private void MoveStartLocationDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               var listViewItem = StartLocations.SelectedItem as ListViewItem;
               if (listViewItem == null)
               {
                   StartLocations.BackColor = Color.MistyRose;
                   return;
               }
               StartLocations.BackColor = Color.Empty;

               var clickIndex = StartLocations.SelectedIndex;
               if (clickIndex >= StartLocations.Items.Count - 1)
                   return;

               var clickItem = (ListViewItem)StartLocations.Items[clickIndex];
               var belowItem = (ListViewItem)StartLocations.Items[clickIndex + 1];

               corradeConfiguration.StartLocations[clickIndex + 1] = clickItem.Tag.ToString();
               StartLocations.Items[clickIndex + 1] = clickItem;

               corradeConfiguration.StartLocations[clickIndex] = belowItem.Tag.ToString();
               StartLocations.Items[clickIndex] = belowItem;

               StartLocations.SelectedIndex = clickIndex + 1;
           }));
        }

        private void AllNotificationsRequested(object sender, EventArgs e)
        {
        }

        private void NoneNotificationsRequested(object sender, EventArgs e)
        {
        }

        private void NonePermissionsRequested(object sender, EventArgs e)
        {
        }

        private void AllPermissionsRequested(object sender, EventArgs e)
        {
        }

        private void AllPermissionsMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   AllPermissionsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly()
                           .GetManifestResourceStream("Configurator.img.all-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void AllPermissionsMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   AllPermissionsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.all.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void AllPermissionsRequested(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   GroupPermissions.SetItemChecked(i, true);
               }
           }));
        }

        private void NonePermissionsRequested(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               for (var i = 0; i < GroupPermissions.Items.Count; ++i)
               {
                   GroupPermissions.SetItemChecked(i, false);
               }
           }));
        }

        private void NonePermissionsMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   NonePermissionsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly()
                           .GetManifestResourceStream("Configurator.img.none-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void NonePermissionsMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   NonePermissionsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.none.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void AllNotificationsRequested(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   GroupNotifications.SetItemChecked(i, true);
               }
           }));
        }

        private void AllNotificationsMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   AllNotificationsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly()
                           .GetManifestResourceStream("Configurator.img.all-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void AllNotificationsMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   AllNotificationsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.all.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void NoneNotificationsRequested(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               for (var i = 0; i < GroupNotifications.Items.Count; ++i)
               {
                   GroupNotifications.SetItemChecked(i, false);
               }
           }));
        }

        private void NoneNotificationsMouseDown(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   NoneNotificationsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly()
                           .GetManifestResourceStream("Configurator.img.none-state.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
        }

        private void NoneNotificationsMouseUp(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
           {
               try
               {
                   NoneNotificationsButton.Image =
                       new Bitmap(Assembly.GetEntryAssembly().GetManifestResourceStream("Configurator.img.none.png"));
               }
               catch
               {
                   mainForm.StatusText.Text = @"Could not load arrow resource...";
               }
           }));
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
                TimeSpan.TicksPerDay * Assembly.GetEntryAssembly().GetName().Version.Build + // days since 1 January 2000
                TimeSpan.TicksPerSecond * 2 * Assembly.GetEntryAssembly().GetName().Version.Revision)).ToLongDateString();

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
            ///     Content-types that Corrade can send and receive.
            /// </summary>
            public struct CONTENT_TYPE
            {
                public const string TEXT_PLAIN = @"text/plain";
                public const string WWW_FORM_URLENCODED = @"application/x-www-form-urlencoded";
            }
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

        private void AddNucleusBlessingsRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
            {
                // If no file is entered or the file is already added then refuse to continue.
                if (string.IsNullOrEmpty(NucleusServerBlessingsBox.Text) ||
                    mainForm.NucleusServerBlessings.Items.OfType<ListViewItem>()
                        .Any(o => string.Equals(o.Tag.ToString(), NucleusServerBlessingsBox.Text)))
                {
                    NucleusServerBlessingsBox.BackColor = Color.MistyRose;
                    return;
                }

                // Attempt regex compilation.
                try
                {
                    new Regex(NucleusServerBlessingsBox.Text, RegexOptions.Compiled);
                }
                catch (Exception)
                {
                    NucleusServerBlessingsBox.BackColor = Color.MistyRose;
                    return;
                }

                // Add the blessed file to the list.
                NucleusServerBlessingsBox.BackColor = Color.Empty;
                NucleusServerBlessings.Items.Add(new ListViewItem
                {
                    Text = NucleusServerBlessingsBox.Text,
                    Tag = NucleusServerBlessingsBox.Text
                });
                if (!corradeConfiguration.NucleusServerBlessings.Contains(NucleusServerBlessingsBox.Text))
                {
                    corradeConfiguration.NucleusServerBlessings.Add(NucleusServerBlessingsBox.Text);
                }
                NucleusServerBlessingsBox.Clear();
            }));
        }

        private void DeleteNucleusBlessingsRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
            {
                var listViewItem = NucleusServerBlessings.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    NucleusServerBlessings.BackColor = Color.MistyRose;
                    return;
                }
                NucleusServerBlessings.BackColor = Color.Empty;
                corradeConfiguration.NucleusServerBlessings.Remove(
                    ((ListViewItem)NucleusServerBlessings.Items[NucleusServerBlessings.SelectedIndex]).Tag.ToString());
                NucleusServerBlessings.Items.RemoveAt(NucleusServerBlessings.SelectedIndex);
            }));
        }

        private void NucleusBlessingsClicked(object sender, MouseEventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker)(() =>
            {
                if (e.Y < NucleusServerBlessings.ItemHeight * NucleusServerBlessings.Items.Count)
                    return;
                NucleusServerBlessings.ClearSelected();
                NucleusServerBlessingsBox.Text = string.Empty;
            }));
        }

        private void NucleusBlessingsChanged(object sender, EventArgs e)
        {
            mainForm.BeginInvoke(
                (Action)(() =>
                {
                    var listViewItem = NucleusServerBlessings.SelectedItem as ListViewItem;
                    if (listViewItem == null)
                        return;
                    var file = listViewItem.Tag.ToString();
                    mainForm.NucleusServerBlessingsBox.Text = file;
                }));
        }
    }
}
