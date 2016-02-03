using System;
using System.IO;
using IniParser;
using IniParser.Model;
using NuGet;
using Ullink.NugetConverter.model;

namespace Ullink.NugetConverter.services.dependenciesService
{
    internal class DependenciesResolverSerializer
    {
        private const string _sectionSeparator = "--";

        public void Save(SemanticVersion version, string name, DllReference parentAssembly)
        {
            var parser = new FileIniDataParser();
            var versionsFileData = new IniData();

            versionsFileData.Sections.AddSection("Dependency");
            versionsFileData.Sections["Dependency"].AddKey("version", version.ToString());
            versionsFileData.Sections["Dependency"].AddKey("name", name);
            
            if(!Directory.Exists("cache"))
                Directory.CreateDirectory("cache");

            parser.WriteFile(@"cache\" + parentAssembly.Id.Item1 + _sectionSeparator + parentAssembly.Id.Item2 + _sectionSeparator + name + ".ini", versionsFileData);
        }

        public void AddUnresolvedDependencies(AssemblyReference parentAssembly)
        {
            var parser = new FileIniDataParser();
            var versionsFileData = new IniData();

            if (!Directory.Exists(@"cache\unresolved"))
                Directory.CreateDirectory(@"cache\unresolved");

            var file = @"cache\unresolved\" + parentAssembly.Item2 + _sectionSeparator + parentAssembly.Item1 + ".ini";
            if (File.Exists(file))
                return;

            parser.WriteFile(file, versionsFileData);
        }

        public Tuple<SemanticVersion, string> Load(DllReference parentAssembly, string Dependency)
        {
            var path = parentAssembly.Id.Item1 + _sectionSeparator + parentAssembly.Id.Item2 + _sectionSeparator + Dependency + ".ini";
            if (!File.Exists(@"cache\" + path))
                return null;

            var parser = new FileIniDataParser();
            var iniData = parser.ReadFile(@"cache\" + path);

            var section = iniData.Sections["Dependency"];
            var version =  SemanticVersion.ParseOptionalVersion(section["version"]);
            var name = section["name"];

            return  new Tuple<SemanticVersion, string>(version, name);
        }

        public void Delete(Tuple<SemanticVersion, string> parentAssembly)
        {
            var files = Directory.GetFiles(@"cache\", parentAssembly.Item1 + _sectionSeparator + parentAssembly.Item2 + "*");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }
}
