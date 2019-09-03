using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ngdb.Models;

namespace ngdb
{
    public class NgDbStartup
    {
        public NgDbStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<NgDbServer>();

            services.AddSingleton<NgDbHttpHandler>();
            services.AddSingleton<StoreService>();

            services.Configure<NgDbConfig>(Configuration);

        }

        public void Configure(IApplicationBuilder app)
        {
            // This is like the UseMvc
            // It will process specific requests
            app.UseMiddleware<NgDbHttpHandler>();
        }
    }
}
