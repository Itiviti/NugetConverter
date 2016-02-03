using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ullink.NugetConverter.utils
{
    [Flags]
    public enum ResolutionLevelEnum
    {
        //Take in account version with 0.0.0.0
        All = 0,
        DontIgnoreEmptyVersion = 1,
        DontUseFolderVersion = 2,
        DontUseFolderVersionRecursivly = 4,
        DontResolveDependancyUsingOfficialRepository=8,
        DontResolveDependancyIgnoringBuildNumber=16,
        DontResolveDependancyIgnoringPatchAndBuildNumber=32,
        DontResolveDependancyIgnoringMultipleAssembliesWithSameVersion=64
    }
}
