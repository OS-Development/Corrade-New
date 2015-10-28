///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> terrain =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator =
                        Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    byte[] data = null;
                    switch (Reflection.wasGetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.GET:
                            ManualResetEvent[] DownloadTerrainEvents =
                            {
                                new ManualResetEvent(false),
                                new ManualResetEvent(false)
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
                            lock (ClientInstanceAssetsLock)
                            {
                                Client.Assets.InitiateDownload += InitiateDownloadEventHandler;
                                Client.Assets.XferReceived += XferReceivedEventHandler;
                                Client.Estate.EstateOwnerMessage("terrain", new List<string>
                                {
                                    "download filename",
                                    simulator.Name
                                });
                                if (!WaitHandle.WaitAll(DownloadTerrainEvents.Select(o => (WaitHandle) o).ToArray(),
                                    (int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Assets.InitiateDownload -= InitiateDownloadEventHandler;
                                    Client.Assets.XferReceived -= XferReceivedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_DOWNLOADING_ASSET);
                                }
                                Client.Assets.InitiateDownload -= InitiateDownloadEventHandler;
                                Client.Assets.XferReceived -= XferReceivedEventHandler;
                            }
                            if (data == null || !data.Any())
                            {
                                throw new ScriptException(ScriptError.EMPTY_ASSET_DATA);
                            }
                            result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA), Convert.ToBase64String(data));
                            break;
                        case Action.SET:
                            try
                            {
                                data = Convert.FromBase64String(
                                    wasInput(
                                        KeyValue.wasKeyValueGet(
                                            wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.INVALID_ASSET_DATA);
                            }
                            if (!data.Any())
                            {
                                throw new ScriptException(ScriptError.EMPTY_ASSET_DATA);
                            }
                            ManualResetEvent AssetUploadEvent = new ManualResetEvent(false);
                            EventHandler<AssetUploadEventArgs> AssetUploadEventHandler = (sender, args) =>
                            {
                                if (args.Upload.Transferred.Equals(args.Upload.Size))
                                {
                                    AssetUploadEvent.Set();
                                }
                            };
                            lock (ClientInstanceAssetsLock)
                            {
                                Client.Assets.UploadProgress += AssetUploadEventHandler;
                                Client.Estate.UploadTerrain(data, simulator.Name);
                                if (!AssetUploadEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Assets.UploadProgress -= AssetUploadEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                                Client.Assets.UploadProgress -= AssetUploadEventHandler;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}