using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using NuGet;

namespace Ullink.NugetConverter.services.configuration
{
    public class Configuration
    {
        public Configuration()
        {
            SnapshotName = "snapshot";
            NugetPackagesMapping  = new Collection<Tuple<string, ConfigurationFileReader.MappingInfo>>();
            SnapshotMapping = new Collection<Tuple<string, Regex>>();
            PackageMapping = new Collection<Tuple<string, Regex>>();
            CircularMapping = new Collection<Tuple<string, Tuple<string, SemanticVersion>>>();
        }

        public string SnapshotName { get; set; }

        public bool Ignore { get; set; }

        public IEnumerable<Tuple<string, Regex>> SnapshotMapping { get; set; }
        
        public IEnumerable<Tuple<string, ConfigurationFileReader.MappingInfo>> NugetPackagesMapping { get; set; }

        public IEnumerable<Tuple<string, Regex>> PackageMapping { get; set; }

        public IEnumerable<Tuple<string, Tuple<string, SemanticVersion>>> CircularMapping { get; set; }

    }
}
