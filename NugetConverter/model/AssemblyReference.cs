using System;
using Mono.Cecil;

namespace Ullink.NugetConverter.model
{
    public  class AssemblyReference : Tuple<Version, string>
    {
        public AssemblyReference(string name, Version version, string fullName) : base(version, name)
        {
            FullName = fullName;
        }

        public string FullName { get; }
    }
}
