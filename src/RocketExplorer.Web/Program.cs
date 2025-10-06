using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nethereum.Web3;
using RocketExplorer.Shared;
using RocketExplorer.Web;
using RocketExplorer.Web.Theming;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();

static void ConfigureServices(IServiceCollection services, string baseAddress)
{
	services.AddMudServices();

	services.AddSingleton<ThemeService>();

	services.AddScoped(_ => new HttpClient
	{
		BaseAddress = new Uri(baseAddress),
	});

	services.AddScoped<Configuration>();

	services.AddSingleton<AppState>();

	services.AddScoped<Web3>(provider =>
	{
		Configuration configuration = provider.GetRequiredService<Configuration>();
		return new Web3(configuration.EthereumRPCEndpoint);
	});

	MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
		.WithResolver(CompositeResolver.Create(BigIntegerResolver.Instance, StandardResolver.Instance));
}