using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using Mono.Cecil;
using NuGet;
using Ullink.NugetConverter.model;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverter.services.assemblyCacheService
{
    public class AssemblyCacheService
    {
        private readonly VersionResolverService _versionresolverService;
        private readonly ConfigurationService _configurationGetter;
        private readonly bool _useCache;
        private SortedDictionary<Tuple<SemanticVersion, string>, DllReference> _assemblies;
        /// <summary>
        /// dll ignored by the user. ie filtered by nugetconverter.ini
        /// </summary>
        public SortedDictionary<Tuple<SemanticVersion, string>, DllReference> IgnoredAssemblies { get; }

        /// <summary>
        /// It happens that assembly version is the same for more than one assemblies, but assembly file version are differents
        /// Key : The assemblyVersion
        /// Value: SortedSet<SemanticVersion> contains list of assemblies with same assembly version
        /// </summary>
        private IDictionary<Tuple<Version, string>, SortedSet<SemanticVersion>> _conflictingAssemblies;
        
        /// <summary>
        /// The only purpose of this is to use Cecil to read References
        /// </summary>
        private readonly DefaultAssemblyResolver _assemblyResolver = new/**/ DefaultAssemblyResolver();
        private readonly ReaderParameters _readerParameters;

        public AssemblyCacheService(IEnumerable<string> dlls, VersionResolverService versionresolverService, ConfigurationService configurationService, bool useCache)
        {
            _versionresolverService = versionresolverService;
            _configurationGetter = configurationService;
            _useCache = useCache;
            _readerParameters = new/**/ ReaderParameters { AssemblyResolver = _assemblyResolver };
            _conflictingAssemblies = new/**/ Dictionary<Tuple<Version, string>, SortedSet<SemanticVersion>>();
            IgnoredAssemblies = new/**/ SortedDictionary<Tuple<SemanticVersion, string>, DllReference>();
            _conflictingAssemblies = new/**/ Dictionary<Tuple<Version, string>, SortedSet<SemanticVersion>>();
            _assemblies = CreateOrUpdateAssemblies(dlls);
        }

        /// <summary>
        /// For a specific assembly contains all the Known Exisiting Versions
        /// Tuple<SemanticVersion, string>: Expected Version
        /// SortedSet<SemanticVersion>    : Exisiting conflicting version
        /// 
        /// The main issue is that some folder have version where dll inside have an other.
        /// API are the main example. folder will have for example
        /// 1.0 (folder) but AssemblyVersion will be 1.0
        /// 1.1 (folder) but AssemblyVersion will be 1.0
        /// 1.2 (folder) but AssemblyVersion will be 1.0
        /// This Cache is used to keep conflicting folder & dll
        /// </summary>
        public IDictionary<Tuple<Version, string>, SortedSet<SemanticVersion>> ConflictingAssemblies => _conflictingAssemblies;
        public SortedDictionary<Tuple<SemanticVersion, string>, DllReference> Assemblies => _assemblies;
        public Tuple<SemanticVersion, string> AddOrUpdateAssembly(string fullPath)
        {
            var result = CreateOrUpdateAssembly(fullPath);
            return result;
        }

        private SortedDictionary<Tuple<SemanticVersion, string>, DllReference> CreateOrUpdateAssemblies(IEnumerable<string> dlls)
        {
            _assemblies = new SortedDictionary<Tuple<SemanticVersion, string>, DllReference>();

            //Ugly Performance...
            var notAlreadyCompute = dlls
                .Where(dll => !_assemblies.Any(_ => dll.Equals(_.Value.Path)))
                .ToList();

            foreach (var dll in notAlreadyCompute)
            {
                CreateOrUpdateAssembly(dll);
            }

            return Assemblies;
        }


        private Tuple<SemanticVersion, string> CreateOrUpdateAssembly(string dll)
        {
            // Read assembly and change reference according to one passed in parameters
            try
            {
                // Read assembly and change reference according to one passed in parameters
                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(dll, _readerParameters);

                var mainAssemblyId = _versionresolverService.ResolveVersion(assemblyDefinition, dll);
                if (mainAssemblyId == null)
                {
                    throw new VersionNotFoundException($"Unable to extract Version from {dll}");
                }

                var assemblyReferences = from asm in assemblyDefinition.MainModule.AssemblyReferences select new /*?*/ AssemblyReference(asm.Name, asm.Version, asm.FullName);
                var frameworkVersionName = GetFrameworkVersion(assemblyDefinition);
                if (_configurationGetter.Get(dll).Ignore)
                {
                    CreateOrUpdateAssembly(IgnoredAssemblies, dll, frameworkVersionName, mainAssemblyId, assemblyReferences);
                    Trace.TraceInformation("IGNORE : {0} because a nugetconverter.ini file ask to do so", dll);
                    return null;
                }
                return CreateOrUpdateAssembly(_assemblies, dll, frameworkVersionName, mainAssemblyId, assemblyReferences);

            }
            catch (Exception exception)
            {
                Trace.TraceInformation("IGNORE : {0} due to {1}", dll, exception.Message);
            }

            return null;
        }

        private static string GetFrameworkVersion(AssemblyDefinition assemblyDefinition)
        {
            //Framework .net Version
            string frameworkVersionName = null;
            var targetFrameworkAttribute = assemblyDefinition.CustomAttributes.FirstOrDefault(_ => _.AttributeType.Name.Equals(typeof (TargetFrameworkAttribute).Name));
            if (targetFrameworkAttribute != null)
            {
                var targetFrameworkVersion = targetFrameworkAttribute.Properties.FirstOrDefault().Argument.Value.ToString();
                frameworkVersionName = "net" + targetFrameworkVersion.Where(Char.IsDigit).Aggregate(String.Empty, (s, c) => s+c).PadRight(2, '0');
            }
            else
            {
                var targetRuntime = assemblyDefinition.MainModule.Runtime;
                switch (targetRuntime)
                {
                    case TargetRuntime.Net_1_0:
                    case TargetRuntime.Net_1_1:
                    case TargetRuntime.Net_2_0:
                        frameworkVersionName = "net35";
                        break;
                    case TargetRuntime.Net_4_0:
                        frameworkVersionName = "net40";
                        break;
                }
            }

            return frameworkVersionName;
        }

        private Tuple<SemanticVersion, string> CreateOrUpdateAssembly(SortedDictionary<Tuple<SemanticVersion, string>, DllReference> cache,
                                                                      string dll,
                                                                      string frameworkVersionName,
                                                                      FileAssemblyVersion mainAssemblyId,
                                                                      IEnumerable<AssemblyReference> assemblyReferences)
        {

            if (cache.ContainsKey(mainAssemblyId))
                return UpdateAssembly(cache, dll, frameworkVersionName, mainAssemblyId, assemblyReferences);
            return CreateAssembly(cache, dll, frameworkVersionName, mainAssemblyId, assemblyReferences);
        }

        private Tuple<SemanticVersion, string> CreateAssembly(SortedDictionary<Tuple<SemanticVersion, string>, DllReference> cache,
                                                                string dll,
                                                                string frameworkVersionName,
                                                                FileAssemblyVersion mainAssemblyId,
                                                                IEnumerable<AssemblyReference> assemblyReferences)
        {
            Trace.TraceInformation($"ADDED : Assembly {mainAssemblyId.Item2}, {mainAssemblyId.Item1}");

            //For a specific version (Version+Name) it might exist multiple real File (SemanticVersion+Name)
            //For exampl snapshot. We store them here will see later which one to choose
            var assemblyId = new Tuple<Version, string>(mainAssemblyId.AssemblyVersion, mainAssemblyId.Item2);

            if (!ConflictingAssemblies.ContainsKey(assemblyId))
                ConflictingAssemblies.Add(assemblyId, new /**/ SortedSet<SemanticVersion>());
            ConflictingAssemblies[assemblyId].Add(mainAssemblyId.Item1);

            // While adding references we ignore assembly in the GAC
            cache.Add(mainAssemblyId,
                           new /**/ DllReference(dll,
                                                frameworkVersionName,
                                                    mainAssemblyId,
                                                    assemblyReferences.Where(
                                                        _ =>
                                                        !GacUtil.IsFullNameAssemblyInGAC(_.FullName))));
            return cache[mainAssemblyId].Id;
        }

        private Tuple<SemanticVersion, string> UpdateAssembly(SortedDictionary<Tuple<SemanticVersion, string>, DllReference> cache, 
                                                                string dll,
                                                                string frameworkVersion,
                                                                FileAssemblyVersion mainAssemblyId,
                                                                IEnumerable<AssemblyReference> assemblyReferences)
        {
            //Not the same path, but it seems more correct as AssemblyName match dll name
            if (cache.ContainsKey(mainAssemblyId) && !mainAssemblyId.Item2.Equals(Path.GetFileNameWithoutExtension(cache[mainAssemblyId].Path))
                                                       && mainAssemblyId.Item2.Equals(Path.GetFileNameWithoutExtension(dll)))
            {
                Trace.TraceInformation($"UPDATE : {mainAssemblyId.Item2}, {mainAssemblyId.Item1} found in {dll} replace {cache[mainAssemblyId].Path} as assembly name match dll name");
                cache[mainAssemblyId].Path = dll;
            }
            //Not the same path, ignore it
            else if(cache.ContainsKey(mainAssemblyId) && !dll.Equals(cache[mainAssemblyId].Path, StringComparison.OrdinalIgnoreCase))
            {
                Trace.TraceInformation($"IGNORE : {mainAssemblyId.Item2}, {mainAssemblyId.Item1} found in {dll} already registered from {cache[mainAssemblyId].Path}");
                return cache[mainAssemblyId].Id;
            }

            var assembly = cache[mainAssemblyId];

            //We never know it might need updated. let's do it everytime
            assembly.FrameworkVersion = frameworkVersion;

            //Update References if References Changed
            var newReferences = assemblyReferences.Where(_ => !GacUtil.IsFullNameAssemblyInGAC(_.FullName));
            if (!newReferences.All(_ => Enumerable.Contains(cache[mainAssemblyId].AssemblyReferences, _)))
            {
                Trace.TraceInformation($"UPDATE : Dependencies for {mainAssemblyId.Item2}, {mainAssemblyId.Item1} has changed");
                cache[mainAssemblyId].AssemblyReferences = newReferences;    
            }

            return assembly.Id;
        }
    }
}