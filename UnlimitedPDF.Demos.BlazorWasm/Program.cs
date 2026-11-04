using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UnlimitedPDF.Demos.BlazorWasm;
using UnlimitedPDF.Demos.BlazorWasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The client only reaches the app's own origin (built-in images); nothing is sent anywhere else.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<BrowserInterop>();
builder.Services.AddScoped<SampleImageStore>();

await builder.Build().RunAsync();
