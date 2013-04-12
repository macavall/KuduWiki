﻿using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Kudu.Common;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Services.ByteRanges;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Editor
{
    /// <summary>
    /// A Virtual File System controller which exposes GET, PUT, and DELETE for the entire Kudu file system.
    /// </summary>
    public class VfsController : VfsControllerBase
    {
        public VfsController(ITracer tracer, IEnvironment environment)
            : base(tracer, environment, environment.RootPath)
        {
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfo info, string localFilePath)
        {
            // Get current etag
            EntityTagHeaderValue currentEtag = GetCurrentEtag(info);

            // Check whether we have a conditional If-None-Match request
            if (IsIfNoneMatchRequest(currentEtag))
            {
                HttpResponseMessage notModifiedResponse = Request.CreateResponse(HttpStatusCode.NotModified);
                notModifiedResponse.Headers.ETag = currentEtag;
                return Task.FromResult(notModifiedResponse);
            }

            // Check whether we have a conditional range request containing both a Range and If-Range header field
            bool isRangeRequest = IsRangeRequest(currentEtag);

            // Generate file response
            Stream fileStream = null;
            try
            {
                fileStream = GetFileReadStream(localFilePath, validate: info);
                MediaTypeHeaderValue mediaType = MediaTypeMap.GetMediaType(info.Extension);
                HttpResponseMessage successFileResponse = Request.CreateResponse(isRangeRequest ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

                if (isRangeRequest)
                {
                    successFileResponse.Content = new ByteRangeStreamContent(fileStream, Request.Headers.Range, mediaType, BufferSize);
                }
                else
                {
                    successFileResponse.Content = new StreamContent(fileStream, BufferSize);
                    successFileResponse.Content.Headers.ContentType = mediaType;
                }

                // Set etag for the file
                successFileResponse.Headers.ETag = currentEtag;
                return Task.FromResult(successFileResponse);
            }
            catch (InvalidByteRangeException invalidByteRangeException)
            {
                // The range request had no overlap with the current extend of the resource so generate a 416 (Requested Range Not Satisfiable)
                // including a Content-Range header with the current size.
                Tracer.TraceError(invalidByteRangeException);
                HttpResponseMessage invalidByteRangeResponse = Request.CreateErrorResponse(invalidByteRangeException);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(invalidByteRangeResponse);
            }
            catch (Exception e)
            {
                // Could not read the file
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, e);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(errorResponse);
            }
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfo info, string localFilePath, bool itemExists)
        {
            // Check that we have a matching conditional If-Match request for existing resources
            if (itemExists)
            {
                // Get current etag
                EntityTagHeaderValue currentEtag = GetCurrentEtag(info);

                // Existing resources require an etag to be updated.
                if (Request.Headers.IfMatch == null)
                {
                    HttpResponseMessage missingIfMatchResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed, Resources.VfsController_MissingIfMatch);
                    return Task.FromResult(missingIfMatchResponse);
                }

                bool isMatch = false;
                foreach (EntityTagHeaderValue etag in Request.Headers.IfMatch)
                {
                    if (currentEtag.Equals(etag) || etag == EntityTagHeaderValue.Any)
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                {
                    HttpResponseMessage conflictFileResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed, Resources.VfsController_EtagMismatch);
                    conflictFileResponse.Headers.ETag = currentEtag;
                    return Task.FromResult(conflictFileResponse);
                }
            }

            // Save file
            Stream fileStream = null;
            try
            {
                fileStream = GetFileWriteStream(localFilePath, fileExists: itemExists, validate: info);
                return Request.Content.CopyToAsync(fileStream)
                    .Then(() =>
                    {
                        // Successfully saved the file
                        fileStream.Close();
                        fileStream = null;

                        // Return either 204 No Content or 201 Created response
                        HttpResponseMessage successFileResponse =
                            Request.CreateResponse(itemExists ? HttpStatusCode.NoContent : HttpStatusCode.Created);

                        // Set updated etag for the file
                        successFileResponse.Headers.ETag = GetUpdatedEtag(localFilePath);
                        return successFileResponse;
                    })
                    .Catch((catchInfo) =>
                    {
                        Tracer.TraceError(catchInfo.Exception);
                        HttpResponseMessage conflictResponse = Request.CreateErrorResponse(
                            HttpStatusCode.Conflict,
                            RS.Format(Resources.VfsController_WriteConflict, localFilePath),
                            catchInfo.Exception);

                        if (fileStream != null)
                        {
                            fileStream.Close();
                        }

                        return catchInfo.Handled(conflictResponse);
                    });

            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse =
                    Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    RS.Format(Resources.VfsController_WriteConflict, localFilePath), e);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(errorResponse);
            }
        }

        protected override Task<HttpResponseMessage> CreateItemDeleteResponse(FileSystemInfo info, string localFilePath)
        {
            // Get current etag
            EntityTagHeaderValue currentEtag = GetCurrentEtag(info);

            // Existing resources require an etag to be updated.
            if (Request.Headers.IfMatch == null)
            {
                HttpResponseMessage conflictDirectoryResponse = Request.CreateErrorResponse(
                    HttpStatusCode.PreconditionFailed, Resources.VfsController_MissingIfMatch);
                return Task.FromResult(conflictDirectoryResponse);
            }

            bool isMatch = false;
            foreach (EntityTagHeaderValue etag in Request.Headers.IfMatch)
            {
                if (currentEtag.Equals(etag) || etag == EntityTagHeaderValue.Any)
                {
                    isMatch = true;
                    break;
                }
            }

            if (!isMatch)
            {
                HttpResponseMessage conflictFileResponse = Request.CreateErrorResponse(
                    HttpStatusCode.PreconditionFailed, Resources.VfsController_EtagMismatch);
                conflictFileResponse.Headers.ETag = currentEtag;
                return Task.FromResult(conflictFileResponse);
            }

            return base.CreateItemDeleteResponse(info, localFilePath);
        }

        private static EntityTagHeaderValue GetCurrentEtag(FileSystemInfo info)
        {
            return CreateEntityTag(info);
        }

        private static EntityTagHeaderValue GetUpdatedEtag(string localFilePath)
        {
            FileInfo fInfo = new FileInfo(localFilePath);
            return CreateEntityTag(fInfo);
        }

        /// <summary>
        /// Create unique etag based on the last modified UTC time
        /// </summary>
        public static EntityTagHeaderValue CreateEntityTag(FileSystemInfo sysInfo)
        {
            Contract.Assert(sysInfo != null);
            byte[] etag = BitConverter.GetBytes(sysInfo.LastWriteTimeUtc.Ticks);

            StringBuilder result = new StringBuilder();
            result.Append("\"");
            foreach (byte b in etag)
            {
                result.AppendFormat("{0:x2}", b);
            }
            result.Append("\"");
            return new EntityTagHeaderValue(result.ToString());
        }
    }
}
