using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Autofac;
using Ullink.NugetConverter.services;
using Ullink.NugetConverter.services.assemblyCacheService;
using Ullink.NugetConverter.utils;

namespace Ullink.NugetConverter
{
    /// <summary>
    /// Detect if a new Assembly was added to a specific folder if it's the case
    /// - If assembly was already a NuGet Package => Update it
    /// - If not Create a new one
    /// -- remove from Repository Old One
    /// -- Resolve Dependancies
    /// TODO : QUEUE Request to avoid issue on Multi-threading
    /// </summary>
    internal class AssembliesDirectoryWatcher : IDisposable
    {
        private readonly CommandLineOptions _options;
        private readonly AssemblyCacheService _cacheService;
        private readonly NuGetPackageCreationService _packageGroupService;
        private readonly FileSystemWatcher watcher = new FileSystemWatcher();
        private readonly ObservableCollection<FileSystemEventArgs> queue = new ObservableCollection<FileSystemEventArgs>();
        private IDisposable _reactiveSubcription;

        internal AssembliesDirectoryWatcher(ILifetimeScope lifetimeScope, CommandLineOptions options)
        {
            _options = options;
            watcher.IncludeSubdirectories = true;
            watcher.Filter = "*.dll";
            watcher.Path = options.Source;
            watcher.NotifyFilter = 
                NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size;
            watcher.Changed += watcher_Changed;
            watcher.Created += Watcher_Created;
            watcher.Deleted += Watcher_Deleted;
            watcher.Renamed += Watcher_Renamed;
            watcher.EnableRaisingEvents = true;
            Trace.TraceInformation("Start watching changes on {0}", options.Source);

            using (var scope = Program.Container.BeginLifetimeScope())
            {
                _cacheService = scope.Resolve<AssemblyCacheService>();
                _packageGroupService = scope.Resolve<NuGetPackageCreationService>();
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            queue.Enqueue(e);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            queue.Enqueue(e);
        }

        private void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            queue.Enqueue(e);
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            var delete = new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath), Path.GetFileName(e.OldName));
            var create = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.Name));
            queue.Enqueue(delete);
            queue.Enqueue(create);   
        }

        public void Monitor()
        {
            var backup= queue.ToList();
            queue.Clear();
            queue.CollectionChanged += queue_CollectionChanged;
            foreach (var fileSystemEventArgse in backup)
            {
                queue.Enqueue(fileSystemEventArgse);     
            }
        }

        void queue_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //BOOOOOO
            var fileChange = e.NewItems != null && e.NewItems.Count > 0
                                 ? (FileSystemEventArgs)e.NewItems[0]
                                 : (FileSystemEventArgs)e.OldItems[0];

            if (!fileChange.FullPath.EndsWith(".dll"))
                return;

            try
            {
                switch (fileChange.ChangeType)
                {
                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Changed:
                    case WatcherChangeTypes.Created:


                        Trace.TraceInformation($"File {fileChange.FullPath} has changed");
                        //Wait for the pdb to be fully copied. as Cecil load it we might encountered the following corner case
                        // .dll triggered the watcher change but .pdb is not yep completely coped
                        // it raised an access denied an other process is already using the file
                        Thread.Sleep(5000);
                        //Assembly Service
                        var assemblyId = _cacheService.AddOrUpdateAssembly(fileChange.FullPath);

                        if (assemblyId == null)
                            return;

                        //Group Service
                        _packageGroupService.RemovePackage(assemblyId);
                        _packageGroupService.CreatePackage(assemblyId, true);
                        break;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError("Unable to create {0} for {1}", exception.Message, fileChange);
            }
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    watcher.Dispose();   
                    _reactiveSubcription.Dispose();
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
