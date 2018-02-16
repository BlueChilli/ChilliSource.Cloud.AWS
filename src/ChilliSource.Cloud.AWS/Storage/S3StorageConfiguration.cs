using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;

namespace ChilliSource.Cloud.AWS
{
    public class S3StorageConfiguration
    {
        public string Host { get; internal set; }
        public string AccessKeyId { get; internal set; }
        public string SecretAccessKey { get; internal set; }
        public string Bucket { get; internal set; }
    }
}
