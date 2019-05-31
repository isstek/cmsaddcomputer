using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace cmsaddcomputer
{
    class CMSConnector
    {
        private const string CMS_SECRET_KEY_PROP_NAME = "secret_key";
        private const string CSRF_CMS_SEND_PROP_NAME = "csrfmiddlewaretoken";

        private const string CSRF_CMS_RECEIVE_PROP_NAME = "csrf";
        private const string COMPUTER_ID_CMS_RECEIVE_PROP_NAME = "computerid";

        private static readonly string[,] OPERATING_SYSTEM_CONSTANTS_SEARCH = { { "2003", "WS03" }, { "2008", "WS08" }, { "2011", "WS08" },
            { "2012", "WS12" }, { "2016", "WS16" }, {"2019", "WS19" }, {"xp", "WXP" }, {"7", "W7" }, {"8.1", "W8" }, {"8", "W8" },
            {"10", "W10" } };

        private static readonly string[,] MANUFACTURER_CONSTANTS_SEARCH = { { "dell", "D" }, {"hp", "H" }, {"hewlett", "H"}, {"lenovo", "L"},
            {"microsoft", "M"}, {"apple", "G" }, {"asus", "A" }, {"sony", "S" }, {"acer", "C" } };

        string _server_name = "";
        int _server_port = 443;
        string _api_key = "";
        string _csrf_token = "";
        bool _use_https = true;
        UriBuilder uri_base = null;
        CookieContainer _cookieContainer = null;


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

        public CMSConnector(string api_key, string servername, int serverport = 443, bool https=true)
        {
            _api_key = api_key;
            _server_name = servername;
            _server_port = serverport;
            _use_https = https;
            _cookieContainer = new CookieContainer();
            uri_base = new UriBuilder(_server_name);
            uri_base.Port = _server_port;
            uri_base.Scheme = _use_https ? "https" : "http";
        }

        private bool GetCSRFToken(Dictionary<string, string> responseValues)
        {
            if (responseValues.ContainsKey(CSRF_CMS_RECEIVE_PROP_NAME))
            {
                _csrf_token = responseValues[CSRF_CMS_RECEIVE_PROP_NAME];
                return true;
            }
            return false;
        }

        private bool InitConnection()
        {
            uri_base.Path = "api/check/";
            uri_base.Query = CMS_SECRET_KEY_PROP_NAME + "=" + _api_key;
            Uri uri = uri_base.Uri;
            uri_base.Path = "";
            uri_base.Query = "";
            HttpStatusCode response_code;
            string response = SendGetRequest(uri, out response_code);
            if (response_code != HttpStatusCode.OK)
            {
                Console.WriteLine(response);
                return false;
            }
            if (response is null)
            {
                return false;
            }
            Dictionary<string, string> responseValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);

            return GetCSRFToken(responseValues);
        }

        public int SendComputerInfo(string clientuuid, Dictionary<string, string> info)
        {
            if (!InitConnection())
                return -1;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("api/client/").Append(clientuuid).Append("/computer");
            uri_base.Path = stringBuilder.ToString();
            uri_base.Query = "";
            Uri uri = uri_base.Uri;
            uri_base.Path = "";
            Dictionary<string, string> data = new Dictionary<string, string>(info);
            data.Add(CSRF_CMS_SEND_PROP_NAME, _csrf_token);
            data.Add(CMS_SECRET_KEY_PROP_NAME, _api_key);
            HttpStatusCode response_code;
            string response = SendPOSTJsonRequest(uri, data, out response_code, true);
            if (response_code != HttpStatusCode.OK)
            {
                Console.WriteLine(response);
                return -1;
            }
            if (response is null)
            {
                return -1;
            }
            Dictionary<string, string> responseValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
            int result = -1;
            if (GetCSRFToken(responseValues) && responseValues.ContainsKey(COMPUTER_ID_CMS_RECEIVE_PROP_NAME) && int.TryParse(responseValues[COMPUTER_ID_CMS_RECEIVE_PROP_NAME], out result))
                return result;

            return -1;
        }

        private bool checkResponseCode(HttpStatusCode code, out string text)
        {
            switch (code)
            {
                case HttpStatusCode.OK:
                    text = "OK";
                    return true;
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.InternalServerError:
                    text = new StringBuilder("Something wrong with the server. HTTP Code: ").Append((int)code).ToString();
                    return false;
                case HttpStatusCode.PaymentRequired:
                    text = "Bad request: API Key needed";
                    return false;
                case HttpStatusCode.Unauthorized:
                    text = "Bad request: API Key is not valid";
                    return false;
                case HttpStatusCode.Forbidden:
                    text = "Bad request: CSRF token is not provided or invalid";
                    return false;
                case HttpStatusCode.BadRequest:
                    text = "Bad request: Some information in the body of the request is not right";
                    return false;
                case HttpStatusCode.Unused:
                    text = "Something is wrong with the server. Server is not reponding or is turned off.";
                    return false;
                default:
                    text = new StringBuilder("Unknown response code. HTTP Code: ").Append((int)code).ToString();
                    return false;
            }
        }

        private string SendGetRequest(Uri uri, out HttpStatusCode responseStatusCode, bool save_cookies=true)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.ContentType = null;
            request.CookieContainer = _cookieContainer;
            HttpWebResponse response = null;
            HttpStatusCode statusCode;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                statusCode = response.StatusCode;
            }
            catch (WebException we)
            {
                if (we.Response is null)
                    statusCode = HttpStatusCode.Unused;
                else
                    statusCode = ((HttpWebResponse)we.Response).StatusCode;
            }
            responseStatusCode = statusCode;
            string response_code_text;
            if (!checkResponseCode(statusCode, out response_code_text))
            {
                return response_code_text;
            }
            if (save_cookies)
                _cookieContainer.Add(response.Cookies);
            string responseText;
            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }
            return responseText;
        }

        private string SendPOSTJsonRequest(Uri uri, Dictionary<string, string> data, out HttpStatusCode responseStatusCode, bool save_cookies=true)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
            request.CookieContainer = _cookieContainer;
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

            HttpWebResponse response = null;
            HttpStatusCode statusCode;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                statusCode = response.StatusCode;
            }
            catch (WebException we)
            {
                if (we.Response is null)
                    statusCode = HttpStatusCode.Unused;
                else
                    statusCode = ((HttpWebResponse)we.Response).StatusCode;
            }
            responseStatusCode = statusCode;
            string response_code_text;
            if (!checkResponseCode(statusCode, out response_code_text))
            {
                return response_code_text;
            }
            if (save_cookies)
                _cookieContainer.Add(response.Cookies);
            string responseText;
            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }
            return responseText;
        }

    }
}
