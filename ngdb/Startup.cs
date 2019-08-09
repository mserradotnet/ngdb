using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ngdb
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<NgDbServer>();
            services.AddSingleton<NgDbHttpHandler>();
            services.AddSingleton<StoreService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<NgDbHttpHandler>();
        }
    }
}
