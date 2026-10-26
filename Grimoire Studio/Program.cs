using Grimoire_Studio;
using Grimoire_Studio.Generator;
using Grimoire_Studio.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<LocalStore>();
builder.Services.AddScoped<ChangeSets>();
builder.Services.AddScoped<Proposals>();
builder.Services.AddScoped<TeamText>();
builder.Services.AddScoped<GameDataClient>();
builder.Services.AddScoped<TokenRenderer>();

await builder.Build().RunAsync();
