using Google.Apis.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WebSiteMonitoring
{
    public class WebsiteCheck
    {
        private string _requestUrl = "https://api.ontario.ca/api/drupal/page%2F2021-ontario-immigrant-nominee-program-updates";

        private readonly ILogger<Monitor> _logger;

        public readonly DateTime updatedDate;

        public string EmailBody = string.Empty;

        public readonly string updateTimeString;
        
        public DateTime UpdatedDate => updatedDate;


        public WebsiteCheck(ILogger<Monitor> logger, string requestUrl)
        {
            _logger = logger;
            _requestUrl = requestUrl;
            updatedDate = GetUpdated();
            updateTimeString = updatedDate.ToLongDateString() + " " + updatedDate.ToLongTimeString();
        }

        private DateTime GetUpdated()
        {

            //= "https://api.ontario.ca/api/drupal/page%2F2021-ontario-immigrant-nominee-program-updates";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_requestUrl);
            request.ContentType = "application/json; charset=utf-8";
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            StreamReader readStream = null;
            HttpWebResponse httpWebResponse = new HttpWebResponse();
            DateTime updatedDate = new DateTime();

            try
            {
                httpWebResponse = (HttpWebResponse)request.GetResponse();
                Stream receiveStream = httpWebResponse.GetResponseStream();

                readStream = new StreamReader(receiveStream, Encoding.UTF8);

                string response = readStream.ReadToEnd();
                string regexDate = string.Empty;

                JObject jObject = null;
                //var regexDate;
                if ((response.StartsWith("{") && response.EndsWith("}")) || //For object
                    (response.StartsWith("[") && response.EndsWith("]"))) //For array
                {
                    jObject = (JObject)JsonConvert.DeserializeObject<object>(response);
                    var dateValue = jObject?.SelectTokens("$..og:updated_time.#attached.drupal_add_html_head..#value").FirstOrDefault();
                    updatedDate = dateValue.ToObject<DateTime>();
                    EmailBody = "Check website: https://www.ontario.ca/page/2021-ontario-immigrant-nominee-program-updates";
                }
                else
                {
                   regexDate = Regex.Match(
                       Regex.Match(response, @"<\s*time property=""dateModified""[^>]*>(.*?)<\s*/\s*time>").Value,
                       @"(\d{1,4}([.\-/])\d{1,2}([.\-/])\d{1,4})").Value;
                    DateTime.TryParse(regexDate, out updatedDate);
                    EmailBody = "Check website: " + _requestUrl;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("Error API Call:" + ex.Message);
            }
            finally
            {
                httpWebResponse.Close();
                readStream.Close();
            }

            return updatedDate;
        }
    }
}
