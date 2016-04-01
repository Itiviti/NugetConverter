# NugetConverter
Watch and convert in realtime directories storing simple dlls hierarchically to a fully fonctionnal Nuget repository managing dependencies

It happens that .net developers rely on a very basic shared folder to reference their assemblies. this was
before nuget gets popular. The aim of this tool is to syncrhonize a usual shared folder containing assemblies with a
nuget server in realtime. So transitive dependencies are better managed.

For each dll localized in the specified folder it generate a nuget package with associated dependencies. The resolution
of dependencies can be tricky, the current resolution work like this :

# Run it
```
nuget-converter C:\MyAssembliesFolder -d
```

Will run as a deamon and watch all dll changes in `C:\MyAssembliesFolder`. First run can take a while depending of :
* How many conflict you have (multiple dll with same version in different folder)
* How many dll you have
* How slow is you connection to nuget.org (or other if specified)

# Run as a windows service

Using a administrator command prompt :

```
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe <Path\To\NugetConverterService.exe>
```

To specify arguments put in the same folder than NugetConverterService.exe a `CommandLine.config` file containing arguments like this :
```
--source
C:\Path\To\Source
--author
me
--owner
me
--repository
http://remoteserver
```
*Each time arguments are changed you need to reinstal the service. use `InstallUtil.exe /u`*

## Sample


```
nuget-converter -s C:\MyAssembliesFolder -a authorName -o ownerName -r C:\\NugetRepostory
```
Will create all packages for assemblies present in MyAssembliesFolder and will put them in C:\\NugetRepostory

```
nuget-converter -s C:\MyAssembliesFolder -a authorName -o ownerName -r C:\\NugetRepostory -d
```
Will do the same except it will run as a deamon and watch for changes on C:\MyAssembliesFolder

## Checking for non generated packages

Logs are very, very verbose in order to quickly identify why some packages where not generated. you can also
check the files/folders called

- **cache\unresolved** : most important one, list all the references that were not found. to solve it you can
add various mapping. see after.
- **cache\assemblies.ini** : cache that hold all the assemblies definition found in source folder
- **cache\versions.ini** : contains all the conflicting version for assemblies found in source folder
- **cache** : contains all the dependencies for each dll (*.ini except versions.ini & assemblies.ini)
- **nuget-repository** : package caches used to detect if a package need to be re-generated


## Command option


```
-d, --deamon                 Start the convert as deamon to generate
-f, --filename               Filename of an assembly to re-generate
-s, --source                 Required. folder location to generate package from
-r, --repository             Required. repository to push to. can be remote or local repository
-v, --credential             User and password for repository in form of user:password
-p, --official-repository    repository to retrieve package from. can be remote or local repository
-n, --no-cache               don't use cache, will slow down the processing
-l, --resolution-level       Define the level to resolve assembly if match is not exact
-a, --author                 Required. Author used for the package
-o, --owner                  Required. Owner use for the package
-b, --slack-username         name to use to slack
-c, --slack-channel          channel to use
-u, --slack-url              Slack api url for hook
-p, --proxy-url              proxy to use
-l, --proxy-white-list       proxy list separated by ','
```

By default all resolution steps are activated, it can be deactivated by adding some of this list :

| Level | Code                                                           |
| ----- | ---------------------------------------------------------------|
| 1     | DontIgnoreEmptyVersion                                         |
| 2     | DontUseFolderVersion                                           |
| 4     | DontUseFolderVersionRecursivly                                 | 
| 8     | DontResolveDependancyUsingOfficialRepository                   |
| 16    | DontResolveDependancyIgnoringBuildNumber                       |
| 32    | DontResolveDependancyIgnoringPatchAndBuildNumber               |
| 64    | DontResolveDependancyIgnoringMultipleAssembliesWithSameVersion |


Ie. setting `3` (1+2) will not perform `DontIgnoreEmptyVersion` & `DontUseFolderVersionRecursivly`

### DontIgnoreEmptyVersion

Will try to generate nuget package for assembly with verion set to 0.0.0.0

### DontUseFolderVersion

nugetconverter rely on folder name to extract version. This is done like this to manage snapshot version
of assembly:
```
C:\dll-1\1.1.10-121201\my-awesome.dll
```
The version will be retrieve from folder name `1.1.10-121201` and will generate (depending of mapping
[Set specific version for Nuget package]) the package named my-awesome-1.1.19-121201. But you might prefer to rely on
Assembly Version. in this can you can set this Option and folder will be ignored

### DontUseFolderVersionRecursivly

nugetconverter rely on folder name to extract version and search for it recursivly:
```
C:\dll-1\1.1.10-121201\net\my-awesome.dll
```
The version will be retrieve from folder named `1.1.10-121201`, not the last one. If this option is set it will stop
to the first folder

### DontResolveDependancyUsingOfficialRepository

nugetconverter search for a reference on nuget.org (or repository specified by `--official-repository` if
any ). You can disable this by setting this option.

### DontResolveDependancyIgnoringBuildNumber

nugetconverter try to match a reference by ignoring build number :
```
Assembly-B
  |__ Assembly-A.1.2.1.2
```
Assembly A availables from remote repository or locally :
```
Assembly-A.1.2.1.0
Assembly-A.1.2.1.4
Assembly-A.1.2.1.6
Assembly-A.1.2.2.0
```
Assembly B reference version `1.2.1.2` of assembly A, but we can't find it. in this case nugetconverter will replace
the version `1.2.1.2` by `1.2.1.6`, ie the most recent patch.
Set this option to stop this behavior.

### DontResolveDependancyIgnoringPatchAndBuildNumber
nugetconverter try to match a reference by ignoring patch number & build number if there is only one match :
```
Assembly-B
  |__ Assembly-A.1.2.1.2
```
Assembly A availables from remote repository or locally :
```
Assembly-A.1.1.3.0
Assembly-A.1.2.5.4
Assembly-A.1.5.0.6
Assembly-A.1.6.1.0
```
Assembly B reference version `1.2.1.2` of assembly A, but we can't find it. in this case nugetconverter will replace
the version `1.2.1.2` by `1.2.5.4`. It will do this only if there is one version starting by 1.2, if it's not the case
it will failed and will not generate the package
Set this option to stop this behavior.


### DontResolveDependancyIgnoringMultipleAssembliesWithSameVersion

it happens that multiple assembly with the same name have same version but in different folder :
```
C:\dll-1\1.1.10-121201\Assembly-A.dll[AssemblyVersion:1.0.0]
C:\dll-1\1.2.10-121201\Assembly-A.dll[AssemblyVersion:1.0.0]
C:\dll-1\1.3.10-121201\Assembly-A.dll[AssemblyVersion:1.0.0]
C:\dll-1\1.4.10-121201\Assembly-A.dll[AssemblyVersion:1.0.0]
C:\dll-1\1.4.10-121201\Assembly-A.dll[AssemblyVersion:1.0.0]
```
and assembly-B reference, off course :
```
Assembly-B.1.2.0
  |__ Assembly-A.1.0.0.0
```
How can we decide which one is the "official" one ?. As mention in [Conflicting Version] we rely on public method check
to find the best one. Set this option to stop this behavior. in case of conflicting version package generation will fail.

# Configuration file

The default configuration file `nugetconverter.ini` should be put in the source assemblies folder at root level. You can
add other extra configuration file in all sub-folders to adjust behavior. 

## Ignored dll


By default Dll with version 0.0.0.0 are ignored

You can add extra ignored folder using `nugetconverter.ini` in specific folder containing :
```
[global]
ignore=true
```

## Simple Mapping

```
[dependencies.mapping]
Rx-Main=^System\.Reactive.*
```
When you find any references in dll starting with `System.Reactive` replace it by the package named 'Rx-Main'

### Set specific package for a reference

```
[dependencies.mapping]
ICSharpCode.TextEditor|3.2.1.6466=^ICSharpCode.TextEditor.*
```

When you find any references starting with ICSharpCode.TextEditor replace it by the package named
`ICSharpCode.TextEditor` with the specific version `3.2.1.6466`

### Set specific package for a reference and version

```
[dependencies.mapping]
my-dll-renamed-new-name=my-dll-old-name\.1\.[1-9]+[0-9]+
my-dll-renamed-new-name=my-dll-old-name\.[2-9]\.[0-9]+
```

When reference of `my-dll-old-name` is > 1.18 the package added will be `my-dll-renamed-new-name`.

### Set specific name for package

```
[package.mapping]
my-tools=^(?<my-tools>mytools).+
```

Will replace `mytools` by `my-tools` in the nupkg. ie `mytoolsfoo.nupkg` will become `my-toolsfoo.nupkg`

### Set specific version for package

In the root configuration file you can add rules to parse specific version, by default the converter will ignore all
assembly where folder version will not match a version. ie. `C:\dll-1\1.1.10\my-awesome.dll`
the 4 digits ofrecognize two kind of element
in a special version :
* `build` : if set, then the nuget package will be created with the build element added to the version
(ie. `C:\dll-1\1.1.10-121201\my-awesome.dll` will become `my-awesome.1.1.10-121201.nupkg`
* `snapshot` : if set, then when the nuget package will be created it will marked it as unstable

if you have the following folder structure : 
```
C:\dll-1\1.1.10-121201-snapshot\my-awesome.dll
C:\dll-1\1.1.10-121201\my-awesome.dll
```
you can add into the `nugetconverter.ini` the following rules :

```
[snapshot.mapping]
gradle-buildnumber-snapshot=(?<build>[0-9]+)-(?<snapshot>snapshot)+
gradle-buildnumber=(?<build>[0-9]+)
```
Which means, if the version folder (`1.1.10-121201-snapshot`) match this regexp then mark this version as unstable
(`<snapshot>`) and add `121201` to the version in nuget.

## Fixing simpe circular dependency

For specific case you can specify if there is circular dependencies that must be fixed. It currently support only
one level circular dependencies

```
[dependencies.circular]
java-jaxen=java-jdom|1.0.0.0
```

Will fix the circular reference betwee jaxen & jdom by using 1.0.0.0 version

# How it works

First, at startup it generate a cache of assemblies folder and keep in in sync if any change occured and stored this
cache under `cache` folder to avoid re-creating the cache at each startup. This is the aim of `AssemblyCacheService`

Then generate a nuget package for each dll and try to resolve each dependencies according to the following rules
(in that order) :
* If there is any manual mapping between referenced dll and nuget package take it. see [Custom Mapping]
* If there is a dll on dll folder [Version Matching|matching] this reference then take it (the reference dll will be a package anyway)
* If there is a package with the same exact name on the remote repository (ie, nuget.org) then reference this one


## Version Matching


By default version retained for each assembly present on the dll directory will be the `AssemblyVersion` defined in
metadata of assembly, except if dll directory structure is containing the version ie :
```
C:\dll-1\1.1.10\my-awesome.dll
C:\foo-dll\1.1.10\my-foo-dll.dll
C:\foo-dll\1.1\1.1.12\my-foo-dll.dll
...
```

In this case the version kept will be the folder name.

## Conflicting Version

In early stage of .net developpement it happens that multiple dll shared the same version, but not the same code, ie :
```
\\mySharedFolder\myassemblyname\1.1.3\myassemblyname.dll
\\mySharedFolder\myassemblyname\1.3.3\myassemblyname.dll
```

where both myassemblyname.dll have the same version internaly. ie 1.0.0 (off course). In this case the resolution is
based on work done by https://apichange.codeplex.com/. We compare public methods and chose the assembly matching the
best with parent assembly.


# TODO
* Be able to configure snapshot name
* Detect circular Dependencies to avoid generating the package
