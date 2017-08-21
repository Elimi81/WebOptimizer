﻿using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace WebOptimizer
{
    internal class ETagMiddleware
    {
        private readonly RequestDelegate _next;

        public ETagMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Method != "GET" && context.Request.Method != "HEAD")
            {
                await _next(context);
                return;
            }

            HttpResponse response = context.Response;
            Stream originalStream = response.Body;

            using (var ms = new MemoryStream())
            {
                response.Body = ms;

                await _next(context);

                if (IsEtagSupported(response))
                {
                    string checksum = CalculateChecksum(ms);

                    response.Headers[HeaderNames.ETag] = checksum;

                    if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var etag) && checksum == etag)
                    {
                        response.StatusCode = StatusCodes.Status304NotModified;
                        return;
                    }
                }

                ms.Position = 0;
                await ms.CopyToAsync(originalStream);
            }
        }

        private static bool IsEtagSupported(HttpResponse response)
        {
            if (response.StatusCode != StatusCodes.Status200OK)
                return false;

            if (response.Body.Length > 20 * 1024)
                return false;

            if (response.Headers.ContainsKey(HeaderNames.ETag))
                return false;

            return true;
        }

        private static string CalculateChecksum(MemoryStream ms)
        {
            string checksum = "";

            using (var algo = SHA1.Create())
            {
                ms.Position = 0;
                byte[] bytes = algo.ComputeHash(ms);
                checksum = $"\"{WebEncoders.Base64UrlEncode(bytes)}\"";
            }

            return checksum;
        }
    }

}
