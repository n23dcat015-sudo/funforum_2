using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Funforum.Api.BackgroundJobs;
using Funforum.Api.Common;
using Funforum.Api.Data;
using Funforum.Api.Security;
using Funforum.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Giai đoạn 1: khung project — Swagger, CORS, health check.
// Giai đoạn 2: DbContext (EF Core + Npgsql), JWT Authentication/Authorization,
// seed Super Admin, auth/users endpoint.
// Giai đoạn 3: board/thread/reply/vote, hot-rank + đối soát + dọn file/notification nền,
// ảnh đính kèm, nested reply, notifications (xem Controllers/, Services/, BackgroundJobs/).
// ---------------------------------------------------------------------------

const string FrontendCorsPolicy = "FrontendCors";

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Funforum API",
        Version = "v1",
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập access token (không cần tiền tố 'Bearer ')",
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// current_state.md mục 3.6: multipart/form-data (đăng thread/reply có ảnh) tối đa 5 MB —
// đặt ở Kestrel làm mốc TRÊN. JSON tối đa 100 KB được siết riêng bằng middleware bên dưới
// (sau app.Build()), vì Kestrel chỉ có 1 giới hạn chung cho toàn bộ request.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});

// current_state.md mục 3.7: thư mục upload, cấu hình qua UPLOADS_PATH.
var uploadPaths = new UploadPaths(builder.Configuration["UPLOADS_PATH"] ?? "uploads");
builder.Services.AddSingleton(uploadPaths);

// current_state.md mục 3.8: đọc được từ biến môi trường ContentLimits__MaxRepliesPerThread /
// ContentLimits__MaxImagesPerThread (mặc định 500/150 ở appsettings.json).
builder.Services.Configure<ContentLimitsOptions>(
    builder.Configuration.GetSection(ContentLimitsOptions.SectionName));

// --- Data access ---
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<FunforumDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// --- JWT ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must be set and at least 32 bytes (256 bits) long. " +
        "Generate one with: openssl rand -base64 32");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, RoleRequirementHandler>();
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

// Phase C — rate-limit thô theo IP cho forgot-password (endpoint công khai, dễ bị spam mail).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            }));
});

// --- DI ---
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Phase D — Google Sign-In: verify ID token + cấu hình ClientId.
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));

// Phase C — quên/đặt lại mật khẩu + cấu hình link frontend cho mail.
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));

// Phase B — email: có Email:Host thì gửi SMTP thật, chưa cấu hình (dev/test) thì ghi ra log.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
if (string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"]))
{
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

// X.2: audit log dùng chung, stage vào cùng DbContext scoped với nghiệp vụ.
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// X.3: report queue. X.4: ban/unban tài khoản + IP. X.5: kháng cáo ban.
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAdminReportService, AdminReportService>();
builder.Services.AddScoped<IBanService, BanService>();
builder.Services.AddScoped<IAppealService, AppealService>();
builder.Services.AddScoped<IAdminAppealService, AdminAppealService>();

// Giai đoạn 3: nghiệp vụ board/thread/reply/vote/notification.
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IAdminBoardService, AdminBoardService>();
builder.Services.AddScoped<IAdminThreadService, AdminThreadService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminAdminService, AdminAdminService>();
builder.Services.AddScoped<IAdminAuditLogService, AdminAuditLogService>();
builder.Services.AddScoped<IThreadService, ThreadService>();
builder.Services.AddScoped<IReplyService, ReplyService>();
builder.Services.AddScoped<IVoteService, VoteService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Job nền: hot_rank (5 phút), đối soát score/reply_count/child_count/image_count (1 giờ),
// dọn file ảnh mồ côi (2 giờ), dọn notification cũ hơn 30 ngày (2 giờ) — mục 4.1, 3.7, 10,
// 11.9 (try/catch, không làm gián đoạn app).
builder.Services.AddHostedService<HotRankRecalculationService>();
builder.Services.AddHostedService<ContentReconciliationService>();
builder.Services.AddHostedService<OrphanFileCleanupService>();
builder.Services.AddHostedService<NotificationCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<Funforum.Api.Common.ApiExceptionMiddleware>();

app.UseHttpsRedirection();

// current_state.md mục 3.6: JSON tối đa 100 KB. Multipart (5 MB) đã đặt ở Kestrel phía
// trên; ở đây SIẾT XUỐNG 100 KB cho mọi request KHÔNG phải multipart/form-data, trước khi
// model binding đọc body. Đặt trước UseAuthentication/MapControllers để chắc chắn áp dụng
// trước khi action đọc body.
app.Use(async (context, next) =>
{
    var isMultipart = context.Request.ContentType?
        .Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;
    if (!isMultipart)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = 100 * 1024;
        }
    }
    await next();
});

// Dev: backend tự phục vụ /uploads (Vite proxy chuyển tiếp). Production (GĐ9): Nginx phục
// vụ trực tiếp từ volume, backend không cần UseStaticFiles nữa.
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadPaths.Root),
        RequestPath = "/uploads",
    });
}

app.UseCors(FrontendCorsPolicy);
// UseRouting tường minh để BanCheckMiddleware đọc được endpoint metadata ([RequiresActiveUser]).
app.UseRouting();
// Phase C — rate limiter phải sau UseRouting để đọc được policy gắn ở endpoint.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// X.4 — chặn đăng bài/reply/vote khi user bị ban (đặt sau Authorization để có User đã xác thực).
app.UseMiddleware<BanCheckMiddleware>();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FunforumDbContext>();
    await db.Database.MigrateAsync();
    await BoardSeed.EnsureAsync(db);

    if (!await db.Admins.AnyAsync())
    {
        var seedUsername = builder.Configuration["SeedSuperAdmin:Username"];
        var seedEmail = builder.Configuration["SeedSuperAdmin:Email"];
        var seedPassword = builder.Configuration["SeedSuperAdmin:Password"];

        if (!string.IsNullOrEmpty(seedUsername) &&
            !string.IsNullOrEmpty(seedEmail) &&
            !string.IsNullOrEmpty(seedPassword))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Admins.Add(new Funforum.Api.Entities.Admin
            {
                Username = seedUsername,
                Email = seedEmail,
                PasswordHash = hasher.Hash(seedPassword),
                Role = Roles.SuperAdmin,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Seeded first Super Admin account {Username}.", seedUsername);
        }
        else
        {
            app.Logger.LogWarning(
                "Table 'admins' is empty but SeedSuperAdmin:Username/Email/Password " +
                "are not set — no Super Admin was created. Set them via environment " +
                "variables (see .env.example) and restart.");
        }
    }
}

app.Run();