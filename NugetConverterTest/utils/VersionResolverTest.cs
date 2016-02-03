using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet;
using Ullink.NugetConverter.services.assemblyCacheService;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverterTest.utils
{
    [TestClass]
    public class VersionResolverTest
    {
        VersionResolverService service = new VersionResolverService(new ConfigurationService(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), ResolutionLevelEnum.All);

        [TestMethod]
        public void IncorrectFolderx86()
        {

            string assemblyPath = @"c:\temp\common\1.0.0\buildtools\bin\windows\x86\icsharpcode.sharpziplib.dll";
            var version = service.ParseFolderVersion(assemblyPath);
            Assert.IsNull(version);

        }

        [TestMethod]
        public void IncorrectFolder06()
        {
            string assemblyPath = @"c:\temp\common\cecil\0.6\mono.cecil.dll";
            var version = service.ParseFolderVersion(assemblyPath);
            Assert.AreEqual(new SemanticVersion("0.6.0.0"), version);

        }


        [TestMethod]
        public void IncorrectFolder0_47()
        {

            string assemblyPath = @"c:\temp\common\ul-tools-commons-core\1.78-dll-snapshot\dll0_47\commons.dll";
            var version = service.ParseFolderVersion(assemblyPath);
            Assert.IsNull(version);

        }

        [TestMethod]
        public void IncorrectFolderBeta1()
        {
            string assemblyPath = @"c:\temp\common\castle-core\3.0.0.beta1\castle.core.dll";
            var version = service.ParseFolderVersion(assemblyPath);
            Assert.IsNull(version);
        }
    }
}
