#if !NET_4X
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Providers;
using SixLabors.ImageSharp.Web.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ChilliSource.Cloud.AWS.ImageSharp
{
    public class AWSImageProvider : IImageProvider
    {
        AwsImageProviderOptions _options;
        PathString _pathPrefix;

        public AWSImageProvider(IOptions<AwsImageProviderOptions> optionsAcessor)
        {
            _options = optionsAcessor.Value;

            if (_options.StorageConfiguration == null)
            {
                throw new ArgumentNullException("StorageConfiguration is required.");
            }

            if (_options.UrlPrefix == null || !_options.UrlPrefix.StartsWith("~"))
            {
                throw new ArgumentException("UrlPrefix is null or is not a relative path (~).");
            }

            if (_options.UrlPrefix.StartsWith("~"))
            {
                _pathPrefix = new PathString(_options.UrlPrefix.TrimStart('~'));
            }
            else
            {
                _pathPrefix = new PathString(_options.UrlPrefix);
            }

            this.Match = this.IsAProviderMatch;
        }

        public Func<HttpContext, bool> Match { get; set; }

        public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        protected bool IsAProviderMatch(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments(_pathPrefix);
        }

        private string GetS3FileName(HttpContext context)
        {
            return context.Request.Path.Value.Substring(_pathPrefix.Value.Length).TrimStart('/');
        }

        public bool IsValidRequest(HttpContext context)
        {
            return true;
        }

        public async Task<IImageResolver> GetAsync(HttpContext context)
        {
            var remoteStorage = new S3RemoteStorage(_options.StorageConfiguration);
            var fileName = GetS3FileName(context);
            if (String.IsNullOrEmpty(fileName))
                return null;

            var metadata = await remoteStorage.GetMetadata(fileName);
            if (metadata != null)
            {
                return new AWSImageResolver(remoteStorage, fileName, metadata);
            }

            return null;
        }
    }
}
#endif