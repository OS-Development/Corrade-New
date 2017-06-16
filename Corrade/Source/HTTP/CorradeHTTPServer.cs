///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using BayesSharp;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Collections.Specialized;
using wasSharp.Web;
using System.Security.Cryptography;
using wasSharpNET.Network.HTTP;
using wasSharpNET.Platform.Windows.Commands.NetSH;
using wasSharpNET.Serialization;
using Reflection = wasSharp.Reflection;
using SHA1 = System.Security.Cryptography.SHA1;
using wasSharpNET.Cryptography;
using wasSharpNET.Diagnostics;

namespace Corrade.HTTP
{
    internal class CorradeHTTPServer : HTTPServer
    {
        public IEnumerable<string> Prefixes { get; private set; }

        public new bool Start(IEnumerable<string> Prefixes)
        {
            this.Prefixes = Prefixes;

            foreach (var prefix in Prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (Utils.GetRunningPlatform().Equals(Utils.Platform.Windows) &&
                    !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName, (int)Corrade.corradeConfiguration.ServicesTimeout);
                    if (!acl.isReserved)
                        acl.Reserve();
                }
            }

            // Start the HTTP server.
            return base.Start(Prefixes);
        }

        public new bool Stop()
        {
            var success = base.Stop();

            foreach (var prefix in Prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (Utils.GetRunningPlatform().Equals(Utils.Platform.Windows) &&
                    !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName, (int)Corrade.corradeConfiguration.ServicesTimeout);
                    if (acl.isReserved)
                        acl.Release();
                }
            }

            // Clear prefixes.
            Prefixes = Enumerable.Empty<string>();
            // Stop the HTTP server.
            return success;
        }

        public override async void ProcessHTTPContext(HttpListenerContext httpContext)
        {
            var httpRequest = httpContext.Request;

            try
            {
                using (var inputMemoryStream = new MemoryStream())
                {
                    var ContentSent = false;
                    switch (httpRequest.HttpMethod)
                    {
                        // Add and remove Horde data.
                        case WebRequestMethods.Http.Get:
                        case WebRequestMethods.Http.Put:
                        case "DELETE":
                            // Microsoft does not consider HTTP DELETE (RFC2616) to be part of the HTTP protocol. Override.
                            using (var HTTPServerResponse = httpContext.Response)
                            {
                                try
                                {
                                    // If authentication is not enabled or the client has not sent any authentication then stop.
                                    if (!Corrade.corradeConfiguration.EnableHTTPServerAuthentication ||
                                        !httpContext.User.Identity.IsAuthenticated)
                                    {
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                                    }

                                    // Authenticate.
                                    var identity = (HttpListenerBasicIdentity)httpContext.User.Identity;
                                    if (
                                        !identity.Name.Equals(Corrade.corradeConfiguration.HTTPServerUsername,
                                            StringComparison.Ordinal) ||
                                        !identity.Password.Equals(Corrade.corradeConfiguration.HTTPServerPassword,
                                            StringComparison.OrdinalIgnoreCase))
                                    {
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                                    }

                                    // Do not proceed if horde synchronization is not enabled.
                                    if (!Corrade.corradeConfiguration.EnableHorde)
                                        throw new HTTPException((int)HttpStatusCode.ServiceUnavailable);

                                    // Get the URL path.
                                    var path =
                                        httpRequest.Url.Segments.Select(o => o.Replace(@"/", ""))
                                            .Where(o => !string.IsNullOrEmpty(o));
                                    if (!path.Any())
                                        throw new HTTPException((int)HttpStatusCode.BadRequest);

                                    // Find peer from shared secret.
                                    string sharedSecretKey;
                                    try
                                    {
                                        sharedSecretKey =
                                            httpRequest.Headers.AllKeys.AsParallel()
                                                .SingleOrDefault(
                                                    o =>
                                                        o.Equals(CORRADE_CONSTANTS.HORDE_SHARED_SECRET_HEADER,
                                                            StringComparison.Ordinal));
                                    }
                                    catch (Exception)
                                    {
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                                    }

                                    if (string.IsNullOrEmpty(sharedSecretKey))
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);

                                    // Find the horde peer.
                                    Configuration.HordePeer hordePeer;
                                    try
                                    {
                                        hordePeer =
                                            Corrade.corradeConfiguration.HordePeers.AsParallel()
                                                .SingleOrDefault(
                                                    o =>
                                                        o.SharedSecret.Equals(
                                                            Encoding.UTF8.GetString(
                                                                Convert.FromBase64String(
                                                                    httpRequest.Headers[sharedSecretKey])),
                                                            StringComparison.Ordinal));
                                    }
                                    catch (Exception)
                                    {
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                                    }

                                    if (hordePeer == null || hordePeer.Equals(default(Configuration.HordePeer)))
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);

                                    // perform decompression in case the client sent compressed data
                                    var requestEncoding = new QValue("identity");
                                    var acceptEncoding = httpRequest.Headers.GetValues("Content-Encoding");
                                    if (acceptEncoding != null && acceptEncoding.Any())
                                    {
                                        var acceptEncodings = new QValueList(acceptEncoding);
                                        if (!acceptEncodings.Equals(default(QValueList)))
                                        {
                                            var preferredEncoding = acceptEncodings.FindPreferred("gzip", "deflate",
                                                "identity");
                                            if (!preferredEncoding.IsEmpty)
                                                requestEncoding = preferredEncoding;
                                        }
                                    }
                                    // retrieve the message sent even if it is a compressed stream.
                                    switch (requestEncoding.Name.ToLowerInvariant())
                                    {
                                        case "gzip":
                                            await httpRequest.InputStream.GZipDecompress(inputMemoryStream);
                                            break;

                                        case "deflate":
                                            await httpRequest.InputStream.DeflateDecompress(inputMemoryStream);
                                            break;

                                        default:
                                            await httpRequest.InputStream.CopyToAsync(inputMemoryStream);
                                            break;
                                    }

                                    // set the content type based on chosen output filers
                                    switch (Corrade.corradeConfiguration.OutputFilters.Last())
                                    {
                                        case Configuration.Filter.RFC1738:
                                            HTTPServerResponse.ContentType =
                                                CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                                            break;

                                        default:
                                            HTTPServerResponse.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                                            break;
                                    }

                                    // Get the first component.
                                    var methodName = path.FirstOrDefault();

                                    // Get all available methods.
                                    var method = GetType()
                                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .AsParallel()
                                        // Consider overloaded methods.
                                        .Where(o =>
                                            o.GetParameters()
                                                .AsParallel()
                                                .Count(p => p.ParameterType == typeof(string))
                                                .Equals(path.Count()))
                                        // Find method name.
                                        .FirstOrDefault(o =>
                                            o.GetCustomAttributes(true)
                                                .AsParallel()
                                                .Any(p =>
                                                    p is HTTPRequestMapping &&
                                                    string.Equals(((HTTPRequestMapping)p).Method,
                                                        httpRequest.HttpMethod.ToUpperInvariant()) &&
                                                    string.Equals(((HTTPRequestMapping)p).Map, methodName)));

                                    using (var dataMemoryStream = new MemoryStream())
                                    {
                                        switch (method != null)
                                        {
                                            case true:
                                                // Get method parameters along with the method name.
                                                var strParams = path.ToArray();

                                                // Convert method parameters to function parameter type and add local parameters.
                                                var @params = method.GetParameters()
                                                    .AsParallel()
                                                    .Where(o => o.ParameterType == typeof(string))
                                                    .Select((p, i) => Convert.ChangeType(strParams[i], p.ParameterType))
                                                    .Concat(new dynamic[]
                                                        {httpRequest.RemoteEndPoint,
                                                            hordePeer,
                                                            HTTPServerResponse,
                                                            inputMemoryStream,
                                                            dataMemoryStream })
                                                    .ToArray();

                                                await Task.Run(() => method.Invoke(this, @params));
                                                break;

                                            default:
                                                throw new HTTPException((int)HttpStatusCode.BadRequest);
                                        }

                                        // Rewind the data memory stream.
                                        dataMemoryStream.Position = 0;

                                        using (var outputStream = new MemoryStream())
                                        {
                                            // perform compression based on the encoding advertised by the client.
                                            var replyEncoding = new QValue(@"identity");
                                            if (acceptEncoding != null && acceptEncoding.Any())
                                            {
                                                var acceptEncodings = new QValueList(acceptEncoding);
                                                if (!acceptEncodings.Equals(default(QValueList)))
                                                {
                                                    var preferredEncoding = acceptEncodings.FindPreferred(@"gzip",
                                                        @"deflate",
                                                        @"identity");
                                                    if (!preferredEncoding.IsEmpty)
                                                        replyEncoding = preferredEncoding;
                                                }
                                            }

                                            switch (replyEncoding.Name.ToLowerInvariant())
                                            {
                                                case "gzip": // gzip compression
                                                    await dataMemoryStream.GZipCompress(outputStream, true);
                                                    HTTPServerResponse.AddHeader(@"Content-Encoding", @"gzip");
                                                    break;

                                                case "deflate": // deflate compression
                                                    await dataMemoryStream.DeflateCompress(outputStream, true);
                                                    HTTPServerResponse.AddHeader(@"Content-Encoding", @"deflate");
                                                    break;

                                                default: // no compression
                                                    HTTPServerResponse.AddHeader(@"Content-Encoding", @"UTF-8");
                                                    await dataMemoryStream.CopyToAsync(outputStream);
                                                    break;
                                            }

                                            // KeepAlive and ChunkedEncoding for HTTP 1.1
                                            switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                            {
                                                case true:
                                                    HTTPServerResponse.ProtocolVersion = HttpVersion.Version11;
                                                    HTTPServerResponse.SendChunked = true;
                                                    HTTPServerResponse.KeepAlive = true;
                                                    break;

                                                default:
                                                    // Set content length.
                                                    HTTPServerResponse.ContentLength64 = outputStream.Length;
                                                    HTTPServerResponse.SendChunked = false;
                                                    HTTPServerResponse.KeepAlive = false;
                                                    break;
                                            }

                                            HTTPServerResponse.StatusCode = (int)HttpStatusCode.OK;

                                            outputStream.Position = 0;
                                            await outputStream.CopyToAsync(HTTPServerResponse.OutputStream).ContinueWith((o) =>
                                            {
                                                ContentSent = true;
                                            });
                                        }
                                    }
                                }
                                catch (HTTPException ex)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        HTTPServerResponse.StatusCode = ex.StatusCode;
                                        HTTPServerResponse.Close();
                                    }
                                    throw;
                                }
                                catch (Exception)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                                        HTTPServerResponse.Close();
                                    }
                                    throw;
                                }
                            }
                            break;
                        // Process commands.
                        case WebRequestMethods.Http.Post:
                            using (var HTTPServerResponse = httpContext.Response)
                            {
                                try
                                {
                                    // perform decompression in case the client sent compressed data
                                    var requestEncoding = new QValue("identity");
                                    var contentEncoding = httpRequest.Headers.GetValues("Content-Encoding");
                                    if (contentEncoding != null && contentEncoding.Any())
                                    {
                                        var acceptEncodings = new QValueList(contentEncoding);
                                        if (!acceptEncodings.Equals(default(QValueList)))
                                        {
                                            var preferredEncoding = acceptEncodings.FindPreferred("gzip", "deflate",
                                                "identity");
                                            if (!preferredEncoding.IsEmpty)
                                                requestEncoding = preferredEncoding;
                                        }
                                    }
                                    // retrieve the message sent even if it is a compressed stream.
                                    switch (requestEncoding.Name.ToLowerInvariant())
                                    {
                                        case "gzip":
                                            await httpRequest.InputStream.GZipDecompress(inputMemoryStream);
                                            break;

                                        case "deflate":
                                            await httpRequest.InputStream.DeflateDecompress(inputMemoryStream);
                                            break;

                                        default:
                                            await httpRequest.InputStream.CopyToAsync(inputMemoryStream);
                                            break;
                                    }

                                    // Get the message.
                                    inputMemoryStream.Position = 0;
                                    var message = Encoding.UTF8.GetString(inputMemoryStream.ToArray());

                                    // ignore empty messages right-away.
                                    if (string.IsNullOrEmpty(message))
                                        throw new HTTPException((int)HttpStatusCode.BadRequest);

                                    var commandGroup = Corrade.GetCorradeGroupFromMessage(message,
                                        Corrade.corradeConfiguration);
                                    // do not process anything from unknown groups.
                                    if (commandGroup == null || commandGroup.Equals(default(Configuration.Group)))
                                        throw new HTTPException((int)HttpStatusCode.Forbidden);

                                    // set the content type based on chosen output filers
                                    switch (Corrade.corradeConfiguration.OutputFilters.Last())
                                    {
                                        case Configuration.Filter.RFC1738:
                                            HTTPServerResponse.ContentType =
                                                CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                                            break;

                                        default:
                                            HTTPServerResponse.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                                            break;
                                    }

                                    // We have the group so schedule the Corrade command though the group scheduler.
                                    var workItem =
                                        Corrade.CorradeThreadPool[Threading.Enumerations.ThreadType.COMMAND].Spawn(
                                            () => Corrade.HandleCorradeCommand(message,
                                                CORRADE_CONSTANTS.WEB_REQUEST,
                                                httpRequest.RemoteEndPoint.ToString(), commandGroup),
                                            Corrade.corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                                            Corrade.corradeConfiguration.SchedulerExpiration);

                                    using (var outputStream = new MemoryStream())
                                    {
                                        // perform compression based on the encoding advertised by the client.
                                        var responseEncoding = new QValue("identity");
                                        if (contentEncoding != null && contentEncoding.Any())
                                        {
                                            var acceptEncodings = new QValueList(contentEncoding);
                                            if (!acceptEncodings.Equals(default(QValueList)))
                                            {
                                                var preferredEncoding = acceptEncodings.FindPreferred("gzip", "deflate",
                                                    "identity");
                                                if (!preferredEncoding.IsEmpty)
                                                    responseEncoding = preferredEncoding;
                                            }
                                        }

                                        var data = Encoding.UTF8.GetBytes(
                                            KeyValue.Encode(
                                                KeyValue.Escape(workItem.GetResult(Timeout.InfiniteTimeSpan, false),
                                                    Corrade.wasOutput)));
                                        // retrieve the message sent even if it is a compressed stream.
                                        switch (responseEncoding.Name.ToLowerInvariant())
                                        {
                                            case "gzip":
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.GZipCompress(outputStream, true);
                                                }
                                                HTTPServerResponse.AddHeader("Content-Encoding", "gzip");
                                                break;

                                            case "deflate":
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.DeflateCompress(outputStream, true);
                                                }
                                                HTTPServerResponse.AddHeader("Content-Encoding", "deflate");
                                                break;

                                            default:
                                                HTTPServerResponse.AddHeader("Content-Encoding", "UTF-8");
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.CopyToAsync(outputStream);
                                                }
                                                break;
                                        }

                                        // KeepAlive and ChunkedEncoding for HTTP 1.1
                                        switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                        {
                                            case true:
                                                HTTPServerResponse.ProtocolVersion = HttpVersion.Version11;
                                                HTTPServerResponse.SendChunked = true;
                                                HTTPServerResponse.KeepAlive = true;
                                                break;

                                            default:
                                                // Set content length.
                                                HTTPServerResponse.ContentLength64 = outputStream.Length;
                                                HTTPServerResponse.SendChunked = false;
                                                HTTPServerResponse.KeepAlive = false;
                                                break;
                                        }

                                        HTTPServerResponse.StatusCode = (int)HttpStatusCode.OK;

                                        outputStream.Position = 0;
                                        await outputStream.CopyToAsync(HTTPServerResponse.OutputStream).ContinueWith((o) =>
                                        {
                                            ContentSent = true;
                                        });
                                    }
                                }
                                catch (HTTPException ex)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        HTTPServerResponse.StatusCode = ex.StatusCode;
                                    }
                                    throw;
                                }
                                catch (Exception)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    }
                                    throw;
                                }
                            }
                            break;
                    }
                }
            }
            catch (HTTPException)
            {
                // Do not report HTTP status errors.
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.HTTP_SERVER_PROCESSING_ABORTED),
                    ex?.PrettyPrint());
            }
        }

        [HTTPRequestMapping("command", "PUT")]
        private void ReceiveHordeGroup(string type, string action, string group, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /command/push/UUID | /command/push/UUID */
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(action))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            if (!string.Equals(action, @"push"))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            // Log the attempt to command data.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                @"command");

            // If this synchronization is not allowed with this peer, then break.
            if (!Corrade.corradeConfiguration.EnableHordeCommand)
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Retrieve the group UUID being pushed.
            UUID groupUUID;
            if (!UUID.TryParse(group, out groupUUID))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            Configuration.Group configurationGroup;
            try
            {
                inputMemoryStream.Position = 0;
                configurationGroup = XmlSerializerCache.Deserialize<Configuration.Group>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    @"command",
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            // Invalid configuration group.
            if (configurationGroup == null || configurationGroup.Equals(default(Configuration.Group)))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Check that this is the group that is being pushed.
            if (!groupUUID.Equals(configurationGroup.UUID))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Search the configuration for the pushed group.
            var configuredGroup = Corrade.corradeConfiguration.Groups.AsParallel()
                .FirstOrDefault(o => o.UUID.Equals(configurationGroup.UUID));
            var corradeConfigurationGroupsModified = false;

            // If the configuration does not contain the group, then add the group.
            if (configuredGroup == null || configuredGroup.Equals(default(Configuration.Group)))
            {
                Corrade.corradeConfiguration.Groups.Add(configurationGroup);
                corradeConfigurationGroupsModified = true;
            }

            // Save the configuration to the configuration file.
            if (corradeConfigurationGroupsModified)
            {
                try
                {
                    var updatedCorradeConfiguration = new Configuration();
                    lock (Corrade.ConfigurationFileLock)
                    {
                        using (
                            var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Create,
                                FileAccess.Write, FileShare.None, 16384, true))
                        {
                            Corrade.corradeConfiguration.Save(fileStream, ref updatedCorradeConfiguration);
                        }
                    }
                    Corrade.corradeConfiguration = updatedCorradeConfiguration;
                }
                catch (Exception)
                {
                    throw new HTTPException((int)HttpStatusCode.InternalServerError);
                }
            }

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                @"command");
        }

        [HTTPRequestMapping("command", "GET")]
        private void RetrieveHordeMetrics(string entity, string action, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /command/metrics | /command/metrics */
            if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(action))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            if (!string.Equals(action, @"metrics"))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            // Log the attempt to command data.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                @"command");

            // If this synchronization is not allowed with this peer, then break.
            if (!Corrade.corradeConfiguration.EnableHordeCommand)
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            try
            {
                XmlSerializerCache.Serialize(new Configuration.HordePeerContext
                {
                    Contribution = Corrade.corradeConfiguration.HordeCommandContribution,
                    Load = 100 * Corrade.GroupWorkers.Values.OfType<int>().Sum() /
                        (int)Corrade.corradeConfiguration.Groups.Sum(o => o.Workers),
                    Name = Corrade.Client.Self.Name,
                    Region = Corrade.Client.Network.CurrentSim.Name,
                    Version = CORRADE_CONSTANTS.CORRADE_VERSION,
                }).Save(outputMemoryStream);
                HTTPServerResponse.ContentType = @"text/xml";

                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                    @"command");
            }
            catch (Exception ex)
            {
                HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;

                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                    @"command",
                    ex?.PrettyPrint());
            }
        }

        [HTTPRequestMapping("cache", "DELETE")]
        private void RemoveCacheSynchronization(string entity, string type, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /cache/{asset}/add | /cache/{asset}/remove */
            // Break if the cache request is incompatible with the cache web resource.
            if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Now attempt to add the asset to the cache.
            switch (dataSynchronizationType)
            {
                case Configuration.HordeDataSynchronization.Region:
                case Configuration.HordeDataSynchronization.Agent:
                case Configuration.HordeDataSynchronization.Group:

                    // If this synchronization option is not allowed with this peer, then break.
                    if (
                        !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                            Configuration.HordeDataSynchronizationOption.Remove))
                        throw new HTTPException((int)HttpStatusCode.Forbidden);

                    try
                    {
                        switch (dataSynchronizationType)
                        {
                            case Configuration.HordeDataSynchronization.Region:
                                inputMemoryStream.Position = 0;
                                var region = XmlSerializerCache.Deserialize<Cache.Region>(inputMemoryStream);

                                Cache.RemoveRegion(region.Name, region.Handle);
                                break;

                            case Configuration.HordeDataSynchronization.Agent:
                                inputMemoryStream.Position = 0;
                                var agent = XmlSerializerCache.Deserialize<Cache.Agent>(inputMemoryStream);

                                Cache.RemoveAgent(agent.FirstName, agent.LastName,
                                    agent.UUID);
                                break;

                            case Configuration.HordeDataSynchronization.Group:
                                inputMemoryStream.Position = 0;
                                var group = XmlSerializerCache.Deserialize<Cache.Group>(inputMemoryStream);

                                Cache.RemoveGroup(group.Name, group.UUID);
                                break;
                        }

                        Corrade.Feedback(
                            CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                            Reflection.GetNameFromEnumValue(dataSynchronizationType));
                    }
                    catch (Exception ex)
                    {
                        HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;

                        Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                            Reflection.GetNameFromEnumValue(dataSynchronizationType),
                            ex?.PrettyPrint());
                    }
                    break;

                default:
                    throw new HTTPException((int)HttpStatusCode.BadRequest);
            }
        }

        [HTTPRequestMapping("cache", "PUT")]
        private void AddCacheSynchronization(string entity, string type, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /cache/{asset}/add | /cache/{asset}/remove */
            // Break if the cache request is incompatible with the cache web resource.
            if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Now attempt to add the asset to the cache.
            switch (dataSynchronizationType)
            {
                case Configuration.HordeDataSynchronization.Region:
                case Configuration.HordeDataSynchronization.Agent:
                case Configuration.HordeDataSynchronization.Group:

                    // If this synchronization option is not allowed with this peer, then break.
                    if (
                        !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                            Configuration.HordeDataSynchronizationOption.Add))
                        throw new HTTPException((int)HttpStatusCode.Forbidden);

                    try
                    {
                        switch (dataSynchronizationType)
                        {
                            case Configuration.HordeDataSynchronization.Region:
                                inputMemoryStream.Position = 0;
                                var region = XmlSerializerCache.Deserialize<Cache.Region>(inputMemoryStream);
                                Cache.UpdateRegion(region.Name, region.Handle);
                                break;

                            case Configuration.HordeDataSynchronization.Agent:
                                inputMemoryStream.Position = 0;
                                var agent = XmlSerializerCache.Deserialize<Cache.Agent>(inputMemoryStream);
                                Cache.AddAgent(agent.FirstName, agent.LastName, agent.UUID);
                                break;

                            case Configuration.HordeDataSynchronization.Group:
                                inputMemoryStream.Position = 0;
                                var group = XmlSerializerCache.Deserialize<Cache.Group>(inputMemoryStream);
                                Cache.AddGroup(group.Name, group.UUID);
                                break;
                        }

                        Corrade.Feedback(
                            CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                            Reflection.GetNameFromEnumValue(dataSynchronizationType));
                    }
                    catch (Exception ex)
                    {
                        HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;

                        Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                            Reflection.GetDescriptionFromEnumValue(
                                Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                            Reflection.GetNameFromEnumValue(dataSynchronizationType),
                            ex?.PrettyPrint());
                    }
                    break;

                default:
                    throw new HTTPException((int)HttpStatusCode.BadRequest);
            }
        }

        [HTTPRequestMapping("cache", "DELETE")]
        private void RemoveCacheSynchronization(string entity, string type, string asset, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /cache/asset/add/UUID | /cache/asset/remove/UUID */
            // Break if the cache request is incompatible with the cache web resource.
            if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(asset))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Remove))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Invalid asset UUID.
            UUID assetUUID;
            if (!UUID.TryParse(asset, out assetUUID))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            try
            {
                Locks.ClientInstanceAssetsLock.EnterWriteLock();
                var hasAsset = Corrade.Client.Assets.Cache.HasAsset(assetUUID);

                if (hasAsset)
                {
                    var fileName = Corrade.Client.Assets.Cache.AssetFileName(assetUUID);
                    File.Delete(Path.Combine(Corrade.Client.Settings.ASSET_CACHE_DIR, fileName));
                    inputMemoryStream.Position = 0;
                    Corrade.HordeDistributeCacheAsset(assetUUID, inputMemoryStream.ToArray(),
                        Configuration.HordeDataSynchronizationOption.Remove);
                }

                Corrade.Feedback(
                    CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint +
                    ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType));
            }
            catch (Exception ex)
            {
                HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;

                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
            }
            finally
            {
                Locks.ClientInstanceAssetsLock.ExitWriteLock();
            }
        }

        [HTTPRequestMapping("cache", "PUT")]
        private void AddCacheSynchronization(string entity, string type, string asset, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /cache/asset/add/UUID | /cache/asset/remove/UUID */
            // Break if the cache request is incompatible with the cache web resource.
            if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(asset))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Add))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Invalid asset UUID.
            UUID assetUUID;
            if (!UUID.TryParse(asset, out assetUUID))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            try
            {
                Locks.ClientInstanceAssetsLock.EnterWriteLock();
                var hasAsset = Corrade.Client.Assets.Cache.HasAsset(assetUUID);

                if (!hasAsset)
                {
                    inputMemoryStream.Position = 0;
                    var requestData = inputMemoryStream.ToArray();
                    Locks.ClientInstanceAssetsLock.EnterWriteLock();
                    Corrade.Client.Assets.Cache.SaveAssetToCache(assetUUID, requestData);
                    Locks.ClientInstanceAssetsLock.ExitWriteLock();
                    Corrade.HordeDistributeCacheAsset(assetUUID, requestData,
                        Configuration.HordeDataSynchronizationOption.Add);
                }

                Corrade.Feedback(
                    CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint +
                    ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType));
            }
            catch (Exception ex)
            {
                HTTPServerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;

                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_STORE_PEER_CACHE_ENTITY),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
            }
            finally
            {
                Locks.ClientInstanceAssetsLock.ExitWriteLock();
            }
        }

        [HTTPRequestMapping("mute", "DELETE")]
        private void RemoveMuteSynchronization(string type, IPEndPoint endPoint, Configuration.HordePeer hordePeer,
            HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            // Break if the mute request is incompatible with the mute web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Remove))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            MuteEntry mute;
            try
            {
                inputMemoryStream.Position = 0;
                mute = XmlSerializerCache.Deserialize<MuteEntry>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.Forbidden);
            }

            // The currently active mutes.
            var mutes = Enumerable.Empty<MuteEntry>();
            bool mutesRetrieved;
            switch (Cache.MuteCache.IsVirgin)
            {
                case true:
                    mutesRetrieved = Services.GetMutes(Corrade.Client, Corrade.corradeConfiguration.ServicesTimeout,
                        ref mutes);
                    break;

                default:
                    mutes = Cache.MuteCache.OfType<MuteEntry>();
                    mutesRetrieved = true;
                    break;
            }

            if (!mutesRetrieved)
                throw new HTTPException((int)HttpStatusCode.InternalServerError);

            var muteExists =
                mutes.AsParallel().Any(o => o.ID.Equals(mute.ID) && o.Name.Equals(mute.Name));

            // If the mute does not exist then we have nothing to do.
            if (!muteExists)
                return;

            Cache.RemoveMute(mute.Flags, mute.ID, mute.Name, mute.Type);

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("mute", "PUT")]
        private void AddMuteSynchronization(string type, IPEndPoint endPoint, Configuration.HordePeer hordePeer,
            HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /mute/add | /mute/remove */
            // Break if the mute request is incompatible with the mute web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Add))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            MuteEntry mute;
            try
            {
                inputMemoryStream.Position = 0;
                mute = XmlSerializerCache.Deserialize<MuteEntry>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.Forbidden);
            }

            // The currently active mutes.
            var mutes = Enumerable.Empty<MuteEntry>();
            bool mutesRetrieved;
            switch (Cache.MuteCache.IsVirgin)
            {
                case true:
                    mutesRetrieved = Services.GetMutes(Corrade.Client, Corrade.corradeConfiguration.ServicesTimeout,
                        ref mutes);
                    break;

                default:
                    mutes = Cache.MuteCache.OfType<MuteEntry>();
                    mutesRetrieved = true;
                    break;
            }

            if (!mutesRetrieved)
                throw new HTTPException((int)HttpStatusCode.InternalServerError);

            var muteExists =
                mutes.AsParallel().Any(o => o.ID.Equals(mute.ID) && o.Name.Equals(mute.Name));

            // Check that the mute entry does not already exist
            if (muteExists)
                return;

            // Add the mute.
            var MuteListUpdatedEvent = new ManualResetEvent(false);
            EventHandler<EventArgs> MuteListUpdatedEventHandler =
                (sender, args) => MuteListUpdatedEvent.Set();

            Locks.ClientInstanceSelfLock.EnterWriteLock();
            Corrade.Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
            Corrade.Client.Self.UpdateMuteListEntry(mute.Type, mute.ID, mute.Name, mute.Flags);
            if (
                !MuteListUpdatedEvent.WaitOne((int)Corrade.corradeConfiguration.ServicesTimeout,
                    false))
            {
                Corrade.Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                Locks.ClientInstanceSelfLock.ExitWriteLock();
                throw new HTTPException((int)HttpStatusCode.InternalServerError);
            }
            Corrade.Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
            Locks.ClientInstanceSelfLock.ExitWriteLock();

            // Add the mute to the cache.
            Cache.AddMute(mute.Flags, mute.ID, mute.Name, mute.Type);

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("softban", "DELETE")]
        private void RemoveSoftBanSynchronization(string type, UUID groupUUID, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /softban/add/<Group UUID> /softban/remove/<Group UUID> */

            // Break if the softban request is incompatible with the softban web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Remove))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Group is not a configured group.
            if (!Corrade.corradeConfiguration.Groups.AsParallel().Any(o => o.UUID.Equals(groupUUID)))
            {
                throw new HTTPException((int)HttpStatusCode.Forbidden);
            }

            SoftBan softBan;
            try
            {
                inputMemoryStream.Position = 0;
                softBan = XmlSerializerCache.Deserialize<SoftBan>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            // Invalid soft ban.
            if (softBan.Equals(default(SoftBan)))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var groupSoftBansModified = false;

            lock (Corrade.GroupSoftBansLock)
            {
                if (Corrade.GroupSoftBans.ContainsKey(groupUUID) &&
                    Corrade.GroupSoftBans[groupUUID].AsParallel()
                        .Any(o => o.Agent.Equals(softBan.Agent)))
                {
                    Corrade.GroupSoftBans[groupUUID].RemoveWhere(o => o.Agent.Equals(softBan.Agent));
                    groupSoftBansModified = true;
                }
            }

            if (groupSoftBansModified)
                Corrade.SaveGroupSoftBansState.Invoke();

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("softban", "PUT")]
        private void AddSoftBanSynchronization(string type, UUID groupUUID, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /softban/add/<Group UUID> /softban/remove/<Group UUID> */

            // Break if the softban request is incompatible with the softban web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Add))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Group is not a configured group.
            if (!Corrade.corradeConfiguration.Groups.AsParallel().Any(o => o.UUID.Equals(groupUUID)))
            {
                throw new HTTPException((int)HttpStatusCode.Forbidden);
            }

            SoftBan softBan;
            try
            {
                inputMemoryStream.Position = 0;
                softBan = XmlSerializerCache.Deserialize<SoftBan>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            // Invalid soft ban.
            if (softBan.Equals(default(SoftBan)))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var groupSoftBansModified = false;

            lock (Corrade.GroupSoftBansLock)
            {
                switch (!Corrade.GroupSoftBans.ContainsKey(groupUUID))
                {
                    case true:
                        Corrade.GroupSoftBans.Add(groupUUID,
                            new ObservableHashSet<SoftBan>());
                        Corrade.GroupSoftBans[groupUUID].CollectionChanged += Corrade.HandleGroupSoftBansChanged;
                        Corrade.GroupSoftBans[groupUUID].Add(softBan);
                        groupSoftBansModified = true;
                        break;

                    default:
                        if (
                            Corrade.GroupSoftBans[groupUUID].AsParallel()
                                .Any(o => o.Agent.Equals(softBan.Agent)))
                            break;
                        Corrade.GroupSoftBans[groupUUID].Add(softBan);
                        groupSoftBansModified = true;
                        break;
                }
            }

            if (groupSoftBansModified)
                Corrade.SaveGroupSoftBansState.Invoke();

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("user", "DELETE")]
        private void RemoveUserSynchronization(string type, string group, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /user/add/<Group UUID> /user/remove/<Group UUID> */
            // Break if the user request is incompatible with the user web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Remove))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Retrieve the group UUID being pushed.
            UUID groupUUID;
            if (!UUID.TryParse(group, out groupUUID))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            Configuration.Group configurationGroup;
            try
            {
                inputMemoryStream.Position = 0;
                configurationGroup = XmlSerializerCache.Deserialize<Configuration.Group>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            // Invalid configuration group.
            if (configurationGroup == null || configurationGroup.Equals(default(Configuration.Group)))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Check that this is the group that is being pushed.
            if (!groupUUID.Equals(configurationGroup.UUID))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Search the configuration for the pushed group.
            var configuredGroup = Corrade.corradeConfiguration.Groups.AsParallel()
                .FirstOrDefault(o => o.UUID.Equals(configurationGroup.UUID));
            var corradeConfigurationGroupsModified = false;

            // If the configuration contains the group, then remove the configured group.
            if (configuredGroup != null && !configuredGroup.Equals(default(Configuration.Group)))
            {
                Corrade.corradeConfiguration.Groups.Remove(configuredGroup);
                corradeConfigurationGroupsModified = true;
            }

            // Save the configuration to the configuration file.
            if (corradeConfigurationGroupsModified)
            {
                try
                {
                    var updatedCorradeConfiguration = new Configuration();
                    lock (Corrade.ConfigurationFileLock)
                    {
                        using (
                            var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Create,
                                FileAccess.Write, FileShare.None, 16384, true))
                        {
                            Corrade.corradeConfiguration.Save(fileStream, ref updatedCorradeConfiguration);
                        }
                    }
                    Corrade.corradeConfiguration = updatedCorradeConfiguration;
                }
                catch (Exception)
                {
                    throw new HTTPException((int)HttpStatusCode.InternalServerError);
                }
            }

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("user", "PUT")]
        private void AddUserSynchronization(string type, string group, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /user/add/<Group UUID> /user/remove/<Group UUID> */
            // Break if the user request is incompatible with the user web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Add))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Retrieve the group UUID being pushed.
            UUID groupUUID;
            if (!UUID.TryParse(group, out groupUUID))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            Configuration.Group configurationGroup;
            try
            {
                inputMemoryStream.Position = 0;
                configurationGroup = XmlSerializerCache.Deserialize<Configuration.Group>(inputMemoryStream);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            // Invalid configuration group.
            if (configurationGroup == null || configurationGroup.Equals(default(Configuration.Group)))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Check that this is the group that is being pushed.
            if (!groupUUID.Equals(configurationGroup.UUID))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // Search the configuration for the pushed group.
            var configuredGroup = Corrade.corradeConfiguration.Groups.AsParallel()
                .FirstOrDefault(o => o.UUID.Equals(configurationGroup.UUID));
            var corradeConfigurationGroupsModified = false;

            // If the configuration does not contain the group, then add the group.
            if (configuredGroup == null || configuredGroup.Equals(default(Configuration.Group)))
            {
                Corrade.corradeConfiguration.Groups.Add(configurationGroup);
                corradeConfigurationGroupsModified = true;
            }

            // Save the configuration to the configuration file.
            if (corradeConfigurationGroupsModified)
            {
                try
                {
                    var updatedCorradeConfiguration = new Configuration();
                    lock (Corrade.ConfigurationFileLock)
                    {
                        using (
                            var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Create,
                                FileAccess.Write, FileShare.None, 16384, true))
                        {
                            Corrade.corradeConfiguration.Save(fileStream, ref updatedCorradeConfiguration);
                        }
                    }
                    Corrade.corradeConfiguration = updatedCorradeConfiguration;
                }
                catch (Exception)
                {
                    throw new HTTPException((int)HttpStatusCode.InternalServerError);
                }
            }

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("bayes", "DELETE")]
        private void RemoveBayesSynchronization(string type, UUID groupUUID, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /bayes/add/<Group UUID> /bayes/remove/<Group UUID> */
            // Break if the bayes request is incompatible with the bayes web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Remove))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            var bayes = new BayesSimpleTextClassifier();
            try
            {
                inputMemoryStream.Position = 0;
                bayes.ImportJsonData(Encoding.UTF8.GetString(inputMemoryStream.ToArray()));
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            var bayesDataModified = false;
            lock (Corrade.GroupBayesClassifiersLock)
            {
                if (Corrade.GroupBayesClassifiers.ContainsKey(groupUUID))
                {
                    Corrade.GroupBayesClassifiers.Remove(groupUUID);
                    bayesDataModified = true;
                }
            }

            if (bayesDataModified)
                Corrade.SaveGroupBayesClassificiations.Invoke();

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }

        [HTTPRequestMapping("bayes", "PUT")]
        private void AddBayesSynchronization(string type, UUID groupUUID, IPEndPoint endPoint,
            Configuration.HordePeer hordePeer, HttpListenerResponse HTTPServerResponse,
            MemoryStream inputMemoryStream, MemoryStream outputMemoryStream)
        {
            /* /bayes/add/<Group UUID> /bayes/remove/<Group UUID> */
            // Break if the bayes request is incompatible with the bayes web resource.
            if (string.IsNullOrEmpty(type))
                throw new HTTPException((int)HttpStatusCode.BadRequest);

            var dataSynchronizationType =
                Reflection.GetEnumValueFromName<Configuration.HordeDataSynchronization>(type);

            // Log the attempt to put cache objects.
            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_ATTEMPTING_SYNCHRONIZATION),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));

            // If this synchronization is not allowed with this peer, then break.
            if (!hordePeer.SynchronizationMask.IsMaskFlagSet(dataSynchronizationType))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            // If this synchronization option is not allowed with this peer, then break.
            if (
                !hordePeer.HasDataSynchronizationOption(dataSynchronizationType,
                    Configuration.HordeDataSynchronizationOption.Add))
                throw new HTTPException((int)HttpStatusCode.Forbidden);

            var bayes = new BayesSimpleTextClassifier();
            try
            {
                inputMemoryStream.Position = 0;
                bayes.ImportJsonData(Encoding.UTF8.GetString(inputMemoryStream.ToArray()));
            }
            catch (Exception ex)
            {
                Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.UNABLE_TO_READ_DISTRIBUTED_RESOURCE),
                    Reflection.GetNameFromEnumValue(dataSynchronizationType),
                    ex?.PrettyPrint());
                throw new HTTPException((int)HttpStatusCode.BadRequest);
            }

            var bayesDataModified = false;
            lock (Corrade.GroupBayesClassifiersLock)
            {
                switch (Corrade.GroupBayesClassifiers.ContainsKey(groupUUID))
                {
                    case true:
                        Corrade.GroupBayesClassifiers[groupUUID] = bayes;
                        bayesDataModified = true;
                        break;

                    default:
                        Corrade.GroupBayesClassifiers.Add(groupUUID, bayes);
                        bayesDataModified = true;
                        break;
                }
            }

            if (bayesDataModified)
                Corrade.SaveGroupBayesClassificiations.Invoke();

            Corrade.Feedback(CORRADE_CONSTANTS.WEB_REQUEST + "(" + endPoint + ")",
                Reflection.GetDescriptionFromEnumValue(
                    Enumerations.ConsoleMessage.PEER_SYNCHRONIZATION_SUCCESSFUL),
                Reflection.GetNameFromEnumValue(dataSynchronizationType));
        }
    }
}
