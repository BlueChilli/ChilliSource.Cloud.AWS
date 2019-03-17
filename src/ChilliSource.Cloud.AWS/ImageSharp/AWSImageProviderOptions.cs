#if !NET_4X
using System;
using System.Collections.Generic;

namespace ChilliSource.Cloud.AWS.ImageSharp
{
    public class AWSImageProviderOptions
    {
        public S3StorageConfiguration StorageConfiguration { get; set; }
        public string UrlPrefix { get; set; }
    }
}
#endif