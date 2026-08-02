using DatabaseProfiler.App.Services.Reporting;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Configure Data Protection with persistent keys
var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("DatabaseProfiler.App");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<DatabaseProfiler.App.Services.Discovery.SchemaDiscoveryService>();
builder.Services.Configure<DatabaseProfiler.App.Services.Profiling.TableProfilingPolicyOptions>(builder.Configuration.GetSection("TableReports:Profiling"));
builder.Services.AddSingleton<DatabaseProfiler.App.Services.Profiling.TableProfilingService>();
builder.Services.AddSingleton<DatabaseProfiler.App.Services.Reporting.TableReportService>();
builder.Services.AddSingleton<DatabaseProfiler.App.Services.Reporting.RelationshipReportService>();
builder.Services.AddSingleton<DatabaseProfiler.App.Services.Reporting.TableReportJobStore>();
builder.Services.AddSingleton<ITableReportJobQueue, TableReportJobQueue>();
builder.Services.AddHostedService<TableReportBackgroundService>();
builder.Services.Configure<TableReportJobStoreOptions>(builder.Configuration.GetSection("TableReports:Retention"));
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
