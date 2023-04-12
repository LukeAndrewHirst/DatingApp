using API.Data;
using API.Entities;
using API.Extensions;
using API.Middleware;
using API.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationSevices(builder.Configuration);
builder.Services.AddIdentitySevices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebAPIv5", Version = "v1" });
});
builder.Services.AddCors();     
builder.Services.AddSignalR();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithOrigins("https://localhost:4200"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PresenceHub>("hubs/presence");
app.MapHub<MessageHub>("hubs/message");
app.MapFallbackToController("Index","Fallback");


using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    var context = services.GetRequiredService<DataContext>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

    await context.Database.MigrateAsync();
    await Seed.SeedUsers(userManager,roleManager);
}
catch(Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "Unable to import data for your migration");
}

app.Run();