#if !NET_4X
using Amazon.S3.Model;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ChilliSource.Cloud.AWS.ImageSharp
{
    internal class AWSImageResolver : IImageResolver
    {
        S3RemoteStorage _storage;
        string _fileName;
        GetObjectMetadataResponse _s3Metadata;

        public AWSImageResolver(S3RemoteStorage storage, string fileName, GetObjectMetadataResponse s3Metadata)
        {
            _storage = storage;
            _fileName = fileName;
            _s3Metadata = s3Metadata;
        }

        public Task<ImageMetaData> GetMetaDataAsync()
        {
            CacheControlHeaderValue cacheControl = null;
            CacheControlHeaderValue.TryParse(_s3Metadata.Headers.CacheControl, out cacheControl);

            var metadata = new ImageMetaData(
                _s3Metadata.LastModified.ToUniversalTime(),
                _s3Metadata.Headers.ContentType,
                cacheControl?.MaxAge ?? TimeSpan.MinValue
            );

            return Task.FromResult<ImageMetaData>(metadata);
        }

        public async Task<Stream> OpenReadAsync()
        {
            var response = await _storage.GetContentAsync(_fileName);
            if (response == null)
                throw new ApplicationException("AWSImageResolver.OpenReadAsync failed to find file.");

            return response.Stream;
        }
    }
}
#endif