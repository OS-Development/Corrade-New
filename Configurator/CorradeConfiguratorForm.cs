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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Configurator
{
    public partial class CorradeConfiguratorForm : Form
    {
        private static readonly object ConfigurationFileLock = new object();
        private static Configuration corradeConfiguration = new Configuration();
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
            foreach (Configuration.Filter filter in corradeConfiguration.InputFilters)
            {
                mainForm.ActiveInputFilters.Items.Add(new ListViewItem
                {
                    Text = Reflection.wasGetNameFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.Items.Clear();
            mainForm.ActiveInputFilters.DisplayMember = "Text";
            foreach (Configuration.Filter filter in corradeConfiguration.OutputFilters)
            {
                mainForm.ActiveOutputFilters.Items.Add(new ListViewItem
                {
                    Text = Reflection.wasGetNameFromEnumValue(filter),
                    Tag = filter
                });
            }
            mainForm.ActiveOutputFilters.DisplayMember = "Text";

            // cryptography
            mainForm.ENIGMARotorSequence.Items.Clear();
            foreach (char rotor in corradeConfiguration.ENIGMAConfiguration.rotors)
            {
                mainForm.ENIGMARotorSequence.Items.Add(new ListViewItem
                {
                    Text = rotor.ToString(),
                    Tag = rotor
                });
            }
            mainForm.ENIGMARotorSequence.DisplayMember = "Text";
            mainForm.ENIGMAPlugSequence.Items.Clear();
            foreach (char plug in corradeConfiguration.ENIGMAConfiguration.plugs)
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
                Reflection.wasGetNameFromEnumValue(corradeConfiguration.HTTPServerCompression);
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
            mainForm.LimitsDataDecay.Text = Reflection.wasGetNameFromEnumValue(corradeConfiguration.DataDecayType);
            mainForm.LimitsMembershipSweep.Text = corradeConfiguration.MembershipSweepInterval.ToString();
            mainForm.LimitsLogoutTimeout.Text = corradeConfiguration.LogoutGrace.ToString();

            // masters
            mainForm.Masters.Items.Clear();
            foreach (Configuration.Master master in corradeConfiguration.Masters)
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
            foreach (Configuration.Group group in corradeConfiguration.Groups)
            {
                mainForm.Groups.Items.Add(new ListViewItem
                {
                    Text = XML.UnescapeXML(group.Name),
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
                mainForm.ActiveInputFilters.Items.Cast<ListViewItem>()
                    .Select(o => (Configuration.Filter) o.Tag)
                    .ToList();
            corradeConfiguration.OutputFilters =
                mainForm.ActiveOutputFilters.Items.Cast<ListViewItem>()
                    .Select(o => (Configuration.Filter) o.Tag)
                    .ToList();

            // cryptography
            corradeConfiguration.ENIGMAConfiguration = new Configuration.ENIGMA
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
                Reflection.wasGetEnumValueFromName<Configuration.HTTPCompressionMethod>(
                    mainForm.HTTPServerCompression.Text);
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
                Reflection.wasGetEnumValueFromName<Time.wasAdaptiveAlarm.DECAY_TYPE>(mainForm.LimitsDataDecay.Text);
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
                                    corradeConfiguration.Load(file, ref corradeConfiguration);
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
                                    corradeConfiguration.Save(file, ref corradeConfiguration);
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
                Configuration.Master master = (Configuration.Master) listViewItem.Tag;
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

                Configuration.Group group = (Configuration.Group) listViewItem.Tag;
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
                          (uint)
                              Reflection.wasGetEnumValueFromName<Configuration.Permissions>(
                                  (string) GroupPermissions.Items[i]))
                            .Equals
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
                          (uint)
                              Reflection.wasGetEnumValueFromName<Configuration.Notifications>(
                                  (string) GroupNotifications.Items[i]))
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
                Configuration.Group group = (Configuration.Group) listViewItem.Tag;
                corradeConfiguration.Groups.Remove(group);

                Configuration.Permissions permission =
                    Reflection.wasGetEnumValueFromName<Configuration.Permissions>(
                        (string) GroupPermissions.Items[e.Index]);

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
                Configuration.Group group = (Configuration.Group) listViewItem.Tag;
                corradeConfiguration.Groups.Remove(group);

                Configuration.Notifications notification =
                    Reflection.wasGetEnumValueFromName<Configuration.Notifications>(
                        (string) GroupNotifications.Items[e.Index]);

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
                Configuration.Group group = (Configuration.Group) listViewItem.Tag;
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

                Configuration.Master master = (Configuration.Master) listViewItem.Tag;

                if (string.IsNullOrEmpty(MasterFirstName.Text) || string.IsNullOrEmpty(MasterLastName.Text))
                {
                    MasterFirstName.BackColor = Color.MistyRose;
                    MasterLastName.BackColor = Color.MistyRose;
                    return;
                }

                MasterFirstName.BackColor = Color.Empty;
                MasterLastName.BackColor = Color.Empty;
                corradeConfiguration.Masters.Remove(master);
                master = new Configuration.Master {FirstName = MasterFirstName.Text, LastName = MasterLastName.Text};
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
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Groups.SelectedItem as ListViewItem;
                if (listViewItem == null)
                    return;

                Configuration.Group group = (Configuration.Group) listViewItem.Tag;

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
                HashSet<Configuration.Permissions> permissions = new HashSet<Configuration.Permissions>();
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    switch (GroupPermissions.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            permissions.Add(
                                Reflection.wasGetEnumValueFromName<Configuration.Permissions>(
                                    (string) GroupPermissions.Items[i]));
                            break;
                    }
                }

                // Notifications
                HashSet<Configuration.Notifications> notifications = new HashSet<Configuration.Notifications>();
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    switch (GroupNotifications.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            notifications.Add(
                                Reflection.wasGetEnumValueFromName<Configuration.Notifications>(
                                    (string) GroupNotifications.Items[i]));
                            break;
                    }
                }


                corradeConfiguration.Groups.Remove(group);

                group = new Configuration.Group
                {
                    Name = XML.EscapeXML(GroupName.Text),
                    UUID = groupUUID,
                    Password = XML.EscapeXML(GroupPassword.Text),
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
                HashSet<Configuration.Permissions> permissions = new HashSet<Configuration.Permissions>();
                for (int i = 0; i < GroupPermissions.Items.Count; ++i)
                {
                    switch (GroupPermissions.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            permissions.Add(
                                Reflection.wasGetEnumValueFromName<Configuration.Permissions>(
                                    (string) GroupPermissions.Items[i]));
                            break;
                    }
                }

                // Notifications
                HashSet<Configuration.Notifications> notifications = new HashSet<Configuration.Notifications>();
                for (int i = 0; i < GroupNotifications.Items.Count; ++i)
                {
                    switch (GroupNotifications.GetItemCheckState(i))
                    {
                        case CheckState.Checked:
                            notifications.Add(
                                Reflection.wasGetEnumValueFromName<Configuration.Notifications>(
                                    (string) GroupNotifications.Items[i]));
                            break;
                    }
                }

                Configuration.Group group = new Configuration.Group
                {
                    Name = XML.EscapeXML(GroupName.Text),
                    UUID = groupUUID,
                    Password = XML.EscapeXML(GroupPassword.Text),
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
                    Tag = Reflection.wasGetEnumValueFromName<Configuration.Filter>(InputDecode.Text)
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
                    Tag = Reflection.wasGetEnumValueFromName<Configuration.Filter>(InputDecryption.Text)
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
                    Tag = Reflection.wasGetEnumValueFromName<Configuration.Filter>(OutputEncrypt.Text)
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
                    Tag = Reflection.wasGetEnumValueFromName<Configuration.Filter>(OutputEncode.Text)
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
                    Tag = new Configuration.Master {FirstName = MasterFirstName.Text, LastName = MasterLastName.Text}
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
            mainForm.BeginInvoke((MethodInvoker) (() =>
            {
                ListViewItem listViewItem = Masters.SelectedItem as ListViewItem;
                if (listViewItem == null)
                {
                    Masters.BackColor = Color.MistyRose;
                    return;
                }
                Masters.BackColor = Color.Empty;
                corradeConfiguration.Masters.Remove(
                    (Configuration.Master) ((ListViewItem) Masters.Items[Masters.SelectedIndex]).Tag);
                Masters.Items.RemoveAt(Masters.SelectedIndex);
            }));
        }

        private void ClearPasswordRequested(object sender, EventArgs e)
        {
            mainForm.BeginInvoke((MethodInvoker) (() => { mainForm.Password.Text = string.Empty; }));
        }

        private void CorradeConfiguratorShown(object sender, EventArgs e)
        {
            mainForm.Version.Text = @"v" + CORRADE_CONSTANTS.CONFIGURATOR_VERSION;
        }

        private void GenerateAESKeyIVRequested(object sender, EventArgs e)
        {
            string readableCharacters =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!\"#$%&'()*+,-./:;<=>?@[\\]^_`{}~0123456789";
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
                (Action) (() =>
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
                (Action) (() =>
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

        public void LoadLegacy(string file, ref Configuration corradeConfiguration)
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
                            if (!uint.TryParse(client.InnerText, out groupCreateFee))
                            {
                                throw new Exception("error in client section");
                            }
                            corradeConfiguration.GroupCreateFee = groupCreateFee;
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
                                        corradeConfiguration.ExitCodeExpected = exitCodeExpected;
                                        break;
                                    case ConfigurationKeys.ABNORMAL:
                                        int exitCodeAbnormal;
                                        if (!int.TryParse(exitCodeNode.InnerText, out exitCodeAbnormal))
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
                            corradeConfiguration.StartLocation = client.InnerText;
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
                                            .wasGetEnumValueFromName<Configuration.Filter>(
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
                                            .wasGetEnumValueFromName<Configuration.Filter>(
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
                            Configuration.ENIGMA enigma = new Configuration.ENIGMA();
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
                            corradeConfiguration.EnableAIML = enable;
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
                            corradeConfiguration.EnableRLV = enable;
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
                            corradeConfiguration.EnableHTTPServer = enableHTTPServer;
                            break;
                        case ConfigurationKeys.PREFIX:
                            if (string.IsNullOrEmpty(serverNode.InnerText))
                            {
                                throw new Exception("error in server section");
                            }
                            corradeConfiguration.HTTPServerPrefix = serverNode.InnerText;
                            break;
                        case ConfigurationKeys.COMPRESSION:
                            corradeConfiguration.HTTPServerCompression =
                                Reflection.wasGetEnumValueFromName<Configuration.HTTPCompressionMethod>(
                                    serverNode.InnerText);
                            break;
                        case ConfigurationKeys.KEEP_ALIVE:
                            bool HTTPKeepAlive;
                            if (!bool.TryParse(serverNode.InnerText, out HTTPKeepAlive))
                            {
                                throw new Exception("error in server section");
                            }
                            corradeConfiguration.HTTPServerKeepAlive = HTTPKeepAlive;
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
                            corradeConfiguration.ThrottleTotal = throttleTotal;
                            break;
                        case ConfigurationKeys.LAND:
                            uint throttleLand;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleLand))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleLand = throttleLand;
                            break;
                        case ConfigurationKeys.TASK:
                            uint throttleTask;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleTask))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleLand = throttleTask;
                            break;
                        case ConfigurationKeys.TEXTURE:
                            uint throttleTexture;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleTexture))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleTexture = throttleTexture;
                            break;
                        case ConfigurationKeys.WIND:
                            uint throttleWind;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleWind))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleWind = throttleWind;
                            break;
                        case ConfigurationKeys.RESEND:
                            uint throttleResend;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleResend))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleResend = throttleResend;
                            break;
                        case ConfigurationKeys.ASSET:
                            uint throttleAsset;
                            if (!uint.TryParse(throttlesNode.InnerText,
                                out throttleAsset))
                            {
                                throw new Exception("error in throttles section");
                            }
                            corradeConfiguration.ThrottleAsset = throttleAsset;
                            break;
                        case ConfigurationKeys.CLOUD:
                            uint throttleCloud;
                            if (!uint.TryParse(throttlesNode.InnerText,
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
                            corradeConfiguration.Range = range;
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
                                        corradeConfiguration.MaximumRLVThreads = maximumRLVThreads;
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
                                        corradeConfiguration.MaximumCommandThreads = maximumCommandThreads;
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
                                        corradeConfiguration.MaximumInstantMessageThreads = maximumInstantMessageThreads;
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
                                        corradeConfiguration.SchedulerExpiration = expiration;
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
                                        corradeConfiguration.MaximumLogThreads = maximumLogThreads;
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
                                        corradeConfiguration.MaximumPOSTThreads = maximumPOSTThreads;
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
                                        corradeConfiguration.ConnectionLimit = connectionLimit;
                                        break;
                                    case ConfigurationKeys.IDLE:
                                        uint connectionIdleTime;
                                        if (
                                            !uint.TryParse(clientLimitNode.InnerText,
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
                                        corradeConfiguration.CallbackTimeout = callbackTimeout;
                                        break;
                                    case ConfigurationKeys.THROTTLE:
                                        uint callbackThrottle;
                                        if (
                                            !uint.TryParse(callbackLimitNode.InnerText, out callbackThrottle))
                                        {
                                            throw new Exception("error in callback limits section");
                                        }
                                        corradeConfiguration.CallbackThrottle = callbackThrottle;
                                        break;
                                    case ConfigurationKeys.QUEUE_LENGTH:
                                        uint callbackQueueLength;
                                        if (
                                            !uint.TryParse(callbackLimitNode.InnerText,
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
                                        corradeConfiguration.NotificationTimeout = notificationTimeout;
                                        break;
                                    case ConfigurationKeys.THROTTLE:
                                        uint notificationThrottle;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText,
                                                out notificationThrottle))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.NotificationThrottle = notificationThrottle;
                                        break;
                                    case ConfigurationKeys.QUEUE_LENGTH:
                                        uint notificationQueueLength;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText,
                                                out notificationQueueLength))
                                        {
                                            throw new Exception("error in notification limits section");
                                        }
                                        corradeConfiguration.NotificationQueueLength = notificationQueueLength;
                                        break;
                                    case ConfigurationKeys.THREADS:
                                        uint maximumNotificationThreads;
                                        if (
                                            !uint.TryParse(notificationLimitNode.InnerText,
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
                                        corradeConfiguration.HTTPServerTimeout = HTTPServerTimeoutValue;
                                        break;
                                    case ConfigurationKeys.DRAIN:
                                        uint HTTPServerDrainTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                out HTTPServerDrainTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerDrainTimeout = HTTPServerDrainTimeoutValue;
                                        break;
                                    case ConfigurationKeys.BODY:
                                        uint HTTPServerBodyTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                out HTTPServerBodyTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerBodyTimeout = HTTPServerBodyTimeoutValue;
                                        break;
                                    case ConfigurationKeys.HEADER:
                                        uint HTTPServerHeaderTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                out HTTPServerHeaderTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerHeaderTimeout = HTTPServerHeaderTimeoutValue;
                                        break;
                                    case ConfigurationKeys.IDLE:
                                        uint HTTPServerIdleTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText,
                                                out HTTPServerIdleTimeoutValue))
                                        {
                                            throw new Exception("error in server limits section");
                                        }
                                        corradeConfiguration.HTTPServerIdleTimeout = HTTPServerIdleTimeoutValue;
                                        break;
                                    case ConfigurationKeys.QUEUE:
                                        uint HTTPServerQueueTimeoutValue;
                                        if (
                                            !uint.TryParse(HTTPServerLimitNode.InnerText,
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
                                        corradeConfiguration.ServicesTimeout = servicesTimeout;
                                        break;
                                    case ConfigurationKeys.REBAKE:
                                        uint rebakeDelay;
                                        if (!uint.TryParse(servicesLimitNode.InnerText, out rebakeDelay))
                                        {
                                            throw new Exception("error in services limits section");
                                        }
                                        corradeConfiguration.RebakeDelay = rebakeDelay;
                                        break;
                                    case ConfigurationKeys.ACTIVATE:
                                        uint activateDelay;
                                        if (
                                            !uint.TryParse(servicesLimitNode.InnerText,
                                                out activateDelay))
                                        {
                                            throw new Exception("error in services limits section");
                                        }
                                        corradeConfiguration.ActivateDelay = activateDelay;
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
                                        corradeConfiguration.DataTimeout = dataTimeout;
                                        break;
                                    case ConfigurationKeys.DECAY:
                                        corradeConfiguration.DataDecayType =
                                            Reflection.wasGetEnumValueFromName<Time.wasAdaptiveAlarm.DECAY_TYPE>(
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
                                        corradeConfiguration.MembershipSweepInterval = membershipSweepInterval;
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
                    (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 66; }));

            // Process masters.
            try
            {
                foreach (XmlNode mastersNode in root.SelectNodes("/config/masters/*"))
                {
                    Configuration.Master configMaster = new Configuration.Master();
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
                    (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action) (() => { mainForm.StatusProgress.Value = 80; }));

            // Process groups.
            try
            {
                foreach (XmlNode groupsNode in root.SelectNodes("/config/groups/*"))
                {
                    Configuration.Group configGroup = new Configuration.Group
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
                                HashSet<Configuration.Permissions> permissions =
                                    new HashSet<Configuration.Permissions>();
                                foreach (XmlNode permissioNode in permissionNodeList)
                                {
                                    XmlNode node = permissioNode;
                                    object LockObject = new object();
                                    Parallel.ForEach(
                                        Reflection.wasGetEnumNames<Configuration.Permissions>()
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
                                                                    .wasGetEnumValueFromName<Configuration.Permissions>(
                                                                        name));
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
                                HashSet<Configuration.Notifications> notifications =
                                    new HashSet<Configuration.Notifications>();
                                foreach (XmlNode notificationNode in notificationNodeList)
                                {
                                    XmlNode node = notificationNode;
                                    object LockObject = new object();
                                    Parallel.ForEach(
                                        Reflection.wasGetEnumNames<Configuration.Notifications>()
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
                                                                    .wasGetEnumValueFromName
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
                    (Action) (() => { mainForm.StatusText.Text = ex.Message; }));
            }

            mainForm.BeginInvoke((Action) (() =>
            {
                mainForm.StatusText.Text = @"read configuration file";
                mainForm.StatusProgress.Value = 100;
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
    }
}