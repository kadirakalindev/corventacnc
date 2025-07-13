using BendingMachine.Api.Hubs;
using BendingMachine.Api.Services;
using BendingMachine.Domain.Configuration;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Driver;
using BendingMachine.Driver.Services;
using BendingMachine.Infrastructure.Modbus;
using BendingMachine.Application.Interfaces;
using BendingMachine.Application.Services;
using BendingMachine.Application.Mapping;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<MachineConfiguration>(
    builder.Configuration.GetSection("MachineConfiguration"));

// Add services to the container.
builder.Services.AddOpenApi();

// Swagger Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Corventa CNC Bending Machine API", 
        Version = "v1",
        Description = "CNC Profil Büküm Makinesi API Documentation"
    });
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<BendingMachine.Application.Validators.BendingCalculationRequestValidator>();

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5038") // Web projesi portu
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR için gerekli
    });
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
});

// Machine Services - Clean Architecture kurallarına uygun
builder.Services.AddSingleton<IModbusClient>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var modbusConfig = config.GetSection("MachineConfiguration:Modbus");
    
    return new NModbusClient(
        ipAddress: modbusConfig.GetValue<string>("IpAddress") ?? "192.168.1.100",
        port: modbusConfig.GetValue<int>("Port"),
        slaveId: (byte)modbusConfig.GetValue<int>("SlaveId"),
        timeoutMs: modbusConfig.GetValue<int>("TimeoutMs")
    );
});

builder.Services.AddSingleton<IMachineDriver, MachineDriver>();
builder.Services.AddSingleton<MachineStatusService>();

// Application Layer Services
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<IPistonService, PistonService>();
builder.Services.AddScoped<IBendingService, BendingService>();
builder.Services.AddScoped<BendingCalculatorService>();

// Background Services
builder.Services.AddHostedService<MachineStatusService>();
builder.Services.AddHostedService<SignalRMachineService>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Corventa CNC Bending Machine API V1");
        c.RoutePrefix = "swagger"; // Swagger UI /swagger adresinde olacak
        c.DocumentTitle = "Corventa CNC API";
        c.EnableTryItOutByDefault();
    });
}

// app.UseHttpsRedirection(); // HTTPS yönlendirmesi devre dışı - API sadece HTTP'de çalışacak

// CORS'u UseRouting'den sonra ekle
app.UseCors("AllowAll");

// Default files - index.html gibi default dosyalar için (Static Files'tan önce)
app.UseDefaultFiles();

// Static Files - wwwroot için gerekli (test-client.html)
app.UseStaticFiles();

app.UseRouting();

// Controllers
app.MapControllers();

// SignalR Hub - /machinestatus endpoint
app.MapHub<MachineStatusHub>("/machinestatus");

// Health check endpoint
app.MapGet("/health", () => new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0"
});

// Machine connection status endpoint - REMOVED (MachineController'da var)

app.Run();
