using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MultiTaskApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<JsonImportService>();

// Configure MySQL connection
var connectionString = builder.Configuration.GetConnectionString("MySqlConnection");
builder.Services.AddDbContext<ApplicationContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    var log = $"{DateTime.Now:yyyy/MM/dd - HH:mm:ss} | {context.Response.StatusCode} | {stopwatch.Elapsed.TotalSeconds:F7}s | {context.Connection.RemoteIpAddress} | {context.Request.Method} \"{context.Request.Path}\"";
    Console.WriteLine(log);
});

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllers();
});

app.Run();