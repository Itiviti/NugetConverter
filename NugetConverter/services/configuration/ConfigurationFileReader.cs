using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Model;
using NuGet;

namespace Ullink.NugetConverter.services.configuration
{
    public class ConfigurationFileReader
    {
        private const string IgnoreToken = "ignore";

        private const string GlobalSection = "global";
        private const string MappingSection = "dependencies.mapping";
        private const string SnapshotMappingSection = "snapshot.mapping";
        private const string PackageMappingSection = "package.mapping";
        private const string DependenciesCircularSection = "dependencies.circular";

        public virtual Configuration Read(string confPath)
        {
            //globale section
            FileIniDataParser parser = new FileIniDataParser();
            SectionData globalSection = parser.ReadFile(confPath).Sections.GetSectionData(GlobalSection);
            bool ignore = false;
            if (globalSection != null
                && globalSection.Keys.ContainsKey(IgnoreToken))
            {
                Boolean.TryParse(globalSection.Keys[IgnoreToken], out ignore);
            }

            //Mapping section
            SectionData mappingSection = parser.ReadFile(confPath).Sections.GetSectionData(MappingSection);
            IEnumerable<Tuple<string, MappingInfo>> mappings = null;
            if (mappingSection != null)
            {
                mappings = mappingSection.Keys.Select(_ => new Tuple<string, MappingInfo>(
                            _.KeyName.Split('|')[0],
                                new MappingInfo(
                                    _.KeyName.Split('|').Count() > 1 ? new SemanticVersion(_.KeyName.Split('|')[1]) : null,
                                    new Regex(_.Value, RegexOptions.IgnoreCase))));
            }

            //Mapping section
            SectionData snapshotMappingSection = parser.ReadFile(confPath).Sections.GetSectionData(SnapshotMappingSection);
            IEnumerable<Tuple<string, Regex>> snapshotMappings = null;
            if (snapshotMappingSection != null)
            {
                snapshotMappings = snapshotMappingSection.Keys.Select(_ => new Tuple<string, Regex>(_.KeyName,new Regex(_.Value, RegexOptions.IgnoreCase)));
            }

            //Mapping package
            SectionData packageMappingSection = parser.ReadFile(confPath).Sections.GetSectionData(PackageMappingSection);
            IEnumerable<Tuple<string, Regex>> packageMappings = null;
            if (packageMappingSection != null)
            {
                packageMappings = packageMappingSection.Keys.Select(_ => new Tuple<string, Regex>(_.KeyName, new Regex(_.Value, RegexOptions.IgnoreCase)));
            }

            //Mapping package
            SectionData circularMappingSection = parser.ReadFile(confPath).Sections.GetSectionData(DependenciesCircularSection);
            IEnumerable<Tuple<string, Tuple<string, SemanticVersion>>> circularMappings = null;
            if (circularMappingSection != null)
            {
                circularMappings = circularMappingSection.Keys.Select(_ =>
                {
                    var fullName = _.Value.Split('|');
                    return new Tuple<string, Tuple<string, SemanticVersion>>(_.KeyName,new Tuple<string, SemanticVersion>(fullName[0], new SemanticVersion(fullName[1])));
                });
            }

            var configuration =  new Configuration {Ignore = ignore};
            if (mappings != null)
                configuration.NugetPackagesMapping = mappings;
            if (snapshotMappings != null)
                configuration.SnapshotMapping = snapshotMappings;
            if (packageMappings != null)
                configuration.CircularMapping = circularMappings;

            return configuration;
        }

        public class MappingInfo
        {
            public Regex Regex { get; }
            public SemanticVersion SemanticVersion { get; }

            public MappingInfo(SemanticVersion semanticVersion, Regex regex)
            {
                this.SemanticVersion = semanticVersion;
                this.Regex = regex;
            }
        }
    }
}
