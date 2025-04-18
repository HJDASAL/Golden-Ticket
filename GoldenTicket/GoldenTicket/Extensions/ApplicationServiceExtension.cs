using GoldenTicket.Hubs;
using GoldenTicket.Services;
using GoldenTicket.Utilities;
using Hangfire;
using Hangfire.MySql;
using OpenAIApp.Services;
namespace GoldenTicket.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers();

        string ConnectionString = config["ConnectionString"] ?? throw new Exception("Connection String is Invalid");

        // ✅ Hangfire Configuration
        services.AddHangfire(config => config.UseStorage(new MySqlStorage(
            ConnectionString,
            new MySqlStorageOptions { TablesPrefix = "Hangfire_" }
        )));
        services.AddHangfireServer();

        // ✅ Register Hangfire Services
        services.AddTransient<IRecurringJobManager>(provider => new RecurringJobManager());
        services.AddSingleton<HangFireService>();
        
        services.AddSingleton<ConfigService>();
        services.AddSingleton<PromptService>();
        services.AddSingleton<OpenAIService>();
        services.AddSingleton<AIUtil>();
        services.AddSingleton<GTHub>();
        services.AddSingleton<ApiConfig>();
        
        services.AddSignalR().AddHubOptions<GTHub>(options =>
        {
            options.EnableDetailedErrors = true;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(10);
            options.KeepAliveInterval = TimeSpan.FromSeconds(5);
        });
        services.AddControllersWithViews();
        
        // Add Database Context HERE
        // services.AddDbContext<DataContext>(opt =>
        // {
        //     opt.UseSqlite(config.GetConnectionString("DefaultConnection"));
        // });

        services.AddCors((opt) => opt.AddPolicy(
            "GoldenTracker",
            (policy) => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
        ));

        // // Define flutter web build file provider
        // string flutterWebPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "app");
        // services.AddSingleton<IFileProvider>(new PhysicalFileProvider(flutterWebPath));

        return services;
    }
}