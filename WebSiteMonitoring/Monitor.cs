using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Vision.v1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSiteMonitoring
{
    public class Monitor : BackgroundService
    {
        private readonly ILogger<Monitor> _logger;

        private readonly EmailSettings _emailSettings;

        private DateTime _OldDate = new DateTime();

        public Monitor(ILogger<Monitor> logger, EmailSettings emailSettings)
        {
            _logger = logger;
            _emailSettings = emailSettings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                _logger.LogInformation("Updated started at: {time}", DateTimeOffset.Now);
                DateTime updatedDate = GetUpdated();
                _logger.LogInformation("Updated ended at: {time}", DateTimeOffset.Now);
                if (updatedDate != default)
                {
                    if (updatedDate.CompareTo(_OldDate) != 0)
                    {
                        _logger.LogWarning("Page Updated : {time}", updatedDate.ToString());
                        SendEmail(updatedDate);
                        _OldDate = updatedDate;
                    }
                }
                TimeSpan Delay = new TimeSpan(0, 0, 30);
                await Task.Delay(delay: Delay, stoppingToken);
            }
        }


        private DateTime GetUpdated()
        {
            string requestUrl = "https://api.ontario.ca/api/drupal/page%2F2021-ontario-immigrant-nominee-program-updates";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);
            request.ContentType = "application/json; charset=utf-8";
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            StreamReader readStream = null;
            HttpWebResponse response = new HttpWebResponse();
            DateTime updatedDate = new DateTime();

            try
            {
                response = (HttpWebResponse)request.GetResponse();
                Stream receiveStream = response.GetResponseStream();

                readStream = new StreamReader(receiveStream, Encoding.UTF8);

                JObject jObject = (JObject)JsonConvert.DeserializeObject<object>(readStream.ReadToEnd());
                var dateValue = jObject.SelectTokens("$..og:updated_time.#attached.drupal_add_html_head..#value").FirstOrDefault();
                updatedDate = dateValue.ToObject<DateTime>();


                Console.WriteLine(readStream.ReadToEnd());
            }
            catch (Exception ex)
            {
                _logger.LogError("Error API Call:" + ex.Message);
            }
            finally
            {
                response.Close();
                readStream.Close();
            }

            return updatedDate;
        }

        private void SendEmail(DateTime updatedDate)
        {
            SmtpClient client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_emailSettings.senderEmail, _emailSettings.senderPassword),
                EnableSsl = true
            };

            MailMessage mailMessage = new MailMessage();
            MailAddress mailAddress = new MailAddress(_emailSettings.senderEmail);

            StringBuilder body = new StringBuilder();
            body.AppendLine("Check website: https://www.ontario.ca/page/2021-ontario-immigrant-nominee-program-updates");
            body.AppendLine("Updated at : " + updatedDate.ToLongDateString() + " " + updatedDate.ToLongTimeString());
            body.AppendLine("Sent from " + Environment.MachineName);

            mailMessage.Subject = "Web Page Updated";
            mailMessage.Body = body.ToString();
            mailMessage.From = mailAddress;
            _emailSettings.receivers.ForEach(receiver => mailMessage.Bcc.Add(receiver));

            client.Send(mailMessage);
        }

        private void SendEmailWithGmailAPI()
        {
            string[] Scopes = { GmailService.Scope.GmailSend };
            string ApplicationName = "Web Monitor .NET";
            string serviceAccountEmail = "web-monitor-api-net@email-client-301419.iam.gserviceaccount.com";
            UserCredential credential;
            try
            {

                using (var stream =
                    new FileStream("./credentials.json", FileMode.Open, FileAccess.Read))
                {
                    // The file token.json stores the user's access and refresh tokens, and is created
                    // automatically when the authorization flow completes for the first time.
                    string credPath = "token.json";
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true))
                        .Result;
                    Console.WriteLine("Credential file saved to: " + credPath);
                }


                // Create Gmail API service.
                var service = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                /*
                var gservice = new VisionService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = gcredential,
                    ApplicationName = ApplicationName,
                });
                */
                Message emailContent = CreateMessage();
                service.Users.Messages.Send(emailContent, "me").Execute();
                _logger.LogInformation("Email Sent");

            }
            catch (Exception ex)
            {
                _logger.LogError("Error as GMAIL API :" + ex.Message);
            }
        }

        private static string Base64UrlEncode(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);

            return System.Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        private Message CreateMessage()
        {
            Message message = new Message();
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                AE.Net.Mail.MailMessage mailMessage = new AE.Net.Mail.MailMessage
                {
                    Subject = "Web Page Updated",
                    Body = "Check website: https://www.ontario.ca/page/2021-ontario-immigrant-nominee-program-updates",
                    From = new MailAddress("deepdesai003@gmail.com"),
                };

                mailMessage.To.Add(new MailAddress("deepdesai003@gmail.com"));
                mailMessage.ReplyTo.Add(mailMessage.From); // Bounces without this!!
                StringWriter msgStr = new StringWriter();
                msgStr.Write(mailMessage);
                mailMessage.Save(msgStr);
                // Special "url-safe" base64 encode.
                var raw = Base64UrlEncode(msgStr.ToString());
                message = new Message { Raw = raw };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return message;
        }

    }
}