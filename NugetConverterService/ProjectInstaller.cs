using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NugetConverterService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            var commandLine = Path.Combine(Path.GetDirectoryName(typeof (ProjectInstaller).Assembly.Location), "CommandLine.config");
            if(!File.Exists(commandLine))
                throw new FileNotFoundException($"the command line argument for the service must be specified in {commandLine}");

            var arguments = File.ReadLines(commandLine);
            Context.Parameters["assemblypath"] = "\"" + Context.Parameters["assemblypath"] + "\" --deamon " +
                                                        arguments.Aggregate("",(source, next) => source + " " + next);
                                                                                             
            base.OnBeforeInstall(savedState);
        }
    }
}
