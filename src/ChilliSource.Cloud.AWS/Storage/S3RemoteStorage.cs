using Amazon.S3;
using Amazon.S3.Model;
using ChilliSource.Cloud.Core;
using ChilliSource.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChilliSource.Cloud.AWS
{
    public class S3RemoteStorage : IRemoteStorage
    {
        S3StorageConfiguration _s3Config;
        Func<S3StorageConfiguration, IAmazonS3> _clientFactory;

        public S3RemoteStorage(S3StorageConfiguration s3Config)
            : this(s3Config, DefaultClientFactory)
        {
        }

        public S3RemoteStorage(S3StorageConfiguration s3Config, Func<S3StorageConfiguration, IAmazonS3> clientFactory)
        {
            if (s3Config == null)
            {
                throw new ArgumentNullException("s3Config is required.");
            }

            if (clientFactory == null)
            {
                throw new ArgumentNullException("client Factory is required");
            }

            _s3Config = s3Config;
            _clientFactory = clientFactory;
        }

        private static IAmazonS3 DefaultClientFactory(S3StorageConfiguration s3Config)
        {
            var host = String.IsNullOrEmpty(s3Config.Host) ? "https://s3.amazonaws.com" : s3Config.Host;
            if (!host.StartsWith("http"))
                host = $"https://{host}";

            return new AmazonS3Client(s3Config.AccessKeyId, s3Config.SecretAccessKey, new AmazonS3Config()
            {
                ServiceURL = host
            });
        }

        private IAmazonS3 GetClient()
        {
            return _clientFactory(_s3Config);
        }

        private static string EncodeKey(string key)
        {
            return key?.Replace('\\', '/');
        }

        private PutObjectRequest CreatePutRequest(Stream stream, string fileName, string contentType)
        {
            return new PutObjectRequest()
            {
                BucketName = _s3Config.Bucket,
                Key = EncodeKey(fileName),
                ContentType = contentType,
                InputStream = stream,
                AutoCloseStream = false
            };
        }

#if NET_4X
        public async Task DeleteAsync(string fileToDelete)
        {
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task DeleteAsync(string fileToDelete, CancellationToken cancellationToken)
        {
#endif
            using (var s3Client = GetClient())
            {
                try
                {
                    await s3Client.DeleteObjectAsync(_s3Config.Bucket, EncodeKey(fileToDelete), cancellationToken)
                          .IgnoreContext();
                }
                catch (AmazonS3Exception ex)
                {
                    if (ex.StatusCode != HttpStatusCode.NotFound)
                        throw;
                }
            }
        }

#if NET_4X
        public async Task<FileStorageResponse> GetContentAsync(string fileName)
        {
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task<FileStorageResponse> GetContentAsync(string fileName, CancellationToken cancellationToken)
        {
#endif
            IAmazonS3 s3Client = null;
            GetObjectResponse response = null;

            try
            {
                s3Client = GetClient();
                response = await s3Client.GetObjectAsync(_s3Config.Bucket, EncodeKey(fileName), cancellationToken)
                                            .IgnoreContext();

                var contentLength = response.Headers.ContentLength;
                var contentType = response.Headers.ContentType;

                Action<Stream> disposingAction = (s) =>
                {
                    response?.Dispose(); //also disposes ResponseStream
                    s3Client?.Dispose();
                };

                var readonlyStream = ReadOnlyStreamWrapper.Create(response.ResponseStream, disposingAction, contentLength);

                return FileStorageResponse.Create(fileName, contentLength, contentType, readonlyStream);
            }
            catch
            {
                response?.Dispose();
                s3Client?.Dispose();

                throw;
            }
        }

#if NET_4X
        public async Task SaveAsync(Stream stream, string fileName, string contentType)
        {
            using (var s3Client = GetClient())
            {
                var putRequest = CreatePutRequest(stream, fileName, contentType);
                var response = await s3Client.PutObjectAsync(putRequest, CancellationToken.None)
                                     .IgnoreContext();
            }
        }
#else
        public async Task SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
        {
            await SaveAsync(stream, new FileStorageMetadataInfo()
            {
                FileName = fileName,
                ContentType = contentType
            }, cancellationToken);
        }

        public async Task SaveAsync(Stream stream, FileStorageMetadataInfo metadata, CancellationToken cancellationToken)
        {
            using (var s3Client = GetClient())
            {
                var putRequest = CreatePutRequest(stream, metadata.FileName, metadata.ContentType);
                if (!String.IsNullOrEmpty(metadata.CacheControl))
                {
                    putRequest.Headers.CacheControl = metadata.CacheControl;
                }

                if (!String.IsNullOrEmpty(metadata.ContentDisposition))
                {
                    putRequest.Headers.ContentDisposition = metadata.ContentDisposition;
                }

                if (!String.IsNullOrEmpty(metadata.ContentEncoding))
                {
                    putRequest.Headers.ContentEncoding = metadata.ContentEncoding;
                }

                var response = await s3Client.PutObjectAsync(putRequest, cancellationToken)
                                     .IgnoreContext();
            }
        }
#endif


        private async Task<GetObjectMetadataResponse> GetMetadataInternalAsync(string fileName, CancellationToken cancellationToken)
        {
            using (var s3Client = GetClient())
            {
                try
                {
                    var request = new GetObjectMetadataRequest()
                    {
                        BucketName = _s3Config.Bucket,
                        Key = EncodeKey(fileName)
                    };

                    return await s3Client.GetObjectMetadataAsync(request, cancellationToken)
                                         .IgnoreContext();
                }
                catch (AmazonS3Exception ex)
                {
                    if (!string.Equals(ex.ErrorCode, "NoSuchBucket") && (ex.StatusCode == HttpStatusCode.NotFound))
                    {
                        return null;
                    }
                    throw;
                }
            }
        }

#if NET_4X
        public async Task<bool> ExistsAsync(string fileName)
        {
            CancellationToken cancellationToken = CancellationToken.None;
#else
        public async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
#endif
            return (await GetMetadataInternalAsync(fileName, cancellationToken).IgnoreContext()) != null;
        }

#if NET_4X
        [Obsolete]
        public string GetPartialFilePath(string fileName)
        {
            return $"{_s3Config.Bucket}/{fileName}";
        }
#else
        public async Task<IFileStorageMetadataResponse> GetMetadataAsync(string fileName, CancellationToken cancellationToken)
        {
            var s3Metadata = await GetMetadataInternalAsync(fileName, cancellationToken);
            var headers = s3Metadata.Headers;

            var metadata = new FileStorageMetadataResponse()
            {
                FileName = fileName,
                CacheControl = headers.CacheControl,
                ContentDisposition = headers.ContentDisposition,
                ContentEncoding = headers.ContentEncoding,
                ContentLength = headers.ContentLength,
                ContentType = headers.ContentType,
                LastModifiedUtc = s3Metadata.LastModified.ToUniversalTime()
            };

            return metadata;
        }
#endif

    }
}
