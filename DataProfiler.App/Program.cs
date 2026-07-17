using DataProfiler.App.Services.Reporting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<DataProfiler.App.Services.Discovery.SchemaDiscoveryService>();
builder.Services.AddSingleton<DataProfiler.App.Services.Profiling.TableProfilingService>();
builder.Services.AddSingleton<DataProfiler.App.Services.Reporting.TableReportService>();
builder.Services.AddSingleton<DataProfiler.App.Services.Reporting.TableReportJobStore>();
builder.Services.AddSingleton<ITableReportJobQueue, TableReportJobQueue>();
builder.Services.AddHostedService<TableReportBackgroundService>();
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
