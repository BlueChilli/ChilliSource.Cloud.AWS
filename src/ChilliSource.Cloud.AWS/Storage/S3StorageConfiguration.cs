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
        public string Host { get; set; }
        public string AccessKeyId { get; set; }
        public string SecretAccessKey { get; set; }
        public string Bucket { get; set; }
    }
}
