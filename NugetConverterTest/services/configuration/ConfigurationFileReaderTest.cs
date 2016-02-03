using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ullink.NugetConverter.services.configuration;

namespace Ullink.NugetConverterTest.services.configuration
{
    [TestClass]
    public class ConfigurationFileReaderTest
    {
        [TestMethod]
        public void ReadFileWithIgnoreFlagSetToTrue()
        {
            new[]
                {
                    new
                        {
                            fileContent = @"[global]
ignore = true",
                            expectedIgnore = true
                        },
                    new
                        {
                            fileContent = "ignore = true",
                            expectedIgnore = false
                        },
                    new
                        {
                            fileContent = @"[global]
ignore = false",
                            expectedIgnore = false
                        },
                    new
                        {
                            fileContent = @"[global]
ignore = smething_wrong",
                            expectedIgnore = false
                        },
                    new
                        {
                            fileContent = "",
                            expectedIgnore = false
                        }
                }.ToList().ForEach(testCase =>
                    {
                        string tempFile = Path.GetTempFileName();
                        try
                        {
                            File.WriteAllText(tempFile, testCase.fileContent);
                            Assert.AreEqual(
                                testCase.expectedIgnore,
                                new ConfigurationFileReader().Read(tempFile).Ignore);
                        }
                        finally
                        {
                            File.Delete(tempFile);
                        }
                    });
        }
    }
}
