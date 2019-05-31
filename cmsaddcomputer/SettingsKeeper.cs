using System.Configuration;
using System.IO;
using System;

namespace cmsaddcomputer
{
    class SettingsKeeper
    {
        private const string DEFAULT_SETTINGS_FILE_NAME = "api.config";

        private const string DEFAULT_API_KEY = "";
        private const string DEFAULT_SERVER_NAME = "127.0.0.1";
        private const string DEFAULT_CLIENT_UUID = "";
        private const int DEFAULT_SERVER_PORT = 443;
        private const bool DEFAULT_USE_HTTPS = true;

        private string _api_key = DEFAULT_API_KEY;
        private string _server_name = DEFAULT_SERVER_NAME;
        private string _client_uuid = DEFAULT_CLIENT_UUID;
        private int _port_number = DEFAULT_SERVER_PORT;
        private bool _use_https = DEFAULT_USE_HTTPS;
        private string _settings_file_name = null;

        public SettingsKeeper(string settings_file_name = DEFAULT_SETTINGS_FILE_NAME)
        {
            if (File.Exists(settings_file_name))
            {
                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = settings_file_name;
                _settings_file_name = settings_file_name;
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
                foreach (string keyname in config.AppSettings.Settings.AllKeys)
                {
                    switch (keyname)
                    {
                        case "api_secret_key":
                            _api_key = !String.IsNullOrEmpty(config.AppSettings.Settings[keyname].Value) ? config.AppSettings.Settings[keyname].Value : DEFAULT_API_KEY;
                            break;
                        case "server_name":
                            _server_name = !String.IsNullOrEmpty(config.AppSettings.Settings[keyname].Value) ? config.AppSettings.Settings[keyname].Value : DEFAULT_SERVER_NAME;
                            break;
                        case "client_uuid":
                            _client_uuid = !String.IsNullOrEmpty(config.AppSettings.Settings[keyname].Value) ? config.AppSettings.Settings[keyname].Value : DEFAULT_CLIENT_UUID;
                            break;
                        case "server_port":
                            if (String.IsNullOrEmpty(config.AppSettings.Settings[keyname].Value) || !int.TryParse(config.AppSettings.Settings[keyname].Value, out _port_number))
                                _port_number = DEFAULT_SERVER_PORT;
                            break;
                        case "https":
                            _use_https = !String.IsNullOrEmpty(config.AppSettings.Settings[keyname].Value) ? config.AppSettings.Settings[keyname].Value == "1" : DEFAULT_USE_HTTPS;
                            break;
                    }
                }
            }
        }

        public string Filename
        {
            get
            {
                return _settings_file_name;
            }
        }

        public string Api_key
        {
            get
            {
                return _api_key;
            }
        }

        public string Server_name
        {
            get
            {
                return _server_name;
            }
        }

        public string Client_uuid
        {
            get
            {
                return _client_uuid;
            }
        }

        public int Port
        {
            get
            {
                return _port_number;
            }
        }

        public bool Https
        {
            get
            {
                return _use_https;
            }
        }
    }
}
