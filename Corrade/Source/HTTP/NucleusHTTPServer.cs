///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using MimeSharp;
using Newtonsoft.Json;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Collections.Utilities;
using wasSharp.Timers;
using wasSharp.Web;
using wasSharpNET.Cryptography;
using wasSharpNET.Network.HTTP;
using wasSharpNET.Platform.Windows.Commands.NetSH;
using Reflection = wasSharp.Reflection;
using SHA1 = System.Security.Cryptography.SHA1;

namespace Corrade.HTTP
{
    internal class NucleusHTTPServer : HTTPServer
    {
        public static Dictionary<string, Dictionary<string, string>> NucleusNotifications =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public object NucleusNotificationsLock = new object();

        public static readonly Action PurgeNucleus = () =>
        {
            lock (NucleusLock)
            {
                Nucleus.Value.Clear();
            }
        };

        private static readonly Mime mime = new Mime();

        private static readonly System.Lazy<Dictionary<string, CoreFile>> Nucleus =
            new System.Lazy<Dictionary<string, CoreFile>>();

        private static readonly object NucleusLock = new object();

        public readonly Timer CacheExpiryTimer = new Timer(((Expression<Action>)(() =>
           Nucleus.Value.Values.AsParallel()
               .TakeWhile(o => o.CachedTime.Add(o.CacheExpire).CompareTo(DateTime.Now) <= 0)
               .Take(1)
               .ForAll(o => PurgeNucleus.Invoke()
               ))).Compile(), TimeSpan.Zero, TimeSpan.Zero);

        public bool SuggestNoCaching { get; set; } = false;

        public IEnumerable<string> Prefixes { get; private set; }

        private static Dictionary<string, CoreFile> LoadNucleus()
        {
            var files = new Dictionary<string, CoreFile>();
            var FilesLock = new object();
            var EntryLock = new object();

            try
            {
                Directory.GetFiles(CORRADE_CONSTANTS.NUCLEUS_ROOT, @"*.zip")
                    .OrderBy(o => o)
                    .AsParallel()
                    .ForAll(nucleons =>
                    {
                        using (
                            var fileStream = new FileStream(nucleons, FileMode.Open, FileAccess.Read, FileShare.Read,
                                16384, true))
                        {
                            using (
                                var zipInputArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, false,
                                    Encoding.UTF8))
                            {
                                zipInputArchive.Entries.AsParallel().ForAll(zipInputEntry =>
                                {
                                    using (var dataStream = new MemoryStream())
                                    {
                                        lock (EntryLock)
                                        {
                                            using (var inputEntry = zipInputEntry.Open())
                                            {
                                                inputEntry.CopyTo(dataStream);
                                            }
                                        }

                                        dataStream.Position = 0;

                                        lock (FilesLock)
                                        {
                                            if (files.ContainsKey(zipInputEntry.FullName))
                                            {
                                                files.Remove(zipInputEntry.FullName);
                                            }
                                            files.Add(zipInputEntry.FullName, new CoreFile
                                            {
                                                Name = zipInputEntry.Name,
                                                Path = zipInputEntry.FullName,
                                                Data = dataStream.ToArray()
                                            });
                                        }
                                    }
                                });
                            }
                        }
                    });
                return files;
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.NUCLEUS_COMPILE_FAILED),
                    ex.Message);
                return new Dictionary<string, CoreFile>();
            }
        }

        private readonly HashSet<Regex> blessingsRegExes = new HashSet<Regex>();

        public new bool Start(IEnumerable<string> Prefixes)
        {
            this.Prefixes = Prefixes;

            foreach (var prefix in Prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (Environment.OSVersion.Platform.Equals(PlatformID.Win32NT) &&
                    !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName);
                    if (!acl.isReserved)
                        acl.Reserve();
                }
            }

            // Construct blessings regular expressions.
            var LockObject = new object();
            Corrade.corradeConfiguration.NucleusServerBlessings.AsParallel().ForAll(o =>
            {
                try
                {
                    var blessing = new Regex(o, RegexOptions.Compiled);
                    lock (LockObject)
                    {
                        blessingsRegExes.Add(blessing);
                    }
                }
                catch (Exception)
                {
                    /* Any blessing we cannot compile, we discard silently at this time. */
                }
            });

            // Start the HTTP server.
            return base.Start(Prefixes);
        }

        public new bool Stop()
        {
            var success = base.Stop();

            foreach (var prefix in Prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (Environment.OSVersion.Platform.Equals(PlatformID.Win32NT) &&
                    !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName);
                    if (acl.isReserved)
                        acl.Release();
                }
            }

            // Remove blessings.
            blessingsRegExes.Clear();

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
                // Authenticate if server started with authentication.
                if (!AuthenticationSchemes.Equals(AuthenticationSchemes.Anonymous))
                {
                    // If authentication is not enabled or the client has not sent any authentication then stop.
                    if (!httpContext.Request.IsAuthenticated)
                    {
                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                    }

                    var identity = (HttpListenerBasicIdentity)httpContext.User.Identity;
                    if (
                        !identity.Name.Equals(Corrade.corradeConfiguration.NucleusServerUsername,
                            StringComparison.Ordinal) ||
                        !Utils.SHA1String(identity.Password).Equals(Corrade.corradeConfiguration.NucleusServerPassword,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                    }
                }

                // Remove trailing slashes.
                if (!(httpRequest.Url.Segments.Length.Equals(1) && httpRequest.Url.Segments[0].Equals("/")) &&
                    httpRequest.Url.OriginalString.EndsWith("/"))
                {
                    using (var NucleusResponse = httpContext.Response)
                    {
                        NucleusResponse.StatusCode = 301;
                        NucleusResponse.Redirect(httpRequest.Url.OriginalString.TrimEnd('/'));
                    }
                    return;
                }

                var path = httpRequest.Url.Segments.Select(o => o.Replace(@"/", ""))
                    .Where(o => !string.IsNullOrEmpty(o));

                var ContentSent = false;
                switch (httpRequest.HttpMethod)
                {
                    case WebRequestMethods.Http.Put:
                        using (var NucleusResponse = httpContext.Response)
                        {
                            try
                            {
                                // retrieve the message sent even if it is a compressed stream.
                                using (var dataMemoryStream = new MemoryStream())
                                {
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

                                    switch (requestEncoding.Name.ToLowerInvariant())
                                    {
                                        case "gzip":
                                            await httpRequest.InputStream.GZipDecompress(dataMemoryStream);
                                            break;

                                        case "deflate":
                                            await httpRequest.InputStream.DeflateDecompress(dataMemoryStream);
                                            break;

                                        default:
                                            await httpRequest.InputStream.CopyToAsync(dataMemoryStream);
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

                                    switch (method != null)
                                    {
                                        case true: // The current configuration file.
                                            // Get method parameters
                                            var strParams = path.ToArray();

                                            // Convert method parameters to function parameter type and add local parameters.
                                            var @params = method.GetParameters()
                                                .AsParallel()
                                                .Where(o => o.ParameterType == typeof(string))
                                                .Select((p, i) => Convert.ChangeType(strParams[i], p.ParameterType))
                                                // Add custom items.
                                                .Concat(new dynamic[] { dataMemoryStream })
                                                .ToArray();

                                            NucleusResponse.StatusCode = (int)HttpStatusCode.OK;

                                            await ((Task)method.Invoke(this, @params)).ContinueWith((o) =>
                                           {
                                               ContentSent = true;
                                           });
                                            break;
                                    }
                                }
                            }
                            catch (HTTPException ex)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = ex.StatusCode;
                                }
                                throw;
                            }
                            catch (Exception)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                                }
                                throw;
                            }
                        }
                        break;

                    case WebRequestMethods.Http.Get:
                        using (var NucleusResponse = httpContext.Response)
                        {
                            using (var etagStream = new MemoryStream())
                            {
                                try
                                {
                                    using (var dataMemoryStream = new MemoryStream())
                                    {
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

                                        switch (method != null)
                                        {
                                            case true: // found a method, so invoke it.
                                                // Get method parameters
                                                var strParams = path.ToArray();

                                                // Convert method parameters to function parameter type and add local parameters.
                                                var @params = method.GetParameters()
                                                    .AsParallel()
                                                    .Where(o => o.ParameterType == typeof(string))
                                                    .Select((p, i) => Convert.ChangeType(strParams[i], p.ParameterType))
                                                    // Add custom items.
                                                    .Concat(new dynamic[] { NucleusResponse, dataMemoryStream })
                                                    .ToArray();

                                                // Invoke the method.
                                                await (Task)method.Invoke(this, @params);
                                                break;

                                            default:
                                                lock (NucleusLock)
                                                {
                                                    // If the nucleus is not loaded, then load it now.
                                                    if (!Nucleus.IsValueCreated || !Nucleus.Value.Any())
                                                        Nucleus.Value.UnionWith(LoadNucleus());

                                                    var url = string.Join(@"/", path);
                                                    var file = new CoreFile();
                                                    if (Nucleus.Value.TryGetValue(url, out file) ||
                                                        Nucleus.Value.TryGetValue(
                                                            string.Format("{0}/{1}", url,
                                                                CORRADE_CONSTANTS.NUCLEUS_DEFAULT_DOCUMENT).Trim('/'),
                                                            out file))
                                                    {
                                                        using (var memoryStream = new MemoryStream(file.Data))
                                                        {
                                                            memoryStream.CopyTo(dataMemoryStream);
                                                        }
                                                        // Override Mime.
                                                        var fileMime = mime.Lookup(file.Name);
                                                        switch (file.Name.Split('.').Last().ToUpperInvariant())
                                                        {
                                                            case "HTML":
                                                            case "HTM":
                                                                fileMime = @"text/html";
                                                                break;
                                                        }
                                                        NucleusResponse.ContentType = fileMime;
                                                        break;
                                                    }

                                                    if (string.IsNullOrEmpty(url))
                                                    {
                                                        using (
                                                            var CSVStream =
                                                                new MemoryStream(
                                                                    Encoding.UTF8.GetBytes(
                                                                        CSV.FromEnumerable(
                                                                            Nucleus.Value.Keys.AsParallel()
                                                                                .Select(
                                                                                    o => o.Split('/').FirstOrDefault())
                                                                                .Where(o => !string.IsNullOrEmpty(o))
                                                                                .Distinct()))))
                                                        {
                                                            CSVStream.CopyTo(dataMemoryStream);
                                                        }
                                                        NucleusResponse.ContentType = @"text/plain";
                                                        break;
                                                    }

                                                    var items =
                                                        new List<string>(
                                                            Nucleus.Value.Values.AsParallel()
                                                                .Where(
                                                                    o =>
                                                                        o.Path.Split('/')
                                                                            .Reverse()
                                                                            .Skip(1)
                                                                            .Reverse()
                                                                            .SequenceEqual(path))
                                                                .Select(o => o.Name)
                                                                .Where(o => !string.IsNullOrEmpty(o))
                                                                .Distinct());
                                                    if (items.Any())
                                                    {
                                                        using (
                                                            var CSVStream =
                                                                new MemoryStream(
                                                                    Encoding.UTF8.GetBytes(CSV.FromEnumerable(items))))
                                                        {
                                                            CSVStream.CopyTo(dataMemoryStream);
                                                        }
                                                        NucleusResponse.ContentType = @"text/plain";
                                                    }
                                                }
                                                break;
                                        }

                                        // Rewind the data memory stream.
                                        dataMemoryStream.Position = 0;

                                        // Create an etag for the resource.
                                        await SHA1.Create().CopyToAsync(dataMemoryStream.ToArray(), etagStream);

                                        // If this is a HTTP 1.1 request, then send not modified if E-Tags match.
                                        if (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                        {
                                            DateTime modified;
                                            if (
                                                DateTime.TryParse(httpRequest.Headers["If-Modified-Since"], out modified) &&
                                                CacheExpiryTimer.ScheduledTime <= modified)
                                            {
                                                etagStream.Position = 0;
                                                if (
                                                    string.Equals(
                                                        httpRequest.Headers["If-None-Match"] ?? string.Empty,
                                                        Encoding.UTF8.GetString(etagStream.ToArray())))
                                                {
                                                    NucleusResponse.StatusCode = (int)HttpStatusCode.NotModified;
                                                    return;
                                                }
                                            }
                                        }

                                        // Create the output stream.
                                        using (var outputStream = new MemoryStream())
                                        {
                                            // perform compression based on the encoding advertised by the client.
                                            var replyEncoding = new QValue(@"identity");
                                            var acceptEncoding = httpRequest.Headers.GetValues(@"Accept-Encoding");
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
                                                    NucleusResponse.AddHeader(@"Content-Encoding", @"gzip");
                                                    break;

                                                case "deflate": // deflate compression
                                                    await dataMemoryStream.DeflateCompress(outputStream, true);
                                                    NucleusResponse.AddHeader(@"Content-Encoding", @"deflate");
                                                    break;

                                                default: // no compression
                                                    NucleusResponse.AddHeader(@"Content-Encoding", @"UTF-8");
                                                    await dataMemoryStream.CopyToAsync(outputStream);
                                                    break;
                                            }

                                            // KeepAlive and ChunkedEncoding for HTTP 1.1
                                            switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                            {
                                                case true:
                                                    NucleusResponse.ProtocolVersion = HttpVersion.Version11;
                                                    NucleusResponse.SendChunked = true;
                                                    NucleusResponse.KeepAlive = true;
                                                    break;

                                                default:
                                                    // Set content length.
                                                    NucleusResponse.ContentLength64 = outputStream.Length;
                                                    NucleusResponse.SendChunked = false;
                                                    NucleusResponse.KeepAlive = false;
                                                    break;
                                            }

                                            switch (SuggestNoCaching)
                                            {
                                                case true: // No caching was chosen so tell the client to not cache the response.
                                                    switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                                    {
                                                        case true:
                                                            NucleusResponse.Headers.Set(HttpResponseHeader.CacheControl,
                                                                "no-cache, no-store, must-revalidate");
                                                            break;

                                                        default:
                                                            NucleusResponse.Headers.Set(HttpResponseHeader.Pragma, "no-cache");
                                                            break;
                                                    }
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.Expires, "0");
                                                    break;

                                                default:
                                                    // Set the expires time of the resource depending on the nucleus rebuild schedule.
                                                    if (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                                    {
                                                        NucleusResponse.Headers.Set(HttpResponseHeader.CacheControl,
                                                            $"max-age={CacheExpiryTimer.DueTime.Seconds}, public");
                                                        etagStream.Position = 0;
                                                        NucleusResponse.Headers.Set(HttpResponseHeader.ETag,
                                                            Encoding.UTF8.GetString(etagStream.ToArray()));
                                                        break;
                                                    }
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.Expires,
                                                        DateTime.UtcNow.Add(CacheExpiryTimer.DueTime)
                                                            .ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.LastModified,
                                                        CacheExpiryTimer.ScheduledTime.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                                                    break;
                                            }
                                            NucleusResponse.StatusCode = (int)HttpStatusCode.OK;

                                            outputStream.Position = 0;
                                            await outputStream.CopyToAsync(NucleusResponse.OutputStream).ContinueWith((o) =>
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
                                        NucleusResponse.StatusCode = ex.StatusCode;
                                    }
                                    throw;
                                }
                                catch (Exception)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        NucleusResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    }
                                    throw;
                                }
                                finally
                                {
                                    // If caching is enabled (by default), then schedule emptying the cache -
                                    // otherwise immediately empty the cache.
                                    switch (Corrade.corradeConfiguration.EnableNucleusServerCache)
                                    {
                                        case true: // Schedule to empty the nucleus.
                                            CacheExpiryTimer.Change(
                                                TimeSpan.FromMilliseconds(
                                                    Corrade.corradeConfiguration.NucleusServerCachePurgeInterval),
                                                TimeSpan.Zero);
                                            break;

                                        default:
                                            PurgeNucleus.Invoke();
                                            break;
                                    }
                                }
                            }
                        }
                        break;

                    case WebRequestMethods.Http.Post:
                        using (var NucleusResponse = httpContext.Response)
                        {
                            try
                            {
                                using (var dataMemoryStream = new MemoryStream())
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
                                            await httpRequest.InputStream.GZipDecompress(dataMemoryStream);
                                            break;

                                        case "deflate":
                                            await httpRequest.InputStream.DeflateDecompress(dataMemoryStream);
                                            break;

                                        default:
                                            await httpRequest.InputStream.CopyToAsync(dataMemoryStream);
                                            break;
                                    }

                                    // Get the message.
                                    dataMemoryStream.Position = 0;
                                    var message = Encoding.UTF8.GetString(dataMemoryStream.ToArray());

                                    // ignore empty messages right-away.
                                    if (string.IsNullOrEmpty(message))
                                        throw new HTTPException((int)HttpStatusCode.BadRequest);

                                    // Attempt to retrieve the group from the message.
                                    var commandGroup = Corrade.GetCorradeGroupFromMessage(message,
                                        Corrade.corradeConfiguration);

                                    byte[] data;
                                    switch (commandGroup != null && !commandGroup.Equals(default(Configuration.Group)))
                                    {
                                        case false:
                                            switch (
                                                !string.IsNullOrEmpty(Corrade.corradeConfiguration.NucleusServerGroup))
                                            {
                                                case true:
                                                    commandGroup =
                                                        Corrade.corradeConfiguration.Groups.FirstOrDefault(
                                                            o =>
                                                                string.Equals(o.Name,
                                                                    Corrade.corradeConfiguration.NucleusServerGroup));
                                                    data =
                                                        Encoding.UTF8.GetBytes(
                                                            JsonConvert.SerializeObject(
                                                                Corrade.ProcessCommand(new Command.
                                                                    CorradeCommandParameters
                                                                {
                                                                    Group = commandGroup,
                                                                    Identifier = httpRequest.RemoteEndPoint.ToString(),
                                                                    Message = message,
                                                                    Sender = "Nucleus"
                                                                })));
                                                    break;

                                                default: // Generate a temporary group if no group was specified.
                                                    commandGroup = new Configuration.Group
                                                    {
                                                        UUID = UUID.Random(),
                                                        Permissions =
                                                            new HashSet<Configuration.Permissions>(
                                                                Enum.GetValues(typeof(Configuration.Permissions))
                                                                    .OfType<Configuration.Permissions>()),
                                                        Notifications =
                                                            new HashSet<Configuration.Notifications>(
                                                                Enum.GetValues(typeof(Configuration.Notifications))
                                                                    .OfType<Configuration.Notifications>()),
                                                        Name = Dns.GetHostEntry(Environment.MachineName).HostName
                                                    };
                                                    lock (Locks.ClientInstanceConfigurationLock)
                                                    {
                                                        Corrade.corradeConfiguration.Groups.Add(commandGroup);
                                                    }
                                                    data =
                                                        Encoding.UTF8.GetBytes(
                                                            JsonConvert.SerializeObject(
                                                                Corrade.ProcessCommand(new Command.
                                                                    CorradeCommandParameters
                                                                {
                                                                    Group = commandGroup,
                                                                    Identifier = httpRequest.RemoteEndPoint.ToString(),
                                                                    Message = message,
                                                                    Sender = "Nucleus"
                                                                })));
                                                    lock (Locks.ClientInstanceConfigurationLock)
                                                    {
                                                        Corrade.corradeConfiguration.Groups.Remove(commandGroup);
                                                    }
                                                    break;
                                            }
                                            break;

                                        default:
                                            data =
                                                Encoding.UTF8.GetBytes(
                                                    JsonConvert.SerializeObject(
                                                        Corrade.ProcessCommand(new Command.CorradeCommandParameters
                                                        {
                                                            Group = commandGroup,
                                                            Identifier = httpRequest.RemoteEndPoint.ToString(),
                                                            Message = message,
                                                            Sender = "Nucleus"
                                                        })));
                                            break;
                                    }

                                    using (var outputStream = new MemoryStream())
                                    {
                                        // perform compression based on the encoding advertised by the client.
                                        var responseEncoding = new QValue("identity");
                                        var acceptEncoding = httpRequest.Headers.GetValues("Accept-Encoding");
                                        if (acceptEncoding != null && acceptEncoding.Any())
                                        {
                                            var acceptEncodings = new QValueList(acceptEncoding);
                                            if (!acceptEncodings.Equals(default(QValueList)))
                                            {
                                                var preferredEncoding = acceptEncodings.FindPreferred("gzip", "deflate",
                                                    "identity");
                                                if (!preferredEncoding.IsEmpty)
                                                    responseEncoding = preferredEncoding;
                                            }
                                        }

                                        // retrieve the message sent even if it is a compressed stream.
                                        switch (responseEncoding.Name.ToLowerInvariant())
                                        {
                                            case "gzip":
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.GZipCompress(outputStream, true);
                                                }
                                                NucleusResponse.AddHeader("Content-Encoding", "gzip");
                                                break;

                                            case "deflate":
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.DeflateCompress(outputStream, true);
                                                }
                                                NucleusResponse.AddHeader("Content-Encoding", "deflate");
                                                break;

                                            default:
                                                NucleusResponse.AddHeader("Content-Encoding", "UTF-8");
                                                using (var memoryStream = new MemoryStream(data))
                                                {
                                                    await memoryStream.CopyToAsync(outputStream);
                                                }
                                                break;
                                        }

                                        // KeepAlive and ChunkedEncoding for HTTP 1.1
                                        // Command output should not be cached.
                                        switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                        {
                                            case true:

                                                NucleusResponse.ProtocolVersion = HttpVersion.Version11;
                                                NucleusResponse.SendChunked = true;
                                                NucleusResponse.KeepAlive = true;
                                                NucleusResponse.Headers.Set(HttpResponseHeader.CacheControl,
                                                    "no-cache, no-store, must-revalidate");
                                                break;

                                            default:
                                                // Set content length.
                                                NucleusResponse.ContentLength64 = outputStream.Length;
                                                NucleusResponse.SendChunked = false;
                                                NucleusResponse.KeepAlive = false;
                                                NucleusResponse.Headers.Set(HttpResponseHeader.Pragma, "no-cache");
                                                break;
                                        }

                                        NucleusResponse.Headers.Set(HttpResponseHeader.Expires, "0");
                                        NucleusResponse.StatusCode = (int)HttpStatusCode.OK;

                                        outputStream.Position = 0;
                                        await outputStream.CopyToAsync(NucleusResponse.OutputStream).ContinueWith((o) =>
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
                                    NucleusResponse.StatusCode = ex.StatusCode;
                                }
                                throw;
                            }
                            catch (Exception)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                                }
                                throw;
                            }
                        }
                        break;
                }
            }
            catch (HTTPException)
            {
                // Do not report HTTP status errors.
            }
            catch (HttpListenerException)
            {
                // Give two shits about clients breaking connection - up to them.
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.NUCLEUS_PROCESSING_ABORTED),
                    ex.Message);
            }
        }

        [HTTPRequestMapping("cfg", "PUT")]
        private async Task PutConfig(string type, string file, Stream dataMemoryStream)
        {
            // rewind
            dataMemoryStream.Position = 0;

            try
            {
                switch (file)
                {
                    case "Corrade.ini":
                        using (
                            var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Create,
                                FileAccess.Write, FileShare.None, 16384, true))
                        {
                            await dataMemoryStream.CopyToAsync(fileStream);
                        }
                        break;

                    default:
                        throw new HTTPException((int)HttpStatusCode.Forbidden);
                }
            }
            catch
            {
                throw new HTTPException((int)HttpStatusCode.InternalServerError);
            }
        }

        [HTTPRequestMapping("fs", "GET")]
        private async Task GetFile(string type, string file, HttpListenerResponse NucleusResponse,
            MemoryStream memoryStream)
        {
            try
            {
                // if no blessings match the requested file then send a forbidden code.
                if (!blessingsRegExes.AsParallel().Any(o => o.IsMatch(file)))
                {
                    throw new HTTPException((int)HttpStatusCode.Forbidden);
                }
                NucleusResponse.ContentType = mime.Lookup(Path.GetFileName(file));
                using (var fileStream = new FileStream(file, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 16384, true))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
            }
            catch
            {
                throw new HTTPException((int)HttpStatusCode.NotFound);
            }
        }

        [HTTPRequestMapping("fs", "PUT")]
        private async Task PutFile(string type, string file, Stream dataMemoryStream)
        {
            try
            {
                // if no blessings match the requested file then send a forbidden code.
                if (!blessingsRegExes.AsParallel().Any(o => o.IsMatch(file)))
                {
                    throw new HTTPException((int)HttpStatusCode.Forbidden);
                }
                using (
                    var fileStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true)
                    )
                {
                    await dataMemoryStream.CopyToAsync(fileStream);
                }
            }
            catch
            {
                throw new HTTPException((int)HttpStatusCode.NotFound);
            }
        }

        [HTTPRequestMapping("events", "GET")]
        private async Task GetNotification(string type, string notification, HttpListenerResponse NucleusResponse,
            MemoryStream memoryStream)
        {
            Dictionary<string, string> notificationData;

            lock (NucleusNotifications)
            {
                if (!NucleusNotifications.TryGetValue(notification, out notificationData))
                    throw new HTTPException((int)HttpStatusCode.NotFound);
            }

            NucleusResponse.ContentType = @"application/json";
            using (var notificationStream = new MemoryStream(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(notificationData))))
            {
                await notificationStream.CopyToAsync(memoryStream);
            }
        }

        [HTTPRequestMapping("cfg", "GET")]
        private async Task GetConfig(string type, string file, HttpListenerResponse NucleusResponse,
            MemoryStream memoryStream)
        {
            try
            {
                switch (file)
                {
                    case "Corrade.ini":
                        NucleusResponse.ContentType = @"text/xml";
                        using (
                            var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Open,
                                FileAccess.Read, FileShare.Read, 16384, true))
                        {
                            await fileStream.CopyToAsync(memoryStream);
                        }
                        break;

                    case "Corrade.ini.default":
                        NucleusResponse.ContentType = @"text/xml";
                        using (
                            var manifestStream =
                                Assembly.GetExecutingAssembly()
                                    .GetManifestResourceStream(@"Corrade.Corrade.ini.default"))
                        {
                            await manifestStream.CopyToAsync(memoryStream);
                        }
                        break;

                    default:
                        throw new HTTPException((int)HttpStatusCode.NotFound);
                }
            }
            catch
            {
                throw new HTTPException((int)HttpStatusCode.NotFound);
            }
        }

        public class CoreFile
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public byte[] Data { get; set; }

            public TimeSpan CacheExpire { get; set; } = TimeSpan.Zero;

            public DateTime CachedTime { get; set; } = DateTime.UtcNow;
        }
    }
}
