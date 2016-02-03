using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ullink.NugetConverter.services.configuration
{
    public class ConfigurationService
    {
        private readonly string _path;

        private readonly ConfigurationGetter _configurationGetter =
            new ConfigurationGetter(new ConfigurationFileReader());

        public ConfigurationService(string path)
        {
            _path = path;
        }

        public Configuration Get()
        {
            return _configurationGetter.Get(_path);
        }

        public Configuration Get(string path)
        {
            return _configurationGetter.Get(path);
        }
    }
}
