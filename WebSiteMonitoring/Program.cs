using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

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
            .UseSystemd()
            .ConfigureServices((hostContext, services) =>
            {
                IConfiguration configuration = hostContext.Configuration;

                EmailSettings emailSettings = configuration.GetSection("Email").Get<EmailSettings>();

                services.AddSingleton(emailSettings);
                services.AddHostedService<Monitor>();
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddFile("Logs/OinpWebMonitor-{Date}.txt");
            });
    }
}
