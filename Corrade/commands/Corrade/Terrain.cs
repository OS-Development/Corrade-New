using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> terrain = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                string region =
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                        message));
                Simulator simulator =
                    Client.Network.Simulators.FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.InvariantCultureIgnoreCase));
                if (simulator == null)
                {
                    throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                }
                byte[] data = null;
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                        message))
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
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), Convert.ToBase64String(data));
                        break;
                    case Action.SET:
                        try
                        {
                            data = Convert.FromBase64String(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                        message)));
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