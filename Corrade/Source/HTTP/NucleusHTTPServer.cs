///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using MimeSharp;
using OpenMetaverse;
using ServiceStack.Text;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Web;
using wasSharpNET.Cryptography;
using wasSharpNET.Diagnostics;
using wasSharpNET.IO.Utilities;
using wasSharpNET.Network.HTTP;
using wasSharpNET.Platform.Windows.Commands.NetSH;
using ReaderWriterLockSlim = System.Threading.ReaderWriterLockSlim;
using Reflection = wasSharp.Reflection;
using SHA1 = System.Security.Cryptography.SHA1;
using Timer = wasSharp.Timers.Timer;

namespace Corrade.HTTP
{
    internal class NucleusHTTPServer : HTTPServer, IDisposable
    {
        public static readonly Action PurgeNucleus = () =>
        {
            if (!NucleusLock.TryEnterWriteLock(
                TimeSpan.FromMilliseconds(
                    Corrade.corradeConfiguration.ServicesTimeout))) return;
            PurgeCache.Invoke();
            NucleusLock.ExitWriteLock();
        };

        private static readonly Mime mime = new Mime();

        private static readonly ReaderWriterLockSlim NucleusLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private static readonly Action PurgeCache = () => Path.Combine(
            CORRADE_CONSTANTS.CACHE_DIRECTORY,
            CORRADE_CONSTANTS.NUCLEUS_CACHE_DIRECTORY).Empty();

        private static readonly FileSystemWatcher NucleonsUpdateWatcher = new FileSystemWatcher();

        public static readonly Action CompileNucleus = () =>
        {
            var FilesLock = new object();
            var EntryLock = new object();

            try
            {
                NucleusLock.EnterWriteLock();
                Directory.EnumerateFiles(CORRADE_CONSTANTS.NUCLEUS_ROOT, @"*.zip")
                    .OrderBy(o => o)
                    .AsParallel()
                    .ForAll(nucleons =>
                    {
                        using (
                            var readFileStream = new FileStream(nucleons, FileMode.Open, FileAccess.Read,
                                FileShare.Read,
                                16384, true))
                        {
                            using (
                                var zipInputArchive = new ZipArchive(readFileStream, ZipArchiveMode.Read, false,
                                    Encoding.UTF8))
                            {
                                zipInputArchive.Entries.AsParallel().ForAll(zipInputEntry =>
                                {
                                    var file = Path.GetFullPath(
                                        Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                                            CORRADE_CONSTANTS.NUCLEUS_CACHE_DIRECTORY,
                                            zipInputEntry.FullName));
                                    var directory = Path.GetDirectoryName(file);
                                    switch (zipInputEntry.FullName.EndsWith(@"/"))
                                    {
                                        case true:
                                            if (!Directory.Exists(directory))
                                                Directory.CreateDirectory(directory);
                                            return;
                                        default:
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
                                                    if (!Directory.Exists(directory))
                                                        Directory.CreateDirectory(directory);
                                                    using (
                                                        var writeFileStream =
                                                            new FileStream(file, FileMode.Create, FileAccess.Write,
                                                                FileShare.None, 16384, true))
                                                    {
                                                        dataStream.CopyTo(writeFileStream);
                                                    }
                                                }
                                            }
                                            break;
                                    }
                                });
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.NUCLEUS_COMPILE_FAILED),
                    ex.PrettyPrint());
            }
            finally
            {
                NucleusLock.ExitWriteLock();
            }
        };

        private static readonly Timer NucleusUpdatedTimer =
            new Timer(() =>
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.REBUILDING_NUCLEUS));

                PurgeCache.Invoke();
                CompileNucleus.Invoke();
            });

        private readonly HashSet<Regex> blessingsRegExes = new HashSet<Regex>();

        public readonly Timer DiskCacheExpiryTimer = new Timer(PurgeCache);

        public bool SuggestNoCaching { get; set; } = false;

        public List<string> Prefixes { get; } = new List<string>();

        public new void Dispose()
        {
            Stop();
        }

        public new bool Start(List<string> prefixes)
        {
            // Reserve any prefixes for Windows
            foreach (var prefix in prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (!Utils.GetRunningPlatform().Equals(Utils.Platform.Windows) ||
                    new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Prefixes.Add(prefix);
                    continue;
                }
                var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName,
                    (int) Corrade.corradeConfiguration.ServicesTimeout);
                if (!acl.isReserved)
                {
                    if (acl.Reserve())
                        Prefixes.Add(prefix);
                    continue;
                }
                Prefixes.Add(prefix);
            }

            // No prefixes installed so return.
            if (!Prefixes.Any())
                return false;

            // Compile Nucleus.
            NucleusLock.EnterWriteLock();
            CompileNucleus.Invoke();
            NucleusLock.ExitWriteLock();

            // Start the nucleons update watcher.
            try
            {
                NucleonsUpdateWatcher.Path = CORRADE_CONSTANTS.NUCLEUS_ROOT;
                NucleonsUpdateWatcher.Filter = @"*.zip";
                NucleonsUpdateWatcher.NotifyFilter = NotifyFilters.LastWrite;
                NucleonsUpdateWatcher.IncludeSubdirectories = true;
                NucleonsUpdateWatcher.Changed += HandleNucleusUpdated;
                NucleonsUpdateWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(
                        Enumerations.ConsoleMessage.ERROR_SETTING_UP_NUCLEUS_WATCHER),
                    ex.PrettyPrint());
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

        private void HandleNucleusUpdated(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            try
            {
                NucleonsUpdateWatcher.EnableRaisingEvents = false;

                if (!NucleusLock.TryEnterWriteLock(
                    TimeSpan.FromMilliseconds(
                        Corrade.corradeConfiguration.ServicesTimeout))) return;

                NucleusUpdatedTimer.Change(1000, 0);
            }
            finally
            {
                NucleonsUpdateWatcher.EnableRaisingEvents = true;
                NucleusLock.ExitWriteLock();
            }
        }

        public new void Stop()
        {
            // Stop the nucleons update watcher.
            try
            {
                NucleonsUpdateWatcher.EnableRaisingEvents = false;
                NucleonsUpdateWatcher.Changed -= HandleNucleusUpdated;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }

            // Stop the HTTP server.
            base.Stop();

            foreach (var prefix in Prefixes)
            {
                // For the Windows platform, if Corrade is not run with Administrator privileges, we need to reserve an URL.
                if (!Utils.GetRunningPlatform().Equals(Utils.Platform.Windows) ||
                    new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                    continue;
                var acl = new URLACL(prefix, Environment.UserName, Environment.UserDomainName,
                    (int) Corrade.corradeConfiguration.ServicesTimeout);
                if (acl.isReserved)
                    acl.Release();
            }

            // Remove blessings.
            blessingsRegExes.Clear();

            // Clear prefixes.
            Prefixes.Clear();
        }

        public override async void ProcessHTTPContext(HttpListenerContext httpContext)
        {
            var httpRequest = httpContext.Request;
            // Do not serve empty discuonnected remote endpoints.
            if (httpRequest.RemoteEndPoint == null)
                return;

            try
            {
                // Authenticate if server started with authentication.
                if (!AuthenticationSchemes.Equals(AuthenticationSchemes.Anonymous))
                {
                    // If authentication is not enabled or the client has not sent any authentication then stop.
                    if (!httpContext.User.Identity.IsAuthenticated)
                        throw new HTTPException((int) HttpStatusCode.Forbidden);

                    var identity = (HttpListenerBasicIdentity) httpContext.User.Identity;
                    if (
                        !identity.Name.Equals(Corrade.corradeConfiguration.NucleusServerUsername,
                            StringComparison.Ordinal) ||
                        !Utils.SHA1String(identity.Password).Equals(Corrade.corradeConfiguration.NucleusServerPassword,
                            StringComparison.OrdinalIgnoreCase))
                        throw new HTTPException((int) HttpStatusCode.Forbidden);
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
                    .Where(o => !string.IsNullOrEmpty(o)).ToList();

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
                                                .Equals(path.Count))
                                        // Find method name.
                                        .FirstOrDefault(o =>
                                            o.GetCustomAttributes(true)
                                                .AsParallel()
                                                .Any(p =>
                                                    p is HTTPRequestMapping &&
                                                    string.Equals(((HTTPRequestMapping) p).Method,
                                                        httpRequest.HttpMethod.ToUpperInvariant()) &&
                                                    string.Equals(((HTTPRequestMapping) p).Map, methodName)));

                                    switch (method != null)
                                    {
                                        case true: // The current configuration file.
                                            // Get method parameters
                                            var strParams = path.ToArray();

                                            // Convert method parameters to function parameter type and add local parameters.
                                            var @params = method.GetParameters()
                                                .Where(o => o.ParameterType == typeof(string))
                                                .Select((p, i) => Convert.ChangeType(strParams[i], p.ParameterType))
                                                // Add custom items.
                                                .Concat(new dynamic[] {dataMemoryStream})
                                                .ToArray();

                                            NucleusResponse.StatusCode = (int) HttpStatusCode.OK;

                                            await ((Task) method.Invoke(this, @params)).ContinueWith(o =>
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
                                    NucleusResponse.Close();
                                }
                                throw;
                            }
                            catch (Exception)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                                    NucleusResponse.Close();
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
                                    // Disable cache purge timer for the duration of this request.
                                    if (Corrade.corradeConfiguration.EnableNucleusServerCache)
                                        DiskCacheExpiryTimer.Change(TimeSpan.Zero, TimeSpan.Zero);

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
                                                    .Equals(path.Count))
                                            // Find method name.
                                            .FirstOrDefault(o =>
                                                o.GetCustomAttributes(true)
                                                    .AsParallel()
                                                    .Any(p =>
                                                        p is HTTPRequestMapping &&
                                                        string.Equals(((HTTPRequestMapping) p).Method,
                                                            httpRequest.HttpMethod.ToUpperInvariant()) &&
                                                        string.Equals(((HTTPRequestMapping) p).Map, methodName)));

                                        switch (method != null)
                                        {
                                            case true: // found a method, so invoke it.
                                                // Get method parameters
                                                var strParams = path.ToArray();

                                                // Convert method parameters to function parameter type and add local parameters.
                                                var @params = method.GetParameters()
                                                    .Where(o => o.ParameterType == typeof(string))
                                                    .Select((p, i) => Convert.ChangeType(strParams[i], p.ParameterType))
                                                    // Add custom items.
                                                    .Concat(new dynamic[] {NucleusResponse, dataMemoryStream})
                                                    .ToArray();

                                                // Invoke the method.
                                                await (Task) method.Invoke(this, @params);
                                                break;

                                            default:

                                                var nucleusCacheDirectory =
                                                    Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                                                        CORRADE_CONSTANTS.NUCLEUS_CACHE_DIRECTORY);

                                                // Get the real path to the request.
                                                var cachedFile = Path.GetFullPath(
                                                    Path.Combine(
                                                        nucleusCacheDirectory,
                                                        string.Join(
                                                            Path.DirectorySeparatorChar.ToString(),
                                                            path
                                                        )
                                                    )
                                                );

                                                // Check for path traversals.
                                                if (!cachedFile
                                                    .isRootedIn(Path.GetFullPath(nucleusCacheDirectory)))
                                                {
                                                    NucleusResponse.StatusCode = (int) HttpStatusCode.NotFound;
                                                    return;
                                                }

                                                // Requested file exists.
                                                if (File.Exists(cachedFile))
                                                {
                                                    using (
                                                        var readFileStream = new FileStream(cachedFile, FileMode.Open,
                                                            FileAccess.Read,
                                                            FileShare.Read,
                                                            16384, true))
                                                    {
                                                        readFileStream.CopyTo(dataMemoryStream);
                                                    }
                                                    // Override Mime.
                                                    var fileMime = mime.Lookup(cachedFile);
                                                    switch (cachedFile.Split('.').Last().ToUpperInvariant())
                                                    {
                                                        case "HTML":
                                                        case "HTM":
                                                            fileMime = @"text/html";
                                                            break;
                                                    }
                                                    NucleusResponse.ContentType = fileMime;
                                                    break;
                                                }

                                                // Index file exists in requested path.
                                                if (File.Exists(Path.Combine(cachedFile,
                                                    CORRADE_CONSTANTS.NUCLEUS_DEFAULT_DOCUMENT)))
                                                {
                                                    cachedFile = Path.Combine(cachedFile,
                                                        CORRADE_CONSTANTS.NUCLEUS_DEFAULT_DOCUMENT);
                                                    using (
                                                        var readFileStream = new FileStream(cachedFile, FileMode.Open,
                                                            FileAccess.Read,
                                                            FileShare.Read,
                                                            16384, true))
                                                    {
                                                        readFileStream.CopyTo(dataMemoryStream);
                                                    }
                                                    // Override Mime.
                                                    var fileMime = mime.Lookup(cachedFile);
                                                    switch (cachedFile.Split('.').Last().ToUpperInvariant())
                                                    {
                                                        case "HTML":
                                                        case "HTM":
                                                            fileMime = @"text/html";
                                                            break;
                                                    }
                                                    NucleusResponse.ContentType = fileMime;
                                                    break;
                                                }

                                                // If this is not a directory then it cannot be listed.
                                                if (!Directory.Exists(cachedFile))
                                                {
                                                    NucleusResponse.StatusCode = (int)HttpStatusCode.NotFound;
                                                    return;
                                                }

                                                using (var JSONStream =
                                                    new MemoryStream(
                                                        Encoding.UTF8.GetBytes(JsonSerializer.SerializeToString(
                                                            Directory.EnumerateFileSystemEntries(
                                                                    cachedFile)
                                                                .Select(o => o
                                                                    .Replace(Path.GetFullPath(cachedFile), string.Empty)
                                                                    .Trim(Path.DirectorySeparatorChar))))))
                                                {
                                                    JSONStream.CopyTo(dataMemoryStream);
                                                }
                                                NucleusResponse.ContentType = @"application/json";
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
                                            if (DateTime.TryParse(httpRequest.Headers["If-Modified-Since"],
                                                    out modified) &&
                                                DiskCacheExpiryTimer.ScheduledTime <= modified || string.Equals(
                                                    httpRequest.Headers["If-None-Match"] ?? string.Empty,
                                                    Encoding.UTF8.GetString(etagStream.ToArray())))
                                            {
                                                NucleusResponse.StatusCode = (int) HttpStatusCode.NotModified;
                                                return;
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
                                                case true
                                                : // No caching was chosen so tell the client to not cache the response.
                                                    switch (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                                    {
                                                        case true:
                                                            NucleusResponse.Headers.Set(HttpResponseHeader.CacheControl,
                                                                "no-cache, no-store, must-revalidate");
                                                            break;

                                                        default:
                                                            NucleusResponse.Headers.Set(HttpResponseHeader.Pragma,
                                                                "no-cache");
                                                            break;
                                                    }
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.Expires, "0");
                                                    break;

                                                default:
                                                    // Set the expires time of the resource depending on the nucleus rebuild schedule.
                                                    if (httpRequest.ProtocolVersion.Equals(HttpVersion.Version11))
                                                    {
                                                        NucleusResponse.Headers.Set(HttpResponseHeader.CacheControl,
                                                            $"max-age={DiskCacheExpiryTimer.DueTime.Seconds}, public");
                                                        etagStream.Position = 0;
                                                        NucleusResponse.Headers.Set(HttpResponseHeader.ETag,
                                                            Encoding.UTF8.GetString(etagStream.ToArray()));
                                                        break;
                                                    }
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.Expires,
                                                        DateTime.UtcNow.Add(DiskCacheExpiryTimer.DueTime)
                                                            .ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                                                    NucleusResponse.Headers.Set(HttpResponseHeader.LastModified,
                                                        DiskCacheExpiryTimer.ScheduledTime.ToString(
                                                            "ddd, dd MMM yyyy HH:mm:ss 'GMT'"));
                                                    break;
                                            }
                                            NucleusResponse.StatusCode = (int) HttpStatusCode.OK;

                                            outputStream.Position = 0;
                                            await outputStream.CopyToAsync(NucleusResponse.OutputStream)
                                                .ContinueWith(o => { ContentSent = true; });
                                        }
                                    }
                                }
                                catch (HTTPException ex)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        NucleusResponse.StatusCode = ex.StatusCode;
                                        NucleusResponse.Close();
                                    }
                                    throw;
                                }
                                catch (Exception)
                                {
                                    /* There was an error and it's our fault */
                                    if (!ContentSent)
                                    {
                                        NucleusResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                                        NucleusResponse.Close();
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
                                            DiskCacheExpiryTimer.Change(
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
                                        throw new HTTPException((int) HttpStatusCode.BadRequest);

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
                                                            JsonSerializer.SerializeToString(
                                                                Corrade.ProcessCommand(
                                                                    new Command.CorradeCommandParameters
                                                                    {
                                                                        Group = commandGroup,
                                                                        Identifier =
                                                                            httpRequest.RemoteEndPoint.ToString(),
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
                                                    Locks.ClientInstanceConfigurationLock.EnterWriteLock();
                                                    Corrade.corradeConfiguration.Groups.Add(commandGroup);
                                                    Locks.ClientInstanceConfigurationLock.ExitWriteLock();
                                                    data =
                                                        Encoding.UTF8.GetBytes(
                                                            JsonSerializer.SerializeToString(
                                                                Corrade.ProcessCommand(
                                                                    new Command.CorradeCommandParameters
                                                                    {
                                                                        Group = commandGroup,
                                                                        Identifier =
                                                                            httpRequest.RemoteEndPoint.ToString(),
                                                                        Message = message,
                                                                        Sender = "Nucleus"
                                                                    })));
                                                    Locks.ClientInstanceConfigurationLock.EnterWriteLock();
                                                    Corrade.corradeConfiguration.Groups.Remove(commandGroup);
                                                    Locks.ClientInstanceConfigurationLock.ExitWriteLock();
                                                    break;
                                            }
                                            break;

                                        default:
                                            data =
                                                Encoding.UTF8.GetBytes(
                                                    JsonSerializer.SerializeToString(
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
                                        NucleusResponse.StatusCode = (int) HttpStatusCode.OK;

                                        outputStream.Position = 0;
                                        await outputStream.CopyToAsync(NucleusResponse.OutputStream)
                                            .ContinueWith(o => { ContentSent = true; });
                                    }
                                }
                            }
                            catch (HTTPException ex)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = ex.StatusCode;
                                    NucleusResponse.Close();
                                }
                                throw;
                            }
                            catch (Exception)
                            {
                                /* There was an error and it's our fault */
                                if (!ContentSent)
                                {
                                    NucleusResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                                    NucleusResponse.Close();
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
                    ex.PrettyPrint());
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
                        throw new HTTPException((int) HttpStatusCode.Forbidden);
                }
            }
            catch
            {
                throw new HTTPException((int) HttpStatusCode.InternalServerError);
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
                    throw new HTTPException((int) HttpStatusCode.Forbidden);
                NucleusResponse.ContentType = mime.Lookup(Path.GetFileName(file));
                using (var fileStream = new FileStream(file, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 16384, true))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
            }
            catch
            {
                throw new HTTPException((int) HttpStatusCode.NotFound);
            }
        }

        [HTTPRequestMapping("fs", "PUT")]
        private async Task PutFile(string type, string file, Stream dataMemoryStream)
        {
            try
            {
                // if no blessings match the requested file then send a forbidden code.
                if (!blessingsRegExes.AsParallel().Any(o => o.IsMatch(file)))
                    throw new HTTPException((int) HttpStatusCode.Forbidden);
                using (var fileStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 16384,
                    true))
                {
                    await dataMemoryStream.CopyToAsync(fileStream);
                }
            }
            catch
            {
                throw new HTTPException((int) HttpStatusCode.NotFound);
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
                        using (var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE, FileMode.Open,
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
                        throw new HTTPException((int) HttpStatusCode.NotFound);
                }
            }
            catch
            {
                throw new HTTPException((int) HttpStatusCode.NotFound);
            }
        }

        [HTTPRequestMapping("e", "GET")]
        private async Task GetNotifications(string type, string group, string password,
            HttpListenerResponse NucleusResponse, MemoryStream memoryStream)
        {
            UUID groupUUID;
            var configuredGroup = UUID.TryParse(group, out groupUUID)
                ? Corrade.corradeConfiguration.Groups.AsParallel().FirstOrDefault(o => o.UUID.Equals(groupUUID))
                : Corrade.corradeConfiguration.Groups.AsParallel()
                    .FirstOrDefault(o => string.Equals(group, o.Name, StringComparison.OrdinalIgnoreCase));
            if (configuredGroup == null ||
                configuredGroup.Equals(default(Configuration.Group)) ||
                !Corrade.Authenticate(configuredGroup.UUID, password))
                throw new HTTPException((int) HttpStatusCode.Forbidden);

            try
            {
                NucleusResponse.ContentType = @"application/json";
                BlockingQueue<NotificationQueueElement> nucleusNotification = null;
                lock (Corrade.NucleusNotificationQueueLock)
                {
                    nucleusNotification = Corrade.NucleusNotificationQueue[configuredGroup.UUID];
                }
                using (var notificationStream =
                    new MemoryStream(Encoding.UTF8.GetBytes(
                        JsonSerializer.SerializeToString(nucleusNotification))))
                {
                    await notificationStream.CopyToAsync(memoryStream);
                }
            }
            catch
            {
                throw new HTTPException((int) HttpStatusCode.NotFound);
            }
        }

        /*public class CoreFile
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public byte[] Data { get; set; }

            public TimeSpan CacheExpire { get; set; } = TimeSpan.Zero;

            public DateTime CachedTime { get; set; } = DateTime.UtcNow;
        }*/
    }
}