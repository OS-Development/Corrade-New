///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace wasSharp
{
    public static class Web
    {
        private static readonly Func<string, string> directURIEscapeDataString =
            ((Expression<Func<string, string>>)
                (data => string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766).AsParallel()
                    .Select(o => Uri.EscapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - o*32766))))
                    .ToArray()))).Compile();

        private static readonly Func<string, string> directURIUnescapeDataString =
            ((Expression<Func<string, string>>)
                (data => string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766).AsParallel()
                    .Select(
                        o => Uri.UnescapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - o*32766))))
                    .ToArray()))).Compile();

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC3986 URI Escapes a string</summary>
        /// <remarks>
        ///     data - a string to escape
        /// </remarks>
        /// <returns>an RFC3986 escaped string</returns>
        public static string URIEscapeDataString(string data)
        {
            return directURIEscapeDataString(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>URI unescapes an RFC3986 URI escaped string</summary>
        /// <remarks>
        ///     data - a string to unescape
        /// </remarks>
        /// <returns>the resulting string</returns>
        public static string URIUnescapeDataString(string data)
        {
            return directURIUnescapeDataString(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC1738 escaped string</returns>
        public static string URLEscapeDataString(string data)
        {
            return WebUtility.UrlEncode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Unescape a string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>an RFC1738 unescaped string</returns>
        public static string URLUnescapeDataString(string data)
        {
            return WebUtility.UrlDecode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        // <summary>A portable HTTP client.</summar>
        public class wasHTTPClient
        {
            private readonly HttpClient HTTPClient;
            private readonly string mediaType;

            public wasHTTPClient(ProductInfoHeaderValue userAgent, CookieContainer cookieContainer, string mediaType,
                uint millisecondsTimeout)
            {
                var HTTPClientHandler = new HttpClientHandler
                {
                    CookieContainer = cookieContainer,
                    UseCookies = true
                };
                if (HTTPClientHandler.SupportsRedirectConfiguration)
                {
                    HTTPClientHandler.AllowAutoRedirect = true;
                }
                if (HTTPClientHandler.SupportsProxy)
                {
                    HTTPClientHandler.Proxy = WebRequest.DefaultWebProxy;
                    HTTPClientHandler.UseProxy = true;
                }
                if (HTTPClientHandler.SupportsAutomaticDecompression)
                {
                    HTTPClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }
                HTTPClientHandler.ClientCertificateOptions = ClientCertificateOption.Automatic;

                HTTPClient = new HttpClient(HTTPClientHandler, false);
                HTTPClient.DefaultRequestHeaders.UserAgent.Add(userAgent);
                HTTPClient.Timeout = TimeSpan.FromMilliseconds(millisecondsTimeout);
                this.mediaType = mediaType;
            }

            ///////////////////////////////////////////////////////////////////////////
            //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
            ///////////////////////////////////////////////////////////////////////////
            /// <summary>
            ///     Sends a POST request to an URL with set key-value pairs.
            /// </summary>
            /// <param name="URL">the url to send the message to</param>
            /// <param name="message">key-value pairs to send</param>
            public async Task<byte[]> POST(string URL, Dictionary<string, string> message)
            {
                try
                {
                    using (var content =
                        new StringContent(KeyValue.Encode(message), Encoding.UTF8, mediaType))
                    {
                        using (var response = await HTTPClient.PostAsync(URL, content))
                        {
                            return response.IsSuccessStatusCode
                                ? await response.Content.ReadAsByteArrayAsync()
                                : null;
                        }
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////////////
            //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
            ///////////////////////////////////////////////////////////////////////////
            /// <summary>
            ///     Sends a GET request to an URL with set key-value pairs.
            /// </summary>
            /// <param name="URL">the url to send the message to</param>
            /// <param name="message">key-value pairs to send</param>
            public async Task<byte[]> GET(string URL, Dictionary<string, string> message)
            {
                try
                {
                    using (var response =
                        await HTTPClient.GetAsync(URL + "?" + KeyValue.Encode(message)))
                    {
                        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}