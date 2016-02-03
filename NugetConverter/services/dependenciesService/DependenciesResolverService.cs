using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ApiChange.Api.Introspection;
using ApiChange.Api.Introspection.Diff;
using Mono.Cecil;
using NuGet;
using Ullink.NugetConverter.model;
using Ullink.NugetConverter.services.assemblyCacheService;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverter.services.dependenciesService
{
    /// <summary>
    /// The goal of this class is to resolve Assembly Reference for a parent assembly, while it should be one line of
    /// code in simple world, it's not in real world for following reasons :
    /// 1. When the reference as multiple assemblies for the SAME version in the sharing folder. so we need to rely on compatibility analyze to select the right one
    /// 2. The expected version doesn't exist but some others seems present (patch mistmatch, revision mismatch)
    /// 3. The expected official version but a snapshot exist
    /// </summary>
    internal class DependenciesResolverService
    {
        private readonly DependenciesResolverSerializer _dependencies = new DependenciesResolverSerializer();
        private readonly AssemblyCacheService _assemblyCacheService;
        private readonly MappingService _mappingService;
        private readonly bool _useCache;
        private readonly ResolutionLevelEnum _resolutionLevel;
        

        public DependenciesResolverService(AssemblyCacheService assemblyCacheService, MappingService mappingService, bool useCache, ResolutionLevelEnum resolutionLevel)
        {
            _assemblyCacheService = assemblyCacheService;
            _mappingService = mappingService;
            _useCache = useCache;
            _resolutionLevel = resolutionLevel;
        }

        public Tuple<SemanticVersion, string> GetDependencyFromCacheOrResolve(DllReference parentAssembly, AssemblyReference assemblyDependency)
        {
            //RULE 0. it's already in cache
            Tuple<SemanticVersion, string> cachedDependancy = null;
            if(_useCache)
                cachedDependancy = _dependencies.Load(parentAssembly, assemblyDependency.Item2);
            if (cachedDependancy != null)
                return cachedDependancy;

            var assembly = ResolveAssembly(parentAssembly, assemblyDependency);

            if (assembly!=null && _useCache)
                _dependencies.Save(assembly.Item1, assembly.Item2, parentAssembly);

            return assembly;
        }


        private Tuple<SemanticVersion, string> ResolveAssembly(DllReference parentAssembly, AssemblyReference assemblyDependency)
        {
            var assemblies = _assemblyCacheService.Assemblies;
            var conflictingAssemblies = _assemblyCacheService.ConflictingAssemblies;

            //RULE 1. It's an assembly that is known on nuget.org or other. Official or Snapshot doesn't matter
            if(_resolutionLevel!=ResolutionLevelEnum.DontResolveDependancyUsingOfficialRepository)
            {
                var nugetPackage = _mappingService.GetNuGetPackage(_assemblyCacheService, assemblyDependency);
                if (nugetPackage != null)
                {
                    Trace.TraceInformation($"{assemblyDependency.Item2}-{assemblyDependency.Item1} found in official repository");
                    return nugetPackage;
                }
            }

            //RULE 2. The reference is not known but it match with at least one dll present on local folder
            var resolvedAssembly = new Tuple<SemanticVersion, string>(new SemanticVersion(assemblyDependency.Item1), assemblyDependency.Item2);
            if (_assemblyCacheService.Assemblies.ContainsKey(resolvedAssembly) 
                                    && conflictingAssemblies[assemblyDependency].Count(_ => string.IsNullOrEmpty(_.SpecialVersion)) == 1)
            {
                Trace.TraceInformation($"{assemblies[resolvedAssembly].Path} found on local directory");
                return assemblies[resolvedAssembly].Id;
            }

            if (conflictingAssemblies.ContainsKey(assemblyDependency)
                                && conflictingAssemblies[assemblyDependency].Count(_ => string.IsNullOrEmpty(_.SpecialVersion)) > 1
                                && _resolutionLevel != ResolutionLevelEnum.DontResolveDependancyIgnoringMultipleAssembliesWithSameVersion)
                // there might be more than one dll with the expected version...Try to find the best one...
                resolvedAssembly = RetrieveConflictingDependancyFromCacheOrResolve(assemblies, parentAssembly, conflictingAssemblies[assemblyDependency], assemblyDependency);
            

            // RULE 3. Let's try to find if there is a snapshot or a equivalent Version (build number mistmatch only) 
            if (_resolutionLevel != ResolutionLevelEnum.DontResolveDependancyIgnoringBuildNumber)
            {
                var resolvedpackage = CompatibleBuild(assemblyDependency, assemblies, resolvedAssembly);
                if (resolvedpackage != null)
                    return resolvedpackage;
            }


            // RULE 4. Let's try to find if there is Higher Version (Patch & Build only) and take it only if there is only one possibility
            if (_resolutionLevel != ResolutionLevelEnum.DontResolveDependancyIgnoringPatchAndBuildNumber)
            {
                var resolvedpackage = ComptiblePathAndBuild(assemblyDependency, assemblies, resolvedAssembly);
                if (resolvedpackage != null)
                    return resolvedpackage;
            }

            Trace.TraceError($"Unable to find dependency {assemblyDependency.Item2}, {assemblyDependency.Item1}");
            if(_useCache)
                _dependencies.AddUnresolvedDependencies(assemblyDependency);

            return null;

        }

        private static Tuple<SemanticVersion, string> ComptiblePathAndBuild(AssemblyReference assemblyDependency, SortedDictionary<Tuple<SemanticVersion, string>, DllReference> assemblies,
            Tuple<SemanticVersion, string> resolvedAssembly)
        {
            var matchingAssemblies = assemblies.Where(_ => _.Key.Item2.Equals(resolvedAssembly.Item2) && new Version(resolvedAssembly.Item1.Version.Major,resolvedAssembly.Item1.Version.Minor).Equals(new Version(_.Key.Item1.Version.Major, _.Key.Item1.Version.Minor)));

            if (matchingAssemblies.Count() == 1)
            {
                var matchingAssembly = matchingAssemblies.First();
                Trace.TraceWarning($"UPDATE : Not found in Cache : {assemblyDependency.Item2}, {assemblyDependency.Item1} Replaced by {matchingAssembly.Key.Item2}, {matchingAssembly.Key.Item1}");
                return assemblies[matchingAssembly.Key].Id;
            }
            return null;
        }

        private static Tuple<SemanticVersion, string> CompatibleBuild(AssemblyReference assemblyDependency, SortedDictionary<Tuple<SemanticVersion, string>, DllReference> assemblies,
            Tuple<SemanticVersion, string> resolvedAssembly)
        {
            var matchingAssembly = assemblies.FirstOrDefault(_ => _.Key.Item2.Equals(resolvedAssembly.Item2) && new Version(resolvedAssembly.Item1.Version.Major,resolvedAssembly.Item1.Version.Minor,resolvedAssembly.Item1.Version.Build).Equals(new Version(_.Key.Item1.Version.Major,_.Key.Item1.Version.Minor, _.Key.Item1.Version.Build)));

            if (matchingAssembly.Key != null)
            {
                Trace.TraceWarning($"UPDATE : Not found in Cache : {assemblyDependency.Item2}, {assemblyDependency.Item1} Replaced by {matchingAssembly.Key.Item2}, {matchingAssembly.Key.Item1}");
                resolvedAssembly = matchingAssembly.Key;
                return resolvedAssembly;
            }
            return null;
        }

        /// <summary>
        /// Try to retrieve dependancies from cache if it was already compute, if not try to resolve it
        /// </summary>
        internal Tuple<SemanticVersion, string> RetrieveConflictingDependancyFromCacheOrResolve(SortedDictionary<Tuple<SemanticVersion, string>, DllReference> assemblies,
                                                                                                DllReference parentAssembly, SortedSet<SemanticVersion> assembliesSemanticVersions,
                                                                                                AssemblyReference assemblyDependency)
        {
            var asmss = assembliesSemanticVersions
                .Select(_ =>
                {
                    try
                    {
                        return assemblies[new Tuple<SemanticVersion, string>(_, assemblyDependency.Item2)];
                    }
                    catch (KeyNotFoundException exc)
                    {
                        var excption = new VersionNotFoundException(String.Format("versions.ini is not consistent with assemblies.ini. versions.ini is referencing {1}-{0} while it doesn't exist in Assemblies.ini", _, assemblyDependency.Item2), exc);
                        throw excption;
                    }
                })
                //If Parent assembly is a snapshot we include snapshot in dependency otherwise no because nuget doesn't support
                // official package with snapshot dependency
                .Where(_ =>
                    (string.IsNullOrEmpty(parentAssembly.Id.Item1.SpecialVersion) &&
                        !string.IsNullOrEmpty(_.Id.Item1.SpecialVersion))
                        ? false
                        : true
                );

            var result = ResolveConflictingDependancy(asmss, parentAssembly);
            return result;
        }


        /// <summary>
        /// Marked as internal for unit test purpose. In charge of fetching all the version of an assembly and analyze the public
        /// API to know if there were breaking change or not
        /// </summary>
        internal Tuple<SemanticVersion, string> ResolveConflictingDependancy(IEnumerable<DllReference> conflictingAssembliesVersions , DllReference parentAssembly)
        {
            var diffs = new List<AssemblyDiffCollection>();
            DiffPrinter diffPrinter = new DiffPrinter();

            //Skip SemanticVersion until we reach the official SemanticVersion supported
            var asms = conflictingAssembliesVersions.Reverse();
            asms = asms.Reverse();

            //We start to next one as we already set PreviousAPI...
            var previousAPIAssembly = asms.FirstOrDefault();
            asms = asms.Skip(1);
            DllReference selectedVersion = previousAPIAssembly;

            foreach (var apiAssembly in asms)
            {
                diffs = new List<AssemblyDiffCollection>();
                AssemblyDiffCollection diff = null;
                try
                {
                    if (previousAPIAssembly.Path != null && apiAssembly.Path != null)
                        diff =
                            new AssemblyDiffer(previousAPIAssembly.Path, apiAssembly.Path).GenerateTypeDiff(
                                QueryAggregator.AllExternallyVisibleApis);
                    //The previous assembly doesn't have any path we can compare with let's forget it
                    else if (previousAPIAssembly.Path == null && apiAssembly.Path != null)
                    {
                        previousAPIAssembly = apiAssembly;
                        continue;
                    }
                    //Current assembly doesn't have any path we can use to compare, ignore it
                    else
                        continue;
                    
                    //Nothing Was added between those 2 versions...Strange but let's go ahead
                    if (diff.AddedRemovedTypes.Count <= 0 && diff.ChangedTypes.Count <= 0)
                    {
                        previousAPIAssembly = apiAssembly;
                        continue;
                    }


                    diffs.Add(diff);

                    var aggregator = new UsageQueryAggregator();

                    var breakingChangeSearcher =
                        AppDomain.CurrentDomain.CreateInstanceAndUnwrap(
                            "ApiChange.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                            "ApiChange.Api.Introspection.Diff.BreakingChangeSearcher", true,
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null,
                            new object[] {diffs, aggregator}, null, null);
                    AssemblyDefinition assembly =
                        AssemblyLoader.LoadCecilAssembly(parentAssembly.Path, false);

                    if (assembly != null)
                        aggregator.Analyze(assembly);

                    //None of New Types, methods, fields etc. are used in assembly
                    //So we stop here and return the previous one
                    if (aggregator.AssemblyMatches.Count != 0
                        || aggregator.TypeMatches.Count != 0
                        || aggregator.MethodMatches.Count != 0
                        || aggregator.FieldMatches.Count != 0)
                    {
                        selectedVersion = apiAssembly;
                    }

                }
                catch (Exception exc)
                {
                    Trace.TraceError(exc.Message);
                    Trace.TraceError(exc.StackTrace);
                    Trace.TraceError(String.Format("Unable to diff assembly from {0} to {1}", previousAPIAssembly, apiAssembly));
                    //We don't set the prevous assembly because the current raised an exception, let's ignore it totally
                    continue;
                }
                previousAPIAssembly = apiAssembly;
            }
            return new Tuple<SemanticVersion, string>(selectedVersion.Id.Item1, selectedVersion.Id.Item2);
        }
    

        internal void RemoveDependenciesCache(Tuple<SemanticVersion, string> semanticVersion)
        {
            var dependencies = new DependenciesResolverSerializer();
            dependencies.Delete(semanticVersion);
        }
    }
}