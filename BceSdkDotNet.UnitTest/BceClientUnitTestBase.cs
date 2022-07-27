using System;
using System.Collections.Generic;
using System.Configuration;
#if NETCOREAPP
using System.IO;
using System.Reflection;
#endif


namespace BaiduBce.UnitTest
{
    public class BceClientUnitTestBase
    {
        protected string endpoint;
        protected string ak;
        protected string sk;
        protected string userId;

        static BceClientUnitTestBase()
        {
            // https://github.com/dotnet/runtime/issues/22720#issuecomment-621273186
#if NETCOREAPP
            string configFile = $"{Assembly.GetExecutingAssembly().Location}.config";
            string outputConfigFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
            File.Copy(configFile, outputConfigFile, true);
#endif
        }

        public BceClientUnitTestBase()
        {
            this.endpoint = ConfigurationManager.AppSettings["endpoint"];
            this.ak = ConfigurationManager.AppSettings["ak"];
            this.sk = ConfigurationManager.AppSettings["sk"];
            this.userId = ConfigurationManager.AppSettings["userid"];
        }
    }
}