using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using NuGet;
using Ullink.NugetConverter.model;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverter.services.assemblyCacheService
{
    /// <summary>
    /// The goal of this class is just to retrieve correct  version from S:\ folder names.
    /// Rules ordered by priority :
    /// 1. If Snapshot version we rely on folder to retrieve name & verion
    /// 2. If Assembly version is set to 0.0.0.0 we retrieve name & version on folder
    /// 3. If Assembly is an api we can't rely on Assembly Version has it's not correct
    /// 4. Return Assembly Version
    /// </summary>
    public class VersionResolverService
    {
        private readonly ConfigurationService _service;
        private readonly ResolutionLevelEnum _resolutionLevel;
        private readonly Version EmptyVersion = new Version(0,0,0,0);

        public VersionResolverService(ConfigurationService service, ResolutionLevelEnum  resolutionLevel)
        {
            _service = service;
            _resolutionLevel = resolutionLevel;
        }

        public FileAssemblyVersion ResolveVersion(AssemblyDefinition ad, string path)
        {
            //Really, let's not waste time on such dll...
            if (ad.Name.Version.Equals(EmptyVersion) && _resolutionLevel!=ResolutionLevelEnum.DontIgnoreEmptyVersion)
            {
                Trace.TraceWarning("AssemblyAttributes {0} Version is 0.0.0.0", path);
                return null;
            }

            if (_resolutionLevel != ResolutionLevelEnum.DontUseFolderVersion)
            {
                //Handle Snapshot : Try to resolve using folder
                return Resolve(ad, path);
            }

            //TODO: Use AssemblyInformationalVersionAttribute just in case
            return new FileAssemblyVersion(new SemanticVersion(ad.Name.Version), ad.Name.Name, ad.Name.Version);
        }

        /// <summary>
        /// 
        /// </summary>
        internal SemanticVersion ParseFolderVersion(string assemblyPath)
        {
            //Let's avoid complex management of Lower/Upper case
            assemblyPath = assemblyPath.ToLower();
            var path = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));

            var result = InnerSemanticVersion(path.Name);
        
            if(result==null && path.Parent!=null && _resolutionLevel!=ResolutionLevelEnum.DontUseFolderVersionRecursivly)
                result = InnerSemanticVersion(path.Parent.Name);
        
            if(result==null)
                Trace.TraceInformation($"SemanticVersion is not Parseable for {assemblyPath}");
            else
                Trace.TraceInformation($"Version for {assemblyPath} is {result}");

            return result;
        }

        private SemanticVersion InnerSemanticVersion(string expectedSemanticVersion)
        {
            //Every valid Version in following format x.x.x_xx-abxd
            Regex regEx = new Regex(@"(?<SemanticVersion>^[0-9]+\.[0-9_.]+)-*(?<Tail>.*)");
            var matches = regEx.Match(expectedSemanticVersion);
            if (!matches.Success)
                return null;

            // Try to extract snapshot & build change
            var SemanticVersion = matches.Groups["SemanticVersion"].Value;
            var tail = matches.Groups["Tail"].Value;

            string build;
            bool isSnapshot;
        
            var succeed = TryParseTail(tail, out build, out isSnapshot);
            if (!succeed)
            {
                Trace.TraceError("Unable to parse Special Version '{0}'", tail );
                return null;
            }

            var verionSplit = SemanticVersion.Split('.', '_', '-');
            var SemanticVersionArray = new int[4];
            //Some of the folder have more than 4 digit...
            for (int i = 0; i < verionSplit.Length && i < 4; i++)
            {
                var VersionNumber = verionSplit[i];
                int VersionInt;
                int.TryParse(VersionNumber, out VersionInt);
                SemanticVersionArray[i] = VersionInt;
            }

            var resolvedVersion =
                new SemanticVersion(
                    new Version(SemanticVersionArray[0], SemanticVersionArray[1], SemanticVersionArray[2],
                        SemanticVersionArray[3]),
                    isSnapshot ?  
                        string.IsNullOrEmpty(build) ? 
                            _service.Get().SnapshotName : 
                            (_service.Get().SnapshotName + "-" + build)
                        : null 
                    );
            return resolvedVersion;
        }

        /// <summary>
        /// 1. If snapshot return folder Name
        /// 2. Version mismathch
        /// </summary>
        /// <param name="snapshotOnly">True: If the assembly is a snapshot then return the Version, otherwise return null even if version is ok</param>
        /// <param name="useVersionFromFolder">False: use AssemblyVersion to extract version, True use Folder name to extract version and if it failed 
        /// return null</param>
        private FileAssemblyVersion Resolve(AssemblyDefinition ad, string assemblyPath)
        {
            var folderVersion = ParseFolderVersion(assemblyPath);
            var assemblyVersion = new Tuple<SemanticVersion, string>(new SemanticVersion(ad.Name.Version), ad.Name.Name);

            if (folderVersion == null)
                return null;

            //SNAPSHOT
            if (!string.IsNullOrEmpty(folderVersion.SpecialVersion))
                return new FileAssemblyVersion(folderVersion, ad.Name.Name, ad.Name.Version);

            var versionMismatch = assemblyVersion.Item1.Version.CompareTo(folderVersion.Version);
            if(versionMismatch!=0)
                Trace.TraceWarning($"MISMATCH : Version for {ad.Name.Name} mismatch between folder ({folderVersion.Version}) and Assembly ({assemblyVersion.Item1.Version}). folder version took.");
            
            //Version between Folder & Assembly Version mistmatch
            return new FileAssemblyVersion(folderVersion, ad.Name.Name, assemblyVersion.Item1.Version);
        }

        internal bool TryParseTail(string tail, out string build, out bool isSnapshot)
        {
            build = null;
            isSnapshot = false;

            if (string.IsNullOrWhiteSpace(tail) )
                return true;

            // SemVer Standard doesn't accept :
            // snapshot starting with Nu
            // snapshot name containing '.'
            var startingWithNumber = new Regex(@"^(?<number>[0-9]+).*");
            var IsStartingWithNumber = startingWithNumber.Match(tail);
            if (IsStartingWithNumber.Success)
                tail = "v-" + tail;
            tail = tail.Replace(".", "-");

            var configuration = _service.Get();
            foreach (var regexp in configuration.SnapshotMapping)
            {
                var successMatch = regexp.Item2.Match(tail);
                if (successMatch.Success)
                {
                    var buildResult = successMatch.Groups["build"];
                    if (buildResult.Success)
                        build = buildResult.Value;
                    isSnapshot = successMatch.Groups["snapshot"].Success;
                    Trace.TraceInformation($"Special version match regexp {regexp.Item2}");
                    return true;
                }
            }
            
            //Really Don't know what is this fucking cheat....
            return false;
        }
    }
}