using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IniParser;
using IniParser.Model;
using Mono.Cecil;
using NuGet;
using Ullink.NugetConverter.model;

namespace Ullink.NugetConverter.services.assemblyCacheService
{
    internal class AssemblyCacheServiceSerializer
    {
        private readonly string _assembliesCache = Path.Combine("cache", "assemblies.ini");
        private readonly string _conflictingAssembliesCache = Path.Combine("cache", "versions.ini");
        private FileIniDataParser _parser = new/**/ FileIniDataParser();

        private const string _sectionSeparator = "--";

        public void Save(IDictionary<Tuple<Version, string>, SortedSet<SemanticVersion>> conflictingAssemblies, SortedDictionary<Tuple<SemanticVersion, string>, DllReference> assemblies)
        {
            var versionsFileData = new/*?*/ IniData();
            foreach (var assembly in conflictingAssemblies.Keys)
            {
                var keyName = assembly.Item1 + _sectionSeparator + assembly.Item2;
                versionsFileData.Sections.AddSection(keyName);
                var section = versionsFileData[keyName];
                foreach (var version in conflictingAssemblies[assembly])
                {
                    section.AddKey(version.ToString());
                }
            }   
            _parser.WriteFile(_conflictingAssembliesCache, versionsFileData);

            versionsFileData = new/*?*/ IniData();
            foreach (var assembly in assemblies)
            {
                versionsFileData.Sections.AddSection(assembly.Key.Item1 + _sectionSeparator + assembly.Key.Item2);
                var section = versionsFileData[assembly.Key.Item1 + _sectionSeparator + assembly.Key.Item2];
                section.AddKey("Path", assembly.Value.Path);
                section.AddKey("FrameworkVersion", assembly.Value.FrameworkVersion);

                versionsFileData.Sections.AddSection(assembly.Key.Item1 + _sectionSeparator + assembly.Key.Item2 + _sectionSeparator + "dependencies");
                var dependancies = versionsFileData[assembly.Key.Item1 + _sectionSeparator + assembly.Key.Item2 + _sectionSeparator + "dependencies"];
                foreach (var dependancy in assembly.Value.AssemblyReferences)
                {
                    dependancies.AddKey(dependancy.Item2, dependancy.Item1 + "|" + dependancy.FullName);
                }
                
            }   
            _parser.WriteFile(_assembliesCache, versionsFileData);
        }

        public void Load(ref IDictionary<Tuple<Version, string>, SortedSet<SemanticVersion>> conflictingAssemblies, ref SortedDictionary<Tuple<SemanticVersion, string>, DllReference> assemblies)
        {
            if (!File.Exists(_conflictingAssembliesCache))
                return;
            if (!File.Exists(_assembliesCache))
                return;

            var parser = new FileIniDataParser();
            var iniData = parser.ReadFile(_assembliesCache);
            foreach (var section in iniData.Sections)
            {
                try
                {
                    var rawkey = section.SectionName.Split(new[] {_sectionSeparator},
                                                           StringSplitOptions.RemoveEmptyEntries);
                    if (rawkey.Length == 2)
                    {
                        var semanticVersion = SemanticVersion.ParseOptionalVersion(rawkey[0]);
                        var key = new Tuple<SemanticVersion, string>(semanticVersion, rawkey[1]);

                        var path = section.Keys["Path"];
                        string mainPath = null;
                        if (!string.IsNullOrEmpty(path))
                            mainPath = path;

                        var frameworkVersionSection = section.Keys["FrameworkVersion"];
                        string frameworkVersion = null;
                        if (!string.IsNullOrEmpty(frameworkVersionSection))
                            frameworkVersion = frameworkVersionSection;

                        var rawkeyDependancies =
                            iniData.Sections[rawkey[0] + _sectionSeparator + rawkey[1] + "--dependencies"];

                        var references = new List<AssemblyReference>();
                        foreach (var dependancy in rawkeyDependancies)
                        {
                            var assemblyName = dependancy.KeyName;
                            var valueSplit = dependancy.Value.Split('|');
                            var assemblyVersion = valueSplit[0];
                            //TODO : Remove Version & FileVersion
                            var assemblyReference = new AssemblyReference(assemblyName, Version.Parse(assemblyVersion), valueSplit.Length>1 ? valueSplit[1]:null);
                            references.Add(assemblyReference);
                        }

                        assemblies.Add(key, new DllReference(mainPath, frameworkVersion, key, references));
                    }
                }
                catch (Exception exc)
                {
                    Trace.TraceWarning("Unable to parse from peristence {0}, it will be ignored. {1} ", section.SectionName, exc.Message);
                }
            }

           
            iniData = parser.ReadFile(_conflictingAssembliesCache);
            foreach (var section in iniData.Sections)
            {
                var keyArray = section.SectionName.Split(new[] { _sectionSeparator }, StringSplitOptions.RemoveEmptyEntries);
                var key = new Tuple<Version, string>(Version.Parse(keyArray[0]), keyArray[1]);
                var value = new SortedSet<SemanticVersion>();
                foreach (var version in section.Keys)
                {

                    var semanticVersion = SemanticVersion.ParseOptionalVersion(version.KeyName);
                    value.Add(semanticVersion);
                    
                    
                }

                if(value.Count!=0)
                    conflictingAssemblies.Add(key, value);
            }

        }
    }
}
