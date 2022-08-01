using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using ChilliSource.Cloud.Core;
using Amazon.Runtime;
using System.Text;

namespace ChilliSource.Cloud.AWS.Tests
{
    public class S3StorageTests
    {
        private readonly Mock<IAmazonS3> _amazonS3Client;
        private readonly Mock<S3StorageConfiguration> _s3Element;
        private readonly S3RemoteStorage _s3RemoteStorage;
        public S3StorageTests()
        {
            if (_amazonS3Client == null)
                _amazonS3Client = new Mock<IAmazonS3>();

            if (_s3Element == null)
                _s3Element = new Mock<S3StorageConfiguration>();

            if (_s3RemoteStorage == null)
                _s3RemoteStorage = CreateRemoteStorageInstance();
        }

        private S3RemoteStorage CreateRemoteStorageInstance()
        {
            var s3element = new S3StorageConfiguration() { AccessKeyId = "", SecretAccessKey = "", Bucket = "" };
            return new S3RemoteStorage(s3element, x => CreateMockS3Factory(s3element));
        }

        private IAmazonS3 CreateMockS3Factory(S3StorageConfiguration arg)
        {
            return _amazonS3Client.Object;
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteFile()
        {
            var _deleteObjectResponse = new Mock<DeleteObjectResponse>();
            _amazonS3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default(CancellationToken)))
               .Returns(Task<DeleteObjectResponse>.FromResult<DeleteObjectResponse>(_deleteObjectResponse.Object))
               .Verifiable();

            await _s3RemoteStorage.DeleteAsync("testfile.txt", CancellationToken.None);

            _deleteObjectResponse.Verify();
            _amazonS3Client.Verify();
        }
        
        [Fact]
        public async Task GetContentAsync_ShouldReturnFile()
        {
           var _getObjectResponse = new Mock<GetObjectResponse>();
            _amazonS3Client.Setup(x => x.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default(CancellationToken)))
                .Returns(Task<GetObjectResponse>.FromResult<GetObjectResponse>(_getObjectResponse.Object))
                .Verifiable();

            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream))
            {
                const string Text = "this is a test file";
                await writer.WriteAsync(Text);
                await writer.FlushAsync();


                _getObjectResponse.Object.Headers.ContentLength = Text.Length;
                _getObjectResponse.Object.Headers.ContentType = "text/plain";
                _getObjectResponse.Object.ResponseStream = stream;

                await _s3RemoteStorage.GetContentAsync("testfile.txt", CancellationToken.None);

                _amazonS3Client.Verify();
            }
        }

        [Fact]
        public async Task SaveAsync_ShouldSaveFile()
        {
            var fakePackageFile = new MemoryStream();
            var _putObjectResponse = new Mock<PutObjectResponse>();
            _amazonS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken)))
               .Returns(Task<PutObjectResponse>.FromResult<PutObjectResponse>(_putObjectResponse.Object))
               .Verifiable();

            await _s3RemoteStorage.SaveAsync(fakePackageFile, "testfile.txt", "text/plain", CancellationToken.None);

            _amazonS3Client.Verify();
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnTrueIfFileExists()
        {
            var _getObjectMetadataResponse = new Mock<GetObjectMetadataResponse>();
            _amazonS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), default(CancellationToken)))
               .Returns(Task<GetObjectMetadataResponse>.FromResult<GetObjectMetadataResponse>(_getObjectMetadataResponse.Object))
               .Verifiable();

            await _s3RemoteStorage.ExistsAsync("testfile.txt", CancellationToken.None);

            _getObjectMetadataResponse.Verify();
        }

        [Fact]
        public async Task ExistsAsync_WillReturnsFalseIfFileDoesNotExist()
        {
            _amazonS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default(CancellationToken)))
                .ThrowsAsync(new AmazonS3Exception(string.Empty, ErrorType.Sender, string.Empty, string.Empty, HttpStatusCode.NotFound))
                .Verifiable();

            var exists = await _s3RemoteStorage.ExistsAsync("testfile.txt", CancellationToken.None);
            Assert.False(exists);
            _amazonS3Client.Verify();
        }

        [Fact]
        public async Task ExistsAsync_ShouldThrowIfNoSuchBucketExists()
        {
            _amazonS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default(CancellationToken)))
                .Throws(new AmazonS3Exception("Test", ErrorType.Unknown, "NoSuchBucket", string.Empty, HttpStatusCode.NotFound))
                .Verifiable();

            await Assert.ThrowsAsync<AmazonS3Exception>(() => _s3RemoteStorage.ExistsAsync("NoSuchBucket", CancellationToken.None));
        }

        //Used only for internal testing
        //[Fact]
        //public void TestRealStorage()
        //{            
        //    var remoteStorage = new S3RemoteStorage(new S3StorageConfiguration()
        //    {
        //        AccessKeyId = "AKIASGRSJEVGPQC67DI2",
        //        SecretAccessKey = "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",
        //        Bucket = "chillibucket"
        //    });

        //    IFileStorage storage = FileStorageFactory.Create(remoteStorage);

        //    string contentType;
        //    long length;

        //    var fileName = $"{Guid.NewGuid()}.txt";

        //    Assert.False(storage.Exists(fileName));

        //    var savedPath = storage.Save(new StorageCommand()
        //    {                
        //        FileName = fileName,           
        //        ContentType = "text/plain"
        //    }.SetStreamSource(GetSampleMemoryStream("this is a sample.")));

        //    Assert.True(!String.IsNullOrEmpty(savedPath));

        //    Assert.True(storage.Exists(savedPath));

        //    var file = storage.GetContent(savedPath, null, out length, out contentType);
        //    byte[] bytes = new byte[length];
        //    file.Read(bytes, 0, (int)length);
        //    var downloadedStr = Encoding.ASCII.GetString(bytes);

        //    Assert.Equal("this is a sample.", downloadedStr);

        //    Assert.Equal("text/plain", contentType);

        //    storage.Delete(savedPath);

        //    Assert.False(storage.Exists(fileName));
        //}

        //public MemoryStream GetSampleMemoryStream(string text)
        //{
        //    var source = new MemoryStream();
        //    var bytes = Encoding.ASCII.GetBytes(text);
        //    source.Write(bytes, 0, bytes.Length);

        //    source.Position = 0;
        //    return source;
        //}
    }
}