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
using BaiduBce.Auth;
using BaiduBce.Services.Sts;
using BaiduBce.Services.Sts.Model;
using Xunit;

namespace BaiduBce.UnitTest.Services.Sts
{
    public class StsClientUnitTest
    {
        public class Base : BceClientUnitTestBase, IDisposable
        {
            protected BceClientConfiguration config;

            protected StsClient client;

            public Base()
            {
                this.config = new BceClientConfiguration();
                this.config.Credentials = new DefaultBceCredentials(this.ak, this.sk);
                this.config.Endpoint = this.endpoint;
                this.client = new StsClient(this.config);
            }

            public void Dispose()
            {
            }
        }

        public class GetSessionTokenTest : Base
        {
            [Fact]
            public void TestDefaultArguments()
            {
                var getSessionTokenResponse = this.client.GetSessionToken();
                Assert.NotNull(getSessionTokenResponse.AccessKeyId);
                Assert.NotNull(getSessionTokenResponse.SecretAccessKey);
                Assert.NotNull(getSessionTokenResponse.SessionToken);
                Assert.NotNull(getSessionTokenResponse.Expiration);
                Assert.True((getSessionTokenResponse.Expiration - DateTime.Now).TotalSeconds > 1500);
            }

            [Fact]
            public void TestDurationSeconds()
            {
                var getSessionTokenResponse =
                    this.client.GetSessionToken(new GetSessionTokenRequest() { DurationSeconds = 10 });
                Assert.NotNull(getSessionTokenResponse.AccessKeyId);
                Assert.NotNull(getSessionTokenResponse.SecretAccessKey);
                Assert.NotNull(getSessionTokenResponse.SessionToken);
                Assert.NotNull(getSessionTokenResponse.Expiration);
                Assert.True((getSessionTokenResponse.Expiration - DateTime.Now).TotalSeconds < 30);
            }

            [Fact]
            public void TestAcl()
            {
                var getSessionTokenResponse =
                    this.client.GetSessionToken(new GetSessionTokenRequest()
                    {
                        AccessControlList = @"
                        {
                            ""id"": ""test"",
                            ""accessControlList"": [
                                {
                                    ""eid"": ""e0"",
                                    ""service"": ""bce:bos"",
                                    ""region"": ""bj"",
                                    ""effect"": ""Allow"",
                                    ""resource"": [""test-bucket/*""],
                                    ""permission"": [""READ""]
                                }
                            ]
                        }"
                    });
                Assert.NotNull(getSessionTokenResponse.AccessKeyId);
                Assert.NotNull(getSessionTokenResponse.SecretAccessKey);
                Assert.NotNull(getSessionTokenResponse.SessionToken);
                Assert.NotNull(getSessionTokenResponse.Expiration);
            }

            [Fact]
            public void TestEmptyAcl()
            {
                Assert.Throws<BceServiceException>(() =>
                {
                    var getSessionTokenResponse = 
                        this.client.GetSessionToken(new GetSessionTokenRequest() {AccessControlList = "{}"});
                });
            }

            [Fact]
            public void TestInvalidAcl()
            {
                Assert.Throws<BceServiceException>(() =>
                {
                    var getSessionTokenResponse =
                        this.client.GetSessionToken(new GetSessionTokenRequest() {AccessControlList = "{"});
                });
            }
        }
    }
}