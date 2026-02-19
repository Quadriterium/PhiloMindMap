using PhiloMindMap.Business;
using PhiloMindMap.Business.Data;
using PhiloMindMap.Client.Pages;
using PhiloMindMap.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddServerSideBlazor().AddCircuitOptions(options => { options.DetailedErrors = true; });
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddControllers();
builder.Services.AddDbContext<PhiloMindMapDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PhiloMindMapDb")
                      ?? "Data Source=philo-mindmap.db"));
builder.Services.AddScoped<PhilosopherService>();

// add console logger
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

builder.Services.AddHttpClient();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var philosopherService = scope.ServiceProvider.GetRequiredService<PhilosopherService>();
    philosopherService.InitializeDatabase();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapControllers();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(PhiloMindMap.Client._Imports).Assembly);

app.Run();
