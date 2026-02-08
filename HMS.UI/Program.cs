using HMS.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7280/";

// Services
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("HmsApi", client => client.BaseAddress = new Uri(apiBase));
builder.Services.AddScoped<ApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

app.Run();
