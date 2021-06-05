using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
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

        private DateTime _OldOinpDate = new DateTime();

        private DateTime _OldCICDate = new DateTime();

        public Monitor(ILogger<Monitor> logger, EmailSettings emailSettings)
        {
            _logger = logger;
            _emailSettings = emailSettings;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await SendEmail(null);

            await base.StartAsync(cancellationToken);
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    _logger.LogInformation("Updated started at: {time}", DateTimeOffset.Now);
                    WebsiteCheck websiteCheck = new WebsiteCheck(_logger, "https://api.ontario.ca/api/drupal/page%2F2021-ontario-immigrant-nominee-program-updates");
                    _logger.LogInformation("Updated ended at: {time}", DateTimeOffset.Now);
                    if ((_OldOinpDate != default) && (websiteCheck.UpdatedDate.CompareTo(_OldOinpDate) != 0))
                    {
                        _logger.LogWarning("Page Updated : {time}", websiteCheck.updateTimeString);
                        SendEmail(websiteCheck);
                    }
                    _OldOinpDate = websiteCheck.UpdatedDate;

                    websiteCheck = new WebsiteCheck(_logger, "https://www.canada.ca/en/immigration-refugees-citizenship/services/immigrate-canada/express-entry/submit-profile/rounds-invitations.html");

                    if ((_OldCICDate != default) && (websiteCheck.UpdatedDate.CompareTo(_OldCICDate) != 0))
                    {
                        _logger.LogWarning("Page Updated : {time}", websiteCheck.updateTimeString);
                        SendEmail(websiteCheck);
                    }

                    _OldCICDate = websiteCheck.UpdatedDate;

                    TimeSpan Delay = new TimeSpan(0, 0, 30);
                    await Task.Delay(delay: Delay, stoppingToken);
                }
                catch(Exception ex)
                {
                    _logger.LogError("Error Service:" + ex);
                }
            }
        }

        /*
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
        */

        private async Task SendEmailWithSendGridAsync(WebsiteCheck websiteCheck)
        {
            string apiKey = _emailSettings.SendGrid;
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(_emailSettings.senderEmail, "Updates");
            var subject = "Web Page Updated";
            var to = new EmailAddress(_emailSettings.senderEmail, "Reciever");
            var plainTextContent = GetEmailBody(websiteCheck);
            var htmlContent = "<strong>and easy to do anywhere, even with C#</strong>";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await client.SendEmailAsync(msg);

        }

        private string GetEmailBody(WebsiteCheck websiteCheck)
        {
            StringBuilder body = new StringBuilder();

            if (websiteCheck == null)
            {
                body.AppendLine("Service started at: " + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
                body.AppendLine();
                body.AppendLine("Rceipents are: ");
                body.AppendJoin(Environment.NewLine, _emailSettings.receivers
                    .Select((receiver, index) => "\t" + (index + 1).ToString() + ". " + receiver));

                body.AppendLine();
                body.AppendLine(Environment.NewLine + "Sent from " + Environment.MachineName);
            }
            else
            {
                body.AppendLine(websiteCheck.EmailBody);
                body.AppendLine("Updated at : " + websiteCheck.updateTimeString);
                body.AppendLine("Sent from " + Environment.MachineName);
            }
            return body.ToString();
        }

        private void SendEmail(WebsiteCheck websiteCheck)
        {
            //if(websiteCheck == null || websiteCheck.UpdatedDate.Equals(default))
            //{
            //    return;
            //}

            SmtpClient client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_emailSettings.senderEmail, _emailSettings.senderPassword),
                EnableSsl = true
            };

            MailMessage mailMessage = new MailMessage();
            MailAddress mailAddress = new MailAddress(_emailSettings.senderEmail);

            //StringBuilder body = new StringBuilder();

            mailMessage.Subject = "Web Page Updated";
            mailMessage.From = mailAddress;

            if (websiteCheck == null || websiteCheck.UpdatedDate.Equals(default))
            {
                mailMessage.Body = GetEmailBody(websiteCheck);
                mailMessage.To.Add(_emailSettings.receivers.First());
            }
            else
            {
                mailMessage.Body = GetEmailBody(websiteCheck);
                _emailSettings.receivers.ForEach(receiver => mailMessage.Bcc.Add(receiver));
            }

            client.Send(mailMessage);
        }

    }
}
