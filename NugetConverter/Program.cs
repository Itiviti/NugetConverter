using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Fody.DependencyInjection;
using Ullink.NugetConverter.services;
using Ullink.NugetConverter.services.assemblyCacheService;
using Ullink.NugetConverter.services.configuration;
using Ullink.NugetConverter.services.dependenciesService;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverter
{    
    public class Program
    {
        public static ContainerBuilder Builder;
        public static IContainer Container;


        public static int Main(string[] args)
        {
            try
            {
                var result = Run(args);
                if (result != null)
                    return 1;
            }
            catch (Exception)
            {
                return 2;
            }
            return 0;
        }

        public static string Run(string[] args)
        {
            string error = null;
            var process = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location));
            if (process.Length > 1)
            {
                error = "An instance of ul-nuget-converter is already running...";
                return error;
            }

            //Un comment this line if you want to force logging of Api change analyze
            //System.Environment.SetEnvironmentVariable("_Trace", "Console; ApiChange.* all");
            
            var options = new CommandLineOptions();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                error = $"Error while parsing options: {args.Aggregate("",(current, next) => current + "|" + next)}";
                return error;
            }

            //When filename is set let's recompute everything
            if (!string.IsNullOrEmpty(options.Filename))
                options.NoCache = true;

            if (string.IsNullOrEmpty(options.OfficialRepository))
                options.OfficialRepository = "https://www.nuget.org/api/v2/";

            ResolutionLevelEnum resolutionLevel = (ResolutionLevelEnum)options.ResolutionLevel;

            if(options.Proxy!=null && options.ProxyWhilelist!=null)
                WebRequest.DefaultWebProxy = new WebProxy(new Uri(options.Proxy), true, options.ProxyWhilelist.Replace('"', ' ').Trim().Split(','));
            else if(options.Proxy!=null)
                WebRequest.DefaultWebProxy = new WebProxy(new Uri(options.Proxy), true);

            Builder = new ContainerBuilder();
            
            string[] dlls = null;
            if (string.IsNullOrEmpty(options.Source))
            {
                return options.GetUsage();
            }

            Trace.TraceInformation("searching for dlls in {0}", options.Source);
            dlls = Directory.GetFiles(options.Source, "*.dll", SearchOption.AllDirectories);
            Trace.TraceInformation("{0} dlls found in {1}", dlls.Length, options.Source);

            if (!Directory.Exists(@"cache"))
                Directory.CreateDirectory(@"cache");

            Builder.RegisterType<ConfigurationService>()
                   .WithParameter("path", options.Source)
                   .AsSelf()
                   .SingleInstance();
            Builder.RegisterType<VersionResolverService>()
                   .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(ConfigurationService)),
                                        (pi, c) => c.Resolve(typeof(ConfigurationService))))
                   .WithParameter("resolutionLevel", resolutionLevel)
                   .AsSelf()
                   .SingleInstance();
            Builder.RegisterType<AssemblyCacheService>()
                   .WithParameter("dlls", dlls)
                   .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(ConfigurationService)),
                                        (pi, c) => c.Resolve(typeof(ConfigurationService))))
                   .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(VersionResolverService)),
                                        (pi, c) => c.Resolve(typeof(VersionResolverService))))
                   .WithParameter("useCache", !options.NoCache)
                   .AsSelf()
                   .SingleInstance();
            Builder.RegisterType<MappingService>().AsSelf().SingleInstance()
                    .WithParameter("rootPath", options.Source)
                    .WithParameter("officialRepository", options.OfficialRepository);
            Builder.RegisterType<DependenciesResolverService>()
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(AssemblyCacheService)),
                                        (pi, c) => c.Resolve(typeof(AssemblyCacheService))))
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(MappingService)),
                                        (pi, c) => c.Resolve(typeof(MappingService))))
                    .WithParameter("useCache", !options.NoCache)
                    .WithParameter("resolutionLevel", resolutionLevel)
                    .AsSelf().SingleInstance();
            Builder.RegisterType<NuGetPackageCreationService>()    
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(AssemblyCacheService)),
                                        (pi, c) => c.Resolve(typeof(AssemblyCacheService))))
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(MappingService)),
                                        (pi, c) => c.Resolve(typeof(MappingService))))
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(ConfigurationService)),
                                        (pi, c) => c.Resolve(typeof(ConfigurationService))))
                    .WithParameter(new ResolvedParameter(
                                        (pi, c) => pi.ParameterType == (typeof(DependenciesResolverService)),
                                        (pi, c) => c.Resolve(typeof(DependenciesResolverService))))
                                        .WithParameter("useCache", !options.NoCache)
                                        .WithParameter("repository", options.Repository)
                                        .WithParameter("credential", options.RepositoryCredential)
                                        .WithParameter("owner", options.Owner)
                                        .WithParameter("author", options.Authors)
                                        .WithParameter("slackUrl", options.SlackUrl)
                                        .WithParameter("slackChannel", options.SlackChannel)
                                        .WithParameter("slackUsername", options.SlackUsername)
                                        .AsSelf()
                                        .SingleInstance();
            Container = Builder.Build();
            ConfigurableInjection.InitializeContainer(Container);

            using (var scope = Container.BeginLifetimeScope())
            {
                var packageCreationService = scope.Resolve<NuGetPackageCreationService>();
                Trace.TraceInformation("ul-nuget-converter successfully started.");
                if (options.StartDeamon)
                    StartDeamon(scope, options);
                else if (!string.IsNullOrEmpty(options.Filename))
                    CreatePackage(scope, options);
                else
                    packageCreationService.SyncAssembliesPackages();
            }

            return null;
        }

        public static void CreatePackage(ILifetimeScope scope, CommandLineOptions options)
        {
            var packageGroupService = scope.Resolve<NuGetPackageCreationService>();
            var _cacheService = scope.Resolve<AssemblyCacheService>();
            
            //Assembly Service
            var assemblyId = _cacheService.AddOrUpdateAssembly(options.Filename);
            if(assemblyId==null)
                return;

            //Group Service
            packageGroupService.RemovePackage(assemblyId);
            packageGroupService.CreatePackage(assemblyId, false);
        }


        public static void StartDeamon(ILifetimeScope scope, CommandLineOptions options)
        {
            bool exit = false;
            var watcher = new AssembliesDirectoryWatcher(scope, options);
            var packageCreationService = scope.Resolve<NuGetPackageCreationService>();
            packageCreationService.SyncAssembliesPackages();
            watcher.Monitor();
            while (exit==false)
            {
                var info = Console.ReadKey();
                if (info.KeyChar == 'c' && info.Modifiers == ConsoleModifiers.Control)
                    exit = true;
            }
        }

    }
}
