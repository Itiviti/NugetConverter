using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using Ullink.NugetConverter.services.assemblyCacheService;

namespace Ullink.NugetConverter.services.configuration
{
    public class MappingService
    {
        private readonly Dictionary<Tuple<Version, string>, Tuple<SemanticVersion, string>> _officialNugetPackageCache
                                                                    = new Dictionary<Tuple<Version, string>, Tuple<SemanticVersion, string>>();

        private readonly string _rootPath;
        private readonly string _officialRepository;

        public MappingService(string rootPath, string officialRepository)
        {
            _rootPath = rootPath;
            _officialRepository = officialRepository;
        }

        public bool IsCiruclar(Tuple<SemanticVersion, string> parent , Tuple<Version, string> dependancy, string frameworkVersion)
        {
            var package = GetFromOfficialRepository(dependancy);
            if(package==null)
                package = GetFromCacheRepository(dependancy);
            var dep = package.GetCompatiblePackageDependencies(new FrameworkName(frameworkVersion));
            return dep.Any(_ => _.Id == dependancy.Item2);
        }

        private IPackage GetFromCacheRepository(Tuple<Version, string> dependancy)
        {
            return null;
        }

        public Tuple<SemanticVersion, string> GetNuGetPackage(AssemblyCacheService _service, Tuple<Version, string> name)
        {
            Configuration configuration = null;
            ConfigurationGetter configurationGetter = new ConfigurationGetter(new ConfigurationFileReader());
            
            //Try to see if some manual mapping is set on the dll directory
            var result = _service.Assemblies.Union(_service.IgnoredAssemblies);
            var groups = result.GroupBy(_ => _.Key.Item2);
            var assemblyVersions = groups.FirstOrDefault(_ => _.Key == name.Item2);

            if (assemblyVersions != null)
            {
                var sortedAssemblyVersions = assemblyVersions.Where(_ => _.Key.Item1.Version.Equals(name.Item1)).ToList();
                //No exact matching, let's find in a larger way
                if (!sortedAssemblyVersions.Any())
                    sortedAssemblyVersions = assemblyVersions.Where(_ => _.Key.Item1.Version >= name.Item1).ToList();
                //No exact matching, let's find in an even larger way
                if (!sortedAssemblyVersions.Any())
                    sortedAssemblyVersions = assemblyVersions.Where(_ => _.Key.Item1.Version.Major >= name.Item1.Major).ToList();
                sortedAssemblyVersions.Sort((pair, valuePair) => pair.Key.Item1.CompareTo(valuePair.Key.Item1));

                configuration = sortedAssemblyVersions.Aggregate(new Configuration(), (tmpConfiguration, pairs) =>
                            configurationGetter.Merge(tmpConfiguration, configurationGetter.Get(pairs.Value.Path)));
            }
            //Unable to find a dll name close to the package name, let's try to find in root .ini
            else
            {
                configuration = configurationGetter.Get(_rootPath);
                
            }

            var nugetPackage = configuration.NugetPackagesMapping.FirstOrDefault(_ => _.Item2.Regex.Match(name.Item2 + NuGetPackageCreationService.NugetSeparator + name.Item1).Success);
            if (nugetPackage!=null)
            {
                Trace.TraceInformation($"Manual configuration ask to used {nugetPackage.Item1} instead of {name.Item2}");
                return new Tuple<SemanticVersion, string>(nugetPackage.Item2.SemanticVersion??new SemanticVersion(name.Item1), nugetPackage.Item1);
            }

            //Try to see if it exist on nuget official repository
            var nuGetPackage = GetOfficialNuGetPackage(name);
            if (nuGetPackage != null)
                return nuGetPackage;

            Trace.TraceInformation($"No mapping information for {name.Item2}");
            return null;
        }

        private Tuple<SemanticVersion, string> GetOfficialNuGetPackage(Tuple<Version, string> name)
        {
            if (_officialNugetPackageCache.ContainsKey(name))
            {
                var offcialNugetPackage = _officialNugetPackageCache[name];
                Trace.TraceInformation($"{offcialNugetPackage.Item2}-{offcialNugetPackage.Item1} previously found in remote repository.");
                return offcialNugetPackage;
            }

            var package = GetFromOfficialRepository(name);
            if (package != null)
            {
                var officialPackage = new Tuple<SemanticVersion, string>(package.Version, package.Id);
                //BOOOOO. This is ugly. we reduce here race condition "Key already added"...
                //Would be better to have a syncronisation mechanism...or not
                if (_officialNugetPackageCache.ContainsKey(name))
                {
                    var offcialNugetPackage = _officialNugetPackageCache[name];
                    Trace.TraceInformation($"{offcialNugetPackage.Item2}-{offcialNugetPackage.Item1} previously found in remote repository.");
                    return offcialNugetPackage;
                }
                _officialNugetPackageCache.Add(name, officialPackage);
                Trace.TraceInformation($"{name.Item2}-{name.Item1} found in remote repository.");
                return officialPackage;
            }
                
            Trace.TraceInformation($"{name.Item2}-{name.Item1} NOT found in remote repository.");
            return null;
        }

        private IPackage GetFromOfficialRepository(Tuple<Version, string> reference)
        {
            //Connect to the official package repository
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(_officialRepository);
            //Get the list of all NuGet packages with ID 'EntityFramework'
            var name = reference.Item2;
            var path = name.Split('.');
            IPackage result;
            for(int index = path.Length-1; index>=0; index--)
            {
                name = string.Join(".",path, 0, index+1);
                try
                {
                    try
                    {
                        result = repo.FindPackage(name, new SemanticVersion(reference.Item1),
                            new DefaultConstraintProvider(), true, true);
                        //Depending of Weather and Time, usually an exception is throw but not always...
                        if (result != null)
                            return result;
                    }
                    catch (Exception exception)
                    {
                        Trace.TraceInformation($"Can't find from official nuget repository official version for {reference.Item1}-{reference.Item2}, {exception.Message}");
                    }

                    result = repo.FindPackage(name, new SemanticVersion(reference.Item1, "SNAPSHOT"), new DefaultConstraintProvider(), true, true);
                    if (result != null)
                    {
                        Trace.TraceInformation($"Can't find from official nuget repository official version for {reference.Item1}-{reference.Item2}, SNAPSHOT Version used");
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    Trace.TraceInformation($"Can't find from official nuget repository {exception.Message}");
                }   
            }

            Trace.TraceInformation($"Can't find from official nuget repository {reference.Item2}-{reference.Item1}");
            return null;
        }
    } 
}
