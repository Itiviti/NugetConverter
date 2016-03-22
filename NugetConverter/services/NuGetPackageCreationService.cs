using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fody.DependencyInjection;
using NuGet;
using Slack.Webhooks;
using Ullink.NugetConverter.model;
using Ullink.NugetConverter.services.assemblyCacheService;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.services.dependenciesService;

namespace Ullink.NugetConverter.services
{
    [Configurable]
    internal class NuGetPackageCreationService
    {
        private const string NugetRepository = @".\nuget-repository\";
        private readonly AssemblyCacheService _assemblyCacheService;
        private readonly MappingService _mappingService;
        private readonly ConfigurationService _configurationService;
        private readonly DependenciesResolverService _dependenciesResolverService;
        private readonly string _repository;
        private readonly string _credential;
        private readonly string _owner;
        private readonly string _author;
        private readonly string _slackUrl;
        private readonly string _slackChannel;
        private readonly string _slackUsername;
        private readonly bool _useCache;
        public const string NugetSeparator = ".";
        
        public NuGetPackageCreationService(AssemblyCacheService assemblyCacheService, MappingService mappingService, ConfigurationService configurationService, DependenciesResolverService dependenciesResolverService, bool useCache,
                                          string repository, string credential, string owner,string author, string slackUrl, string slackChannel, string slackUsername)
        {
            _assemblyCacheService = assemblyCacheService;
            _mappingService = mappingService;
            _configurationService = configurationService;
            _dependenciesResolverService = dependenciesResolverService;
            _useCache = useCache;
            _repository = repository;
            _credential = credential;
            _owner = owner;
            _author = author;
            _slackUrl = slackUrl;
            _slackChannel = slackChannel;
            _slackUsername = slackUsername;
        }

        public void SyncAssembliesPackages()
        {
            //From Assembly Group we need to create special nuget Package ( due to API LEVEL ) :
            Parallel.ForEach(_assemblyCacheService.Assemblies, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, (_) => CreatePackageIfNeeded(_, false));
        }

        public void CreatePackage(Tuple<SemanticVersion, string> assemblyId, bool logToSlack)
        {
            var group = _assemblyCacheService.Assemblies[assemblyId];
            CreatePackageIfNeeded(new KeyValuePair<Tuple<SemanticVersion, string>, DllReference>(assemblyId, group), logToSlack);
        }

        private bool CreatePackageIfNeeded(KeyValuePair<Tuple<SemanticVersion, string>, DllReference> assemblyId, bool logToSlack)
        {
            var packageName = NugetRepository + assemblyId.Key.Item2 + NugetSeparator + assemblyId.Key.Item1 + ".nupkg";
            if (_useCache && File.Exists(packageName))
            {
                Trace.TraceInformation("Creation of {0} skipped already in local cache", packageName);
                return false;
            }

            InnerCreatePackage(assemblyId, logToSlack);
            return true;
        }

        private void InnerCreatePackage(KeyValuePair<Tuple<SemanticVersion, string>, DllReference> mainAssembly, bool logToSlack)
        {
            try
            {
                var metadata = new ManifestMetadata()
                {
                    Authors = _author,
                    Version = mainAssembly.Key.Item1.ToString(),
                    Id = mainAssembly.Key.Item2,
                    Description = "automatically created package using nuget-converter for " + mainAssembly.Key.Item2,
                    Owners = _owner,
                    RequireLicenseAcceptance = false
                };

                var builder = new PackageBuilder();
                var dependencies = new List<PackageDependency>();

                //Common Dependancies
                var asmReferencies = mainAssembly.Value;
                Trace.TraceInformation($"Resolving Cached Dependencies for {asmReferencies.Id.Item2}-{asmReferencies.Id.Item1}...");
                foreach (var dependency in asmReferencies.AssemblyReferences)
                {
                    Trace.TraceInformation($"Resolving Dependency {dependency.Item2}-{dependency.Item1}...");
                    //CIRCULAR REFERENCE STEP 1 : This dependency reference the current package, let's avoid referencing it.
                    if (IsDependencyCircular(mainAssembly, dependency))
                        continue;
                    
                    var packageId = _dependenciesResolverService.GetDependencyFromCacheOrResolve(mainAssembly.Value, dependency);
                    if (packageId == null)
                        throw new VersionNotFoundException($"{dependency.Item2}-{dependency.Item1} was not found on official repository/assemblies directory and none of nugetconverter.ini files on assemblies directory provide custom mapping to help me.");

                    //Package is already added or it reference it self we can ignore it 
                    if (!dependencies.Any(_ => _.Id.Equals(packageId.Item2))
                            // Package already referenced We can ignore it
                             && (packageId.Item2 != mainAssembly.Key.Item2 || packageId.Item1 != mainAssembly.Key.Item1))
                    {
                        var semanticVersion = new VersionSpec {MinVersion = packageId.Item1, IsMinInclusive = true};
                        dependencies.Add(new PackageDependency(packageId.Item2, semanticVersion));

                        //CIRCULAR REFERENCE STEP 2 :
                        //We removed the circular dependency previously
                        //to be sure everything is still working we add it again at an upper level.
                        // LibA
                        //   |_jaxen
                        //     |_jdom
                        //       |_jaxen
                        //         |_jdom
                        //         ...
                        //Into
                        //// LibA
                        //   |_jaxen
                        //   |_jdom
                        //     |_jaxen
                        var circularDependency = _configurationService.Get().CircularMapping.FirstOrDefault(__ => packageId.Item2 == __.Item1 && dependencies.All(_ => _.Id != __.Item2.Item1));
                        if(circularDependency!=null)
                                dependencies.Add(new PackageDependency(circularDependency.Item2.Item1, new VersionSpec(circularDependency.Item2.Item2)));
                    }
                }

                var manifestFileDll = new PhysicalPackageFile();
                manifestFileDll.SourcePath = asmReferencies.Path;
                manifestFileDll.TargetPath = @"lib\" + asmReferencies.FrameworkVersion + @"\" + Path.GetFileName(asmReferencies.Path);
                builder.Files.Add(manifestFileDll);

                var pdb = asmReferencies.Path.Replace(".dll", ".pdb");
                if (File.Exists(pdb))
                {
                    var manifestFilePdb = new PhysicalPackageFile();
                    manifestFilePdb.SourcePath = pdb;
                    manifestFilePdb.TargetPath = @"lib\" + asmReferencies.FrameworkVersion + @"\" + Path.GetFileNameWithoutExtension(asmReferencies.Path) + ".pdb";
                    builder.Files.Add(manifestFilePdb);
                }

                var xml = asmReferencies.Path.Replace(".dll", ".xml");
                if (File.Exists(xml))
                {
                    var manifestFileXml = new PhysicalPackageFile();
                    manifestFileXml.SourcePath = xml;
                    manifestFileXml.TargetPath = @"lib\" + asmReferencies.FrameworkVersion + @"\" + Path.GetFileNameWithoutExtension(asmReferencies.Path) + ".xml";
                    builder.Files.Add(manifestFileXml);
                }

                builder.DependencySets.Add(new PackageDependencySet(VersionUtility.ParseFrameworkName(asmReferencies.FrameworkVersion), dependencies));

                WritePackage(builder, metadata);

                // We absolutly need to generate extra package and keep the original
                // one in order to check cyclic dependancies
                var configuration = _configurationService.Get();
                foreach (var packageMapping in configuration.PackageMapping)
                {
                    var packageMappingMatching = packageMapping.Item2.Match(mainAssembly.Key.Item2);
                    if (packageMappingMatching.Success)
                    {
                        var group = packageMappingMatching.Groups[packageMapping.Item1];
                        if (group.Success)
                        {
                            metadata.Id = mainAssembly.Key.Item2.Replace(group.Value, packageMapping.Item1);
                            Trace.TraceWarning($"nugetconverter.ini asked to generate an extra {metadata.Id} package");
                            WritePackage(builder, metadata);
                        }
                    }

                }
            }
            catch (Exception exception)
            {
                var package = NugetRepository + mainAssembly.Key.Item2 + NugetSeparator + mainAssembly.Key.Item1 + ".nupkg";
                SendError(logToSlack, package, exception.Message);

                if (File.Exists(package))
                    File.Delete(package);
            }
        }

        private bool IsDependencyCircular(KeyValuePair<Tuple<SemanticVersion, string>, DllReference> mainAssembly, AssemblyReference dependency)
        {
            foreach (var circularDef in _configurationService.Get().CircularMapping)
            {
                if (circularDef.Item1.Equals(mainAssembly.Value.Id.Item2) &&
                    circularDef.Item2.Item1.Equals(dependency.Item2))
                {
                    Trace.TraceWarning($"Circular dependency detected between {mainAssembly.Key.Item2} and {dependency.Item2}");
                    return true;
                }
                    
             }
            return false;
        }

        private void SendError(bool logToSlack, string package, string exception)
        {
            Trace.TraceError("Unable to create package {0}, {1}", package, exception);
            if (logToSlack && _slackUrl!=null)
            {
                try
                {
                    var slackClient = new SlackClient(_slackUrl);
                    var slackMessage = new SlackMessage
                    {
                        Channel = _slackChannel,
                        Text = $"*Upload for {package} failed*\n{exception}",
                        Username = _slackUsername
                    };
                    slackClient.Post(slackMessage);
                }
                catch (Exception slackException)
                {
                    Trace.TraceError($"Unable to send message to slack {slackException.Message}");
                }
            }
        }

        void WritePackage(PackageBuilder builder, ManifestMetadata metadata)
        {
            //Create the Nuget package
            if (!Directory.Exists(NugetRepository)) Directory.CreateDirectory(NugetRepository);

            var package = NugetRepository + metadata.Id + NugetSeparator + metadata.Version + ".nupkg";
            using (FileStream stream = File.Open(package, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    builder.Populate(metadata);
                    builder.Save(stream);
                }
                Trace.TraceInformation("Package {0} created locally", package);
                
                var localRepo = PackageRepositoryFactory.Default.CreateRepository(new DirectoryInfo("./nuget-repository").FullName);
                var nuGetPackage = PackageHelper.ResolvePackage(localRepo, metadata.Id, new SemanticVersion(metadata.Version));
                var packageFile = new FileInfo(package);
                var size = packageFile.Length;
                var ps = new PackageServer(_repository, "None");
                ps.PushPackage(_credential, nuGetPackage, size, 50000, false);
               
                Trace.TraceInformation("Package {0} uploaded to {1}", package, _repository);
        }

        public void RemovePackage(Tuple<SemanticVersion, string> groupId)
        {
            _dependenciesResolverService.RemoveDependenciesCache(groupId);
            var package = NugetRepository + groupId.Item2 + NugetSeparator + groupId.Item1 + ".nupkg";
            if(!File.Exists(package))
                File.Delete(NugetRepository + groupId.Item2 + NugetSeparator + groupId.Item1 + ".nupkg");
        }
    }
}