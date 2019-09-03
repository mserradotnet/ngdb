using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ngdb
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var httpPort = args?.FirstOrDefault(a => Regex.IsMatch(a, "^--httpPort=[0-9]{3,5}$"))?.Substring(11);
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureAppConfiguration((context, builder) =>
                    {
                        builder.AddJsonFile("ngdb.config.json", optional: false);
                        builder.AddCommandLine(args);
                    });
                    webBuilder.UseStartup<NgDbStartup>();
                    webBuilder.UseUrls($"http://*:{httpPort}");
                });
        }
    }
}
