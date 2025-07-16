using SoraEssayJudge.Services;
using SoraEssayJudge.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

// 1. Configure Serilog for bootstrap logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 2. Add full Serilog support
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the container.
    builder.Services.AddDbContext<EssayContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 添加 CORS 服务
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
    
    builder.Services.AddScoped<ApiKeyService>();
    builder.Services.AddScoped<OpenAIService>();
    builder.Services.AddScoped<RecognizeHandwritingService>();
    builder.Services.AddScoped<ProcessImageService>();
    builder.Services.AddScoped<JudgeService>();
    builder.Services.AddScoped<IPreProcessImageService, PreProcessImageService>();
    

    builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(s =>
    {
        s.SwaggerDoc("v1", new OpenApiInfo { Title = "SoraEssayJudge Api", Version = "v1" });
        s.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
        {
            Description = "在下框中输入请求头中需要添加Jwt授权Token：Bearer Token",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
    
        s.AddSecurityRequirement(new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecurityScheme{
                                    Reference = new OpenApiReference {
                                                Type = ReferenceType.SecurityScheme,
                                                Id = "Bearer"}
                               },new string[] { }
                            }
                        });
    
        
    });


    // JWT 认证服务
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

    // 配置转发头选项
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // 安全提示：在生产环境中，最好清除 KnownProxies 和 KnownNetworks
        // 并明确指定您的代理服务器的 IP 地址，以防止 IP 欺骗。
        // options.KnownProxies.Add(IPAddress.Parse("YOUR_NGINX_IP_ADDRESS"));
    });

    var app = builder.Build();
    
    // 3. Add Serilog request logging middleware
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseForwardedHeaders();
    // 启用 CORS
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();



    // 确保 essayfiles 目录存在
    var essayFilesPath = Path.Combine(app.Environment.ContentRootPath, "essayfiles");
    if (!Directory.Exists(essayFilesPath))
    {
        Directory.CreateDirectory(essayFilesPath);
    }

    // 配置 essayfiles 目录为静态文件目录
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(essayFilesPath),
        RequestPath = "/essayfiles"
    });

    app.UseHttpsRedirection();

    app.MapControllers();

    // 在应用程序启动时应用数据库迁移
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<EssayContext>();
            context.Database.EnsureCreated(); // 确保数据库已创建
            context.Database.Migrate(); // 应用所有待处理的迁移
            Log.Information("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the database.");
            // 根据需要，可以选择在这里停止应用程序或继续运行
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

