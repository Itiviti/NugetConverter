using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ullink.NugetConverter.services.configuration
{
    public class ConfigurationGetter
    {
        private const string ConfigurationFileName = "nugetconverter.ini";

        private readonly ConfigurationFileReader _configurationFileReader;

        public ConfigurationGetter(ConfigurationFileReader configurationFileReader)
        {
            if (configurationFileReader == null)
            {
                throw new ArgumentNullException(nameof(configurationFileReader));
            }
            _configurationFileReader = configurationFileReader;
        }

        public Configuration Get(string dllPath)
        {
            DirectoryInfo parentDir = Directory.Exists(dllPath) ? new DirectoryInfo(dllPath) : new FileInfo(dllPath).Directory;
            Configuration configuration = new Configuration();
            while (parentDir != null)
            {
                var confPath = Path.Combine(parentDir.FullName, ConfigurationFileName);
                if (File.Exists(confPath))
                {
                    Trace.TraceInformation("Configuration of {0} will be added for mapping resolution", confPath);
                    configuration = Merge(configuration, _configurationFileReader.Read(confPath));
                }
                parentDir = parentDir.Parent;
            }
            return configuration;
        }
        
        public Configuration Merge(Configuration child, Configuration parent)
        {
            return new Configuration
                {
                    Ignore = child.Ignore || parent.Ignore,
                    NugetPackagesMapping = child.NugetPackagesMapping.Union(parent.NugetPackagesMapping, new ConfigurationComparer()),
                    SnapshotMapping =  child.SnapshotMapping.Union(parent.SnapshotMapping),
                    PackageMapping = child.SnapshotMapping.Union(parent.PackageMapping),
                    CircularMapping = child.CircularMapping.Union(parent.CircularMapping)
            };
        }

        private class ConfigurationComparer : IEqualityComparer<Tuple<string, ConfigurationFileReader.MappingInfo>>
        {
            public bool Equals(Tuple<string, ConfigurationFileReader.MappingInfo> x, Tuple<string, ConfigurationFileReader.MappingInfo> y)
            {
                return x.Item1 == y.Item1 && x.Item2 == y.Item2;
            }

            public int GetHashCode(Tuple<string, ConfigurationFileReader.MappingInfo> obj)
            {
                return obj.GetHashCode();
            }
        }

    }
}
