using System;
using NuGet;

namespace Ullink.NugetConverter.model
{
    public class FileAssemblyVersion : Tuple<SemanticVersion, string>
    {
        public FileAssemblyVersion(SemanticVersion folderVersion, string assemblyName, Version assemblyVersion)
            : base(folderVersion, assemblyName)
        {
            AssemblyVersion = assemblyVersion;
        }

        /// <summary>
        /// Version between Folder Name & Assembly Name were not the same
        /// </summary>
        public bool VersionsMismatch => AssemblyVersion.CompareTo(Item1.Version) != 0;

        public bool IsSnapshot => !string.IsNullOrEmpty(Item1.SpecialVersion);

        public Version AssemblyVersion { get; }
    }
}

