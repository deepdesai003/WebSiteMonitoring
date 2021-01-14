using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebSiteMonitoring
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((hostContext, services) =>
            {
                IConfiguration configuration = hostContext.Configuration;

                EmailSettings emailSettings = configuration.GetSection("Email").Get<EmailSettings>();

                services.AddSingleton(emailSettings);
                services.AddHostedService<Monitor>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.AddEventLog(new EventLogSettings()
                {
                    SourceName = "Oinp Monitor Service"
                });
            });
    }
}
