///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> terrain =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.OrdinalIgnoreCase));
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    if (simulator == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    byte[] data = null;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.GET:
                            ManualResetEventSlim[] DownloadTerrainEvents =
                            {
                                new ManualResetEventSlim(false),
                                new ManualResetEventSlim(false)
                            };
                            EventHandler<InitiateDownloadEventArgs> InitiateDownloadEventHandler =
                                (sender, args) =>
                                {
                                    Client.Assets.RequestAssetXfer(args.SimFileName, false, false, UUID.Zero,
                                        AssetType.Unknown, false);
                                    DownloadTerrainEvents[0].Set();
                                };
                            EventHandler<XferReceivedEventArgs> XferReceivedEventHandler = (sender, args) =>
                            {
                                data = args.Xfer.AssetData;
                                DownloadTerrainEvents[1].Set();
                            };
                            Locks.ClientInstanceAssetsLock.EnterWriteLock();
                            Client.Assets.InitiateDownload += InitiateDownloadEventHandler;
                            Client.Assets.XferReceived += XferReceivedEventHandler;
                            Client.Estate.EstateOwnerMessage("terrain", new List<string>
                            {
                                "download filename",
                                simulator.Name
                            });
                            if (!WaitHandle.WaitAll(DownloadTerrainEvents.Select(o => o.WaitHandle).ToArray(),
                                (int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Assets.InitiateDownload -= InitiateDownloadEventHandler;
                                Client.Assets.XferReceived -= XferReceivedEventHandler;
                                Locks.ClientInstanceAssetsLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_DOWNLOADING_ASSET);
                            }
                            Client.Assets.InitiateDownload -= InitiateDownloadEventHandler;
                            Client.Assets.XferReceived -= XferReceivedEventHandler;
                            Locks.ClientInstanceAssetsLock.ExitWriteLock();
                            if (data == null || !data.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ASSET_DATA);
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                Convert.ToBase64String(data));
                            break;

                        case Enumerations.Action.SET:
                            try
                            {
                                data = Convert.FromBase64String(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            }
                            if (!data.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ASSET_DATA);
                            var AssetUploadEvent = new ManualResetEventSlim(false);
                            EventHandler<AssetUploadEventArgs> AssetUploadEventHandler = (sender, args) =>
                            {
                                if (args.Upload.Transferred.Equals(args.Upload.Size))
                                    AssetUploadEvent.Set();
                            };
                            Locks.ClientInstanceAssetsLock.EnterWriteLock();
                            Client.Assets.UploadProgress += AssetUploadEventHandler;
                            Client.Estate.UploadTerrain(data, simulator.Name);
                            if (!AssetUploadEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Assets.UploadProgress -= AssetUploadEventHandler;
                                Locks.ClientInstanceAssetsLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Client.Assets.UploadProgress -= AssetUploadEventHandler;
                            Locks.ClientInstanceAssetsLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}