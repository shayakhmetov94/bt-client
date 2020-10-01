using bittorrent_service.Base.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace bittorrent_client_webgui.Server
{
    public class Startup
    {
        private static IBtServiceDataContext _dataContext = null;

        public static IConfiguration Configuration { get; private set; }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
            services.AddMvc((o) => o.EnableEndpointRouting = false).AddNewtonsoftJson();
            services.AddResponseCompression(opts => {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/octet-stream" }
                );
            });

            services.AddScoped<IBtServiceDataContext, SQLiteDataContext>((s) => new SQLiteDataContext(new SQLiteDbConnectionFactory(Configuration.GetConnectionString("Default"))));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            app.UseResponseCompression();

            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseBlazorDebugging();
            }

            app.UseClientSideBlazorFiles<Client.Program>();

            app.UseRouting();
            app.UseMvcWithDefaultRoute();

            app.UseEndpoints(endpoints => {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapFallbackToClientSideBlazor<Client.Program>("index.html");
            });

            app.UseStaticFiles();

            IConfigurationBuilder builder = new ConfigurationBuilder()
                                                .SetBasePath(env.ContentRootPath)
                                                .AddJsonFile("appconfig.json");
            Configuration = builder.Build();
        }
    }
}
