using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace NugetConverterService
{
    public partial class NugetConverterService : ServiceBase
    {
        private readonly EventLog _eventLog;
        private  Func<string> runService;
        private ServicePInvoke.ServiceStatus _serviceStatus;

        public NugetConverterService(string[] args)
        {
            InitializeComponent();
            _eventLog = new EventLog();
            if (!EventLog.SourceExists("NugetConverterService"))
            {
                EventLog.CreateEventSource("NugetConverterService", "Application");
            }
            _eventLog.Source = "NugetConverterService";
            _eventLog.Log = "Application";

            runService = () =>
            {
                Trace.TraceInformation("Nuget Converter Starting...");
                _eventLog.WriteEntry("Nuget Converter Starting...");
                var error = Ullink.NugetConverter.Program.Run(args);
                if (error != null)
                    Trace.TraceError(error);
                return error;

            };
            runService.BeginInvoke(Callback, null);
        }

        private void Callback(IAsyncResult ar)
        {
            try
            {
                var error = runService.EndInvoke(ar);
                if (error != null)
                {
                    _eventLog.WriteEntry(
                        $"Nuget Converter Stopped due to error. Check logs in {Path.GetDirectoryName(typeof(NugetConverterService).Assembly.Location)}.\nError: {error}", EventLogEntryType.Error);
                }
            }
            catch (Exception exc)
            {
                _eventLog.WriteEntry($"Nuget Converter Stopped due to error. Check logs in {Path.GetDirectoryName(typeof(NugetConverterService).Assembly.Location)}.\nError: {exc.Message}", EventLogEntryType.Error);
                Trace.TraceError(exc.Message);
                Trace.TraceError(exc.StackTrace);
            }
            
            this.Stop();
        }

        protected override void OnStop()
        {
        }
    }
}
