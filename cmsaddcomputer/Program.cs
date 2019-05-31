﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Configuration;

namespace cmsaddcomputer
{
    class Program
    {
        const string COMPUTERNAME_POST_KEY = "computername";
        const string MODEL_POST_KEY = "model";
        const string SERIAL_NUMBER_POST_KEY = "serialnumber";
        const string MANUFACTURER_POST_KEY = "manuf";
        const string YEAR_POST_KEY = "year";
        const string MONTH_POST_KEY = "month";
        const string IP_TYPE_POST_KEY = "iptype";
        const string IP_ADDRESS_POST_KEY = "ipaddress";
        const string MAC_ADDRESS_POST_KEY = "macaddress";
        const string CONNECTION_TYPE_POST_KEY = "contype";
        const string OS_VERSION_POST_KEY = "opsystem";

        static readonly string[,] OPERATING_SYSTEM_CONSTANTS_SEARCH = { { "2003", "WS03" }, { "2008", "WS08" }, { "2011", "WS08" },
            { "2012", "WS12" }, { "2016", "WS16" }, {"2019", "WS19" }, {"xp", "WXP" }, {"7", "W7" }, {"8.1", "W8" }, {"8", "W8" },
            {"10", "W10" } };
        
        const string CMS_SECRET_KEY_PROP_NAME = "secret_key";
        const string CSRF_CMS_RECEIVE_PROP_NAME = "csrf";
        const string CSRF_CMS_SEND_PROP_NAME = "csrfmiddlewaretoken";

        static readonly string[,] MANUFACTURER_CONSTANTS_SEARCH = { { "dell", "D" }, {"hp", "H" }, {"hewlett", "H"}, {"lenovo", "L"},
            {"microsoft", "M"}, {"apple", "G" }, {"asus", "A" }, {"sony", "S" }, {"acer", "C" } };

        static string convert_manufacturer(string manuf)
        {
            manuf = manuf.ToLower();
            for (int i = 0; i < MANUFACTURER_CONSTANTS_SEARCH.GetLength(0); ++i)
            {
                if (manuf.IndexOf(MANUFACTURER_CONSTANTS_SEARCH[i, 0]) != -1)
                    return MANUFACTURER_CONSTANTS_SEARCH[i, 1];
            }
            return "O";
        }


        static string get_os_version(string long_caption)
        {
            long_caption = long_caption.ToLower();
            for (int i = 0; i < OPERATING_SYSTEM_CONSTANTS_SEARCH.GetLength(0); ++i)
            {
                if (long_caption.IndexOf(OPERATING_SYSTEM_CONSTANTS_SEARCH[i, 0]) != -1)
                    return OPERATING_SYSTEM_CONSTANTS_SEARCH[i, 1];
            }
            return "WO";
        }


        static Dictionary<string, string> GetComputerInfo()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            result.Add(COMPUTERNAME_POST_KEY, Environment.MachineName);
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem");
            ManagementObjectCollection information = searcher.Get();
            foreach (ManagementObject obj in information)
            {
                try
                {
                    result.Add(MODEL_POST_KEY, obj["Model"].ToString().Trim());
                }
                catch { }
            }
            searcher = new ManagementObjectSearcher("SELECT SerialNumber, Manufacturer, ReleaseDate FROM Win32_BIOS");
            information = searcher.Get();
            foreach (ManagementObject obj in information)
            {
                try
                {
                    result.Add(SERIAL_NUMBER_POST_KEY, obj["SerialNumber"].ToString().Trim());
                }
                catch { }
                try
                {
                    result.Add(MANUFACTURER_POST_KEY, convert_manufacturer(obj["Manufacturer"].ToString().Trim()));
                }
                catch { }
                try
                {
                    result.Add(YEAR_POST_KEY, obj["ReleaseDate"].ToString().Trim().Substring(0, 4));
                }
                catch { }
                try
                {
                    result.Add(MONTH_POST_KEY, obj["ReleaseDate"].ToString().Trim().Substring(4, 2));
                }
                catch { }
            }

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    IPInterfaceProperties ipprop = nic.GetIPProperties();
                    if (ipprop.GatewayAddresses.Count > 0)
                    {
                        if (ipprop.DhcpServerAddresses.Count > 0)
                            try
                            {
                                result.Add(IP_TYPE_POST_KEY, "D");
                            }
                            catch { }
                        else
                        {
                            try
                            {
                                result.Add(IP_TYPE_POST_KEY, "S");
                            }
                            catch { }
                            foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    try
                                    {
                                        result.Add(IP_ADDRESS_POST_KEY, ip.Address.ToString());
                                    }
                                    catch { }
                                }
                            }
                        }
                        try
                        {
                            result.Add(MAC_ADDRESS_POST_KEY, nic.GetPhysicalAddress().ToString());
                        }
                        catch { }

                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            try
                            {
                                result.Add(CONNECTION_TYPE_POST_KEY, "W");
                            }
                            catch { }
                        else
                            try
                            {
                                result.Add(CONNECTION_TYPE_POST_KEY, "E");
                            }
                            catch { }

                    }
                }
            }

            searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            information = searcher.Get();
            foreach (ManagementObject obj in information)
            {
                try
                {
                    result.Add(OS_VERSION_POST_KEY, get_os_version(obj["Caption"].ToString().Trim()));
                }
                catch { }
            }

            return result;
        }

        static string SendGetRequest(Uri uri, Dictionary<string, string> cookie = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.ContentType = null;
            if (cookie != null)
            {
                request.CookieContainer = new CookieContainer();
                foreach (KeyValuePair<string, string> entry in cookie)
                {
                    request.CookieContainer.Add(new Cookie(entry.Key, entry.Value));
                }
            }
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseText;
            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }
            return responseText;
        }

        static string SendPOSTJsonRequest(Uri uri, Dictionary<string, string> data, Dictionary<string, string> cookie = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
            if (cookie != null)
            {
                request.CookieContainer = new CookieContainer();
                foreach (KeyValuePair<string, string> entry in cookie)
                {
                    request.CookieContainer.Add(new Cookie(entry.Key, entry.Value) { Domain = uri.Host });
                }
            }
            NameValueCollection outgoingQueryString = HttpUtility.ParseQueryString(String.Empty);
            if (data != null)
            {
                foreach (KeyValuePair<string, string> entry in data)
                {
                    outgoingQueryString.Add(entry.Key, entry.Value);
                }
            }
            string strData = outgoingQueryString.ToString();
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] byte1 = encoding.GetBytes(strData);
            request.ContentLength = byte1.Length;
            using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(strData);
                streamWriter.Flush();
                streamWriter.Close();
            }

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseText;
            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }
            return responseText;
        }


        static bool SendInfoToServerRequest(string server, int clientid, Dictionary<String, String> info)
        {
            DateTime requestDate = DateTime.UtcNow;
            string url = server;
            UriBuilder builder = new UriBuilder(server);
            builder.Path = "api/check/";
            builder.Query = CMS_SECRET_KEY_PROP_NAME + "=" + ConfigurationManager.AppSettings["api_secret_key"];
            Uri right_url = builder.Uri;
            string responseText = SendGetRequest(right_url);

            Dictionary<string, string> responseValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

            if (responseValues.ContainsKey(CSRF_CMS_RECEIVE_PROP_NAME))
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("api/client/").Append(clientid).Append("/computer");
                builder.Path = stringBuilder.ToString();
                builder.Query = "";
                Dictionary<string, string> cookies = new Dictionary<string, string>();
                if (!info.ContainsKey(CMS_SECRET_KEY_PROP_NAME))
                    info.Add(CMS_SECRET_KEY_PROP_NAME, ConfigurationManager.AppSettings["api_secret_key"]);
                if (!info.ContainsKey(CSRF_CMS_SEND_PROP_NAME))
                    info.Add(CSRF_CMS_SEND_PROP_NAME, responseValues[CSRF_CMS_RECEIVE_PROP_NAME]);
                cookies.Add(CSRF_CMS_SEND_PROP_NAME, responseValues[CSRF_CMS_RECEIVE_PROP_NAME]);
                responseText = SendPOSTJsonRequest(builder.Uri, info, cookies);
            }
            else
                return false;

            return true;
        }

        static void Main(string[] args)
        {
            SettingsKeeper settings = new SettingsKeeper();
            CMSConnector connector = new CMSConnector(settings.Api_key, settings.Server_name, settings.Port, settings.Https);
            int result = connector.SendComputerInfo(settings.Client_uuid, GetComputerInfo());
            if (result == -1)
                System.Console.WriteLine("The computer was not added");
            else
                System.Console.WriteLine("Computer ID: {0}", result);
            System.Console.ReadLine();
        }
    }
}
