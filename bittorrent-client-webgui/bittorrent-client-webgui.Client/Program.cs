using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Blazor.Hosting;

namespace bittorrent_client_webgui.Client
{
    public class Program
    {
        public static void Main(string[] args) {
            CreateHostBuilder(args).RunAsync();
        }

        public static WebAssemblyHost CreateHostBuilder(string[] args) {
            var hostBuilder = WebAssemblyHostBuilder.CreateDefault(args);

            hostBuilder.RootComponents.Add<App>("app");

            hostBuilder.Services
                .AddBlazorise(options => {
                    options.ChangeTextOnKeyPress = true;
                })
                .AddBootstrapProviders()
                .AddFontAwesomeIcons();

            return hostBuilder.Build();
        }
    }
}
