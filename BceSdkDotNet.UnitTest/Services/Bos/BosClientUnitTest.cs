// Copyright (c) 2014 Baidu.com, Inc. All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.

using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BaiduBce.Services.Bos;
using BaiduBce.Services.Bos.Model;
using BaiduBce.Auth;
using BaiduBce.Util;
using Xunit;

namespace BaiduBce.UnitTest.Services.Bos
{
    /// <summary>
    /// Summary description for BosClientUnitTest
    /// </summary>
    public class BosClientUnitTest
    {
        public class Base : BceClientUnitTestBase, IDisposable
        {
            protected static readonly string BucketPrefix = "ut-net-" + new Random().Next().ToString("X") + "-";

            protected string bucketName;

            protected User owner;

            protected Grantee grantee;

            protected Grantee anonymous;

            protected BceClientConfiguration config;

            protected BosClient client;

            public Base()
            {
                this.bucketName = (BucketPrefix + new Random().Next().ToString("X")).ToLower();
                this.owner = new User() {Id = this.userId, DisplayName = "PASSPORT:105015426"};
                this.grantee = new Grantee() {Id = this.userId};
                this.anonymous = new Grantee() {Id = "*"};
                this.config = new BceClientConfiguration();
                this.config.Credentials = new DefaultBceCredentials(this.ak, this.sk);
                this.config.Endpoint = this.endpoint;
                this.client = new BosClient(this.config);
                this.client.CreateBucket(this.bucketName);
            }

            public void Dispose()
            {
                this.client = new BosClient(this.config);
                List<BucketSummary> buckets = this.client.ListBuckets().Buckets;
                if (buckets == null || buckets.Count == 0)
                {
                    return;
                }

                foreach (BucketSummary bucket in buckets)
                {
                    string bucketName = bucket.Name;
                    if (bucketName.StartsWith("ut"))
                    {
                        ListObjectsResponse listObjectsResponse = this.client.ListObjects(bucketName);
                        List<BosObjectSummary> objects = listObjectsResponse.Contents;
                        if (objects != null && objects.Count > 0)
                        {
                            foreach (BosObjectSummary bosObject in objects)
                            {
                                String key = bosObject.Key;
                                this.client.DeleteObject(bucketName, key);
                            }
                        }

                        this.client.DeleteBucket(bucket.Name);
                    }
                }
            }
        }

        public class CommonTest : Base
        {
            [Fact]
            public void TestRequestWithInvalidCredential()
            {
                BceClientConfiguration bceClientConfiguration = new BceClientConfiguration();
                bceClientConfiguration.Credentials = new DefaultBceCredentials("test", "test");
                bceClientConfiguration.Endpoint = this.endpoint;
                this.client = new BosClient(bceClientConfiguration);
                Assert.Throws<BceServiceException>(() => this.client.ListBuckets());
            }

            [Fact]
            public void TestMimetypes()
            {
                Assert.Equal("image/png", MimeTypes.GetMimetype("png"));
                Assert.Equal("application/srgs", MimeTypes.GetMimetype("gram"));
                Assert.Equal(MimeTypes.MimeTypeOctetStream, MimeTypes.GetMimetype(""));
            }
        }

        public class BucketTest : Base
        {
            [Fact]
            public void TestListBuckets()
            {
                var listBucketsResponse = this.client.ListBuckets();
                Assert.True(listBucketsResponse.Buckets.Count > 0);
            }

            [Fact]
            public void TestDoesBucketExist()
            {
                Assert.True(this.client.DoesBucketExist(this.bucketName));
                Assert.False(this.client.DoesBucketExist("xxxaaa"));
            }

            [Fact]
            public void TestGetBucketLocation()
            {
                GetBucketLocationResponse getBucketLocationResponse = this.client.GetBucketLocation(this.bucketName);
                Assert.Equal("bj", getBucketLocationResponse.LocationConstraint);
            }
        }

        public class SetBucketAclTest : Base
        {
            [Fact]
            public void TestPublicReadWrite()
            {
                string objectKey = "objectPublicReadWrite";
                string data = "dataPublicReadWrite";

                this.client.SetBucketAcl(this.bucketName, BosConstants.CannedAcl.PublicReadWrite);
                GetBucketAclResponse response = this.client.GetBucketAcl(this.bucketName);
                Assert.Equal(response.Owner.Id, this.grantee.Id);

                List<Grant> grants = new List<Grant>();
                List<Grantee> granteeOwner = new List<Grantee>();
                granteeOwner.Add(this.grantee);
                List<string> permissionOwner = new List<string>();
                permissionOwner.Add(BosConstants.Permission.FullControl);
                grants.Add(new Grant() {Grantee = granteeOwner, Permission = permissionOwner});
                List<Grantee> granteeAnonymous = new List<Grantee>();
                granteeAnonymous.Add(this.anonymous);
                List<string> permissionAnonymous = new List<string>();
                permissionAnonymous.Add(BosConstants.Permission.Read);
                permissionAnonymous.Add(BosConstants.Permission.Write);
                grants.Add(new Grant() {Grantee = granteeAnonymous, Permission = permissionAnonymous});

                Assert.Equal(response.AccessControlList.Count, grants.Count);
                this.client.PutObject(this.bucketName, objectKey, data);
                BceClientConfiguration bceClientConfiguration = new BceClientConfiguration();
                bceClientConfiguration.Endpoint = this.endpoint;
                BosClient bosAnonymous = new BosClient(bceClientConfiguration);
                Assert.Equal(
                    Encoding.Default.GetString(bosAnonymous.GetObjectContent(this.bucketName, objectKey)), data);

                bosAnonymous.PutObject(this.bucketName, "anonymous", "dataAnonymous");
                Assert.Equal(
                    "dataAnonymous",
                    Encoding.Default.GetString(bosAnonymous.GetObjectContent(this.bucketName, "anonymous")));
            }
        }

        public class GeneratePresignedUrlTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string objectKey = "test";
                string value = "value1" + "\n" + "value2";
                this.client.PutObject(this.bucketName, objectKey, value);
                GeneratePresignedUrlRequest request = new GeneratePresignedUrlRequest()
                {
                    BucketName = this.bucketName,
                    Key = objectKey,
                    Method = BceConstants.HttpMethod.Get
                };
                request.ExpirationInSeconds = 1800;
                Uri url = this.client.GeneratePresignedUrl(request);
                using (WebClient webClient = new WebClient())
                {
                    using (Stream stream = webClient.OpenRead(url))
                    using (StreamReader streamReader = new StreamReader(stream))
                    {
                        string response = streamReader.ReadToEnd();
                        Assert.Equal(response, value);
                    }
                }
            }
        }

        public class CopyObjectTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string objectName = "sample";
                client.PutObject(bucketName, objectName, "sampledata");

                // 2. 普通拷贝并打印结果
                string newObjectName = "copyobject";
                CopyObjectResponse copyObjectResponse = client.CopyObject(bucketName, objectName, bucketName,
                    newObjectName);
                // sampledata
                Assert.Equal("sampledata",
                    Encoding.Default.GetString(client.GetObjectContent(bucketName, newObjectName)));

                // 3. 拷贝并设置新的meta
                newObjectName = "copyobject-newmeta";
                CopyObjectRequest copyObjectRequest = new CopyObjectRequest()
                {
                    SourceBucketName = bucketName,
                    SourceKey = objectName,
                    BucketName = bucketName,
                    Key = newObjectName
                };
                Dictionary<String, String> userMetadata = new Dictionary<String, String>();
                userMetadata["metakey"] = "metavalue";
                ObjectMetadata objectMetadata = new ObjectMetadata()
                {
                    UserMetadata = userMetadata
                };
                copyObjectRequest.NewObjectMetadata = objectMetadata;
                client.CopyObject(copyObjectRequest);
                Assert.Equal("metavalue",
                    client.GetObjectMetadata(bucketName, newObjectName).UserMetadata["metakey"]);
            }
        }

        public class PutObjectTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string path = "put_object_ordinary.txt";
                File.WriteAllText(path, "data");
                FileInfo fileInfo = new FileInfo(path);
                string key = "te%%st  ";
                PutObjectRequest request = new PutObjectRequest()
                {
                    BucketName = this.bucketName,
                    Key = key,
                    FileInfo = fileInfo
                };
                String eTag = this.client.PutObject(request).ETAG;
                Assert.Equal(eTag, HashUtils.ComputeMD5Hash(fileInfo));
                String content = System.Text.Encoding.Default.GetString(this.client.GetObjectContent
                    (this.bucketName, key));
                Assert.Equal("data", content);
                FileInfo outFileInfo = new FileInfo("object_ordinary.txt");
                this.client.GetObject(this.bucketName, key, outFileInfo);
                Assert.Equal(eTag, HashUtils.ComputeMD5Hash(outFileInfo));
            }

            [Fact]
            public void TestContentLengthSmallThanStreamLength()
            {
                ObjectMetadata objectMetadata = new ObjectMetadata();
                objectMetadata.ContentLength = 2;
                var userMetaDic = new Dictionary<string, string>();
                objectMetadata.UserMetadata = userMetaDic;
                PutObjectRequest request = new PutObjectRequest()
                {
                    BucketName = this.bucketName,
                    Key = "te%%st",
                    Stream = new MemoryStream(System.Text.Encoding.Default.GetBytes("data")),
                    ObjectMetadata = objectMetadata
                };
                this.client.PutObject(request);
                String content = System.Text.Encoding.Default.GetString(this.client.GetObjectContent
                    (this.bucketName, "te%%st"));
                Assert.Equal("da", content);
            }
        }

        public class GetObjectTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string path = "put_object_ordinary.txt";
                File.WriteAllText(path, "data");
                FileInfo fileInfo = new FileInfo(path);
                string key = "te%%st  ";
                PutObjectRequest request = new PutObjectRequest()
                {
                    BucketName = this.bucketName,
                    Key = key,
                    FileInfo = fileInfo
                };
                String eTag = this.client.PutObject(request).ETAG;
                Assert.Equal(eTag, HashUtils.ComputeMD5Hash(fileInfo));
                BosObject bosObject = this.client.GetObject(this.bucketName, key);
                String content =
                    Encoding.Default.GetString(IOUtils.StreamToBytes(bosObject.ObjectContent,
                        bosObject.ObjectMetadata.ContentLength, 8192));
                Assert.Equal("data", content);
            }

            [Fact]
            public void TestGetRange()
            {
                string path = "put_object_ordinary.txt";
                File.WriteAllText(path, "data");
                FileInfo fileInfo = new FileInfo(path);
                string key = "te%%st  ";
                PutObjectRequest request = new PutObjectRequest()
                {
                    BucketName = this.bucketName,
                    Key = key,
                    FileInfo = fileInfo
                };
                this.client.PutObject(request);
                GetObjectRequest getObjectRequest = new GetObjectRequest() {BucketName = this.bucketName, Key = key};
                getObjectRequest.SetRange(0, 0);
                BosObject bosObject = this.client.GetObject(getObjectRequest);
                String content =
                    Encoding.Default.GetString(IOUtils.StreamToBytes(bosObject.ObjectContent,
                        bosObject.ObjectMetadata.ContentLength, 8192));
                Assert.Equal("d", content);
            }
        }

        public class InitiateMultipartUploadTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                InitiateMultipartUploadResponse response = this.client.InitiateMultipartUpload(this.bucketName, "test");
                Assert.Equal(response.Bucket, this.bucketName);
                Assert.Equal("test", response.Key);
                String uploadId = response.UploadId;
                List<MultipartUploadSummary> uploads =
                    this.client.ListMultipartUploads(this.bucketName).Uploads;
                Assert.Single(uploads);
                Assert.Equal(uploads[0].UploadId, uploadId);
            }
        }

        public class UploadPartTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                String uploadId = this.client.InitiateMultipartUpload(this.bucketName, "test").UploadId;
                UploadPartResponse response = this.client.UploadPart(new UploadPartRequest()
                {
                    BucketName = this.bucketName,
                    Key = "test",
                    UploadId = uploadId,
                    PartNumber = 1,
                    PartSize = 4,
                    InputStream = new MemoryStream(Encoding.Default.GetBytes("data"))
                });
                Assert.Equal(1, response.PartNumber);
                Assert.NotNull(response.ETag);
                List<PartSummary> parts = this.client.ListParts(this.bucketName, "test", uploadId).Parts;
                Assert.Single(parts);
                PartSummary part = parts[0];
                Assert.NotNull(part);
                Assert.Equal(part.ETag, response.ETag);
                Assert.Equal(4L, part.Size);
            }
        }

        public class ListPartsTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string uploadId = this.client.InitiateMultipartUpload(this.bucketName, "test").UploadId;
                List<string> eTags = new List<string>();
                for (int i = 0; i < 10; ++i)
                {
                    eTags.Add(this.client.UploadPart(new UploadPartRequest()
                    {
                        BucketName = this.bucketName,
                        Key = "test",
                        UploadId = uploadId,
                        PartNumber = i + 1,
                        PartSize = 1,
                        InputStream = new MemoryStream(Encoding.Default.GetBytes(i.ToString()))
                    }).ETag);
                }

                ListPartsResponse response = this.client.ListParts(this.bucketName, "test", uploadId);
                Assert.Equal(response.BucketName, this.bucketName);
                Assert.False(response.IsTruncated);
                Assert.Equal("test", response.Key);
                Assert.Equal(1000, response.MaxParts);
                Assert.Equal(10, response.NextPartNumberMarker);
                Assert.Equal(response.Owner.Id, this.owner.Id);
                Assert.Equal(0, response.PartNumberMarker);
                Assert.Equal(response.UploadId, uploadId);
                List<PartSummary> parts = response.Parts;
                Assert.Equal(10, parts.Count);
                for (int i = 0; i < 10; ++i)
                {
                    PartSummary part = parts[i];
                    Assert.Equal(part.ETag, eTags[i]);
                    Assert.Equal(part.PartNumber, i + 1);
                    Assert.Equal(1, part.Size);
                    Assert.True(Math.Abs(part.LastModified.Subtract(DateTime.UtcNow).TotalSeconds) < 60);
                }
            }
        }

        public class CompleteMultipartUploadTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                ObjectMetadata objectMetadata = new ObjectMetadata();
                objectMetadata.ContentType = "text/plain";
                InitiateMultipartUploadRequest initRequest = new InitiateMultipartUploadRequest()
                {
                    BucketName = this.bucketName,
                    Key = "test",
                    ObjectMetadata = objectMetadata
                };

                string uploadId = this.client.InitiateMultipartUpload(initRequest).UploadId;
                List<PartETag> partETags = new List<PartETag>();
                for (int i = 0; i < 1; ++i)
                {
                    string eTag = this.client.UploadPart(new UploadPartRequest()
                    {
                        BucketName = this.bucketName,
                        Key = "test",
                        UploadId = uploadId,
                        PartNumber = i + 1,
                        PartSize = 1,
                        InputStream = new MemoryStream(Encoding.Default.GetBytes(i.ToString()))
                    }).ETag;
                    partETags.Add(new PartETag() {PartNumber = i + 1, ETag = eTag});
                }

                objectMetadata = new ObjectMetadata();
                Dictionary<string, string> userMetadata = new Dictionary<string, string>();
                userMetadata["metakey"] = "metaValue";
                objectMetadata.UserMetadata = userMetadata;
                objectMetadata.ContentType = "text/json";
                CompleteMultipartUploadRequest request =
                    new CompleteMultipartUploadRequest()
                    {
                        BucketName = this.bucketName,
                        Key = "test",
                        UploadId = uploadId,
                        PartETags = partETags,
                        ObjectMetadata = objectMetadata
                    };
                CompleteMultipartUploadResponse response = this.client.CompleteMultipartUpload(request);
                Assert.Equal(response.BucketName, this.bucketName);
                Assert.Equal("test", response.Key);
                Assert.NotNull(response.ETag);
                Assert.NotNull(response.Location);
                ObjectMetadata metadata = this.client.GetObjectMetadata(bucketName, "test");
                Assert.Equal("text/plain", metadata.ContentType);
                string resultUserMeta = metadata.UserMetadata["metakey"];
                Assert.Equal("metaValue", resultUserMeta);
            }
        }

        public class AbortMultipartUploadTest : Base
        {
            [Fact]
            public void TestOrdinary()
            {
                string uploadId = this.client.InitiateMultipartUpload(this.bucketName, "abortMultipartTest").UploadId;
                for (int i = 0; i < 10; ++i)
                {
                    this.client.UploadPart(new UploadPartRequest()
                    {
                        BucketName = this.bucketName,
                        Key = "abortMultipartTest",
                        UploadId = uploadId,
                        PartNumber = i + 1,
                        PartSize = 1,
                        InputStream = new MemoryStream(Encoding.Default.GetBytes(i.ToString()))
                    });
                }

                List<MultipartUploadSummary> uploads =
                    this.client.ListMultipartUploads(this.bucketName).Uploads;
                Assert.Single(uploads);
                this.client.AbortMultipartUpload(this.bucketName, "abortMultipartTest", uploadId);
                uploads = this.client.ListMultipartUploads(this.bucketName).Uploads;
                Assert.Empty(uploads);
            }
        }
    }
}