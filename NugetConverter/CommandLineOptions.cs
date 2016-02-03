using CommandLine;
using CommandLine.Text;

namespace Ullink.NugetConverter
{
    public class CommandLineOptions
    {
        [Option('d', "deamon", HelpText = "Start the converter as deamon to generate")]
        public bool StartDeamon { get; set; }

        [Option('f', "filename", Required = false,
          HelpText = "Filename of an assembly to re-generate")]
        public string Filename { get; set; }

        [Option('s', "source", Required = true,
          HelpText = "folder location to generate package from")]
        public string Source { get; set; }

        [Option('r', "repository", Required = true,
          HelpText = "repository to push to. can be remote or local repository")]
        public string Repository { get; set; }

        [Option('v', "credential", Required = false,
          HelpText = "User and password for repository in form of user:password")]
        public string RepositoryCredential { get; set; }

        [Option('p', "official-repository", Required = false,
          HelpText = "repository to retrieve package from. can be remote or local repository")]
        public string OfficialRepository { get; set; }

        [Option('n', "no-cache", Required = false,
          HelpText = "don't use cache, will slow down the processing")]
        public bool NoCache { get; set; }

        [Option('l', "resolution-level", Required = false,
          HelpText = "Define the level to resolve assembly if match is not exact")]
        public int ResolutionLevel { get; set; }

        [Option('a', "author", Required = true,
          HelpText = "Author used for the package")]
        public string Authors { get; set; }

        [Option('o', "owner", Required = true,
          HelpText = "Owner use for the package")]
        public string Owner { get; set; }

        [Option('b', "slack-username", Required = false,
          HelpText = "name to use to slack")]
        public string SlackUsername { get; set; }

        [Option('c', "slack-channel", Required = false,
          HelpText = "channel to use")]
        public string SlackChannel { get; set; }

        [Option('u', "slack-url", Required = false,
          HelpText = "Slack api url for hook")]
        public string SlackUrl { get; set; }

        [Option('p', "proxy-url", Required = false,
          HelpText = "proxy to use")]
        public string Proxy { get; set; }

        [Option('l', "proxy-white-list", Required = false,
          HelpText = "proxy list separated by ','")]
        public string ProxyWhilelist { get; set; }


        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
