using System.Threading.Tasks;
using FunMSNP.MSNP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

namespace FunMSNP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {       
            var host = new HostBuilder()
                .ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
                {
                    configurationBuilder.AddEnvironmentVariables(prefix: "MSNP_");

                    configurationBuilder.AddJsonFile("appsettings.json", optional: true);
                    configurationBuilder.AddJsonFile($"appsettings.{hostBuilderContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureLogging((hostBuilderContext, builder) =>
                {
                    var config = hostBuilderContext.Configuration.GetSection("Logging");
                    builder.AddConfiguration(config);
                    builder.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    });
                    builder.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var cfg = hostContext.Configuration;
                    var conn = cfg.GetConnectionString("Database");

                    services.AddDbContext<MSNPContext>(o =>
                        o.UseMySql(conn, ServerVersion.AutoDetect(conn)),
                        ServiceLifetime.Scoped, ServiceLifetime.Scoped);

                    services.AddScoped<IHostedService, MSNPService>();
                    services.AddTransient<UserManager>();
                });

            await host.RunConsoleAsync();
        }

    }
}

