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

builder.Services.AddMudServices();

builder.Services.AddSingleton<ThemeService>();

builder.Services.AddScoped(_ => new HttpClient
{
	BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<Configuration>();

builder.Services.AddSingleton<AppState>();

builder.Services.AddScoped<Web3>(provider =>
{
	Configuration configuration = provider.GetRequiredService<Configuration>();
	return new Web3(configuration.EthereumRPCEndpoint);
});

MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
	.WithResolver(CompositeResolver.Create(BigIntegerResolver.Instance, StandardResolver.Instance));

await builder.Build().RunAsync();