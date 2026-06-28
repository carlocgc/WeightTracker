using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
var connectionString = builder.Configuration["ConnectionStrings:WeightTracker"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("The ConnectionStrings:WeightTracker connection string is required.");
}

connectionString = DatabaseInitializer.NormalizeSqliteConnectionString(connectionString, builder.Environment.ContentRootPath);
var dataProtectionKeyDirectory = DataProtectionKeyDirectory.Resolve(
    builder.Configuration["DataProtection:KeysPath"],
    connectionString,
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataProtectionKeyDirectory.FullName);
builder.Services.AddDataProtection()
    .SetApplicationName("WeightTracker")
    .PersistKeysToFileSystem(dataProtectionKeyDirectory);
builder.Services.AddDbContext<WeightTrackerDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<SettingsService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ILocalDateProvider, LocalDateProvider>();
builder.Services.AddScoped<WeightEntryService>();
builder.Services.AddScoped<WeightDataService>();
builder.Services.AddScoped<MetricsService>();

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

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();

public partial class Program;
