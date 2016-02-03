using System;
using System.Collections.Generic;
using NuGet;

namespace Ullink.NugetConverter.model
{
    public class DllReference
    {
        public IEnumerable<AssemblyReference> AssemblyReferences { get; set; }
        readonly Tuple<SemanticVersion, string> _id;
        
        public DllReference(string path, string frameworkVersion,  Tuple<SemanticVersion, string> id, IEnumerable<AssemblyReference> assemblyReferences)
        {
            AssemblyReferences = assemblyReferences;

            if (frameworkVersion == null)
                throw new ArgumentNullException(nameof(frameworkVersion));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (id==null)
                throw new ArgumentNullException(nameof(id));

            Path = path;
            FrameworkVersion = frameworkVersion;
            _id = id;
        }

        public string FrameworkVersion { get; set; }

        public Tuple<SemanticVersion, string> Id => _id;

        public string Path { get; set; }

        public override string ToString()
        {
            return _id.Item2 + ", " + _id.Item1;
        }

        public override bool Equals(object obj)
        {
            return Id.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}