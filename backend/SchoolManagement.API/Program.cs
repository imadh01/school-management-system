using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.API.BackgroundJobs;
using SchoolManagement.API.Common;
using SchoolManagement.API.Configuration;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Middleware;
using SchoolManagement.API.Swagger;
using SchoolManagement.Application.DTOs.AdmissionFees;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.DTOs.Subjects;
using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Validators;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Infrastructure.Repositories;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Infrastructure.Storage;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog -----------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// --- Services ---------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
// Permission/role checks inside actions read the permission cache, never JWT claims (hotfix after D1).
builder.Services.AddScoped<ICurrentUserPermissions, HttpContextCurrentUserPermissions>();
// Scoped, not singleton: the interceptor remembers the user and what to log for the save in progress.
builder.Services.AddScoped<AuditableEntitySaveChangesInterceptor>();
builder.Services.AddScoped<IConcurrencyGuard, ConcurrencyGuard>();
// F1: "which records may this caller read?" (View → all, ViewScoped → related) and the sensitive-read audit row.
builder.Services.AddScoped<IDataScopeResolver, DataScopeResolver>();
builder.Services.AddScoped<ISensitiveReadLog, SensitiveReadLog>();

// ── D1: Permission infrastructure (Singleton) ─────────────────────────
builder.Services.AddSingleton<IPermissionCacheService, PermissionCacheService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
// E1: Roles API (guards live in RoleService).
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IValidator<CreateRoleRequest>, CreateRoleRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateRoleRequest>, UpdateRoleRequestValidator>();
builder.Services.AddScoped<IValidator<SetRolePermissionsRequest>, SetRolePermissionsRequestValidator>();
builder.Services.AddScoped<IValidator<ChangeRoleStatusRequest>, ChangeRoleStatusRequestValidator>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ── D2: Refresh tokens ────────────────────────────────────────────────
// Settings are read through IOptions at runtime (not eagerly here), so test
// configuration overrides reach them; see the JWT signing-key note in the test factory.
builder.Services.Configure<RefreshTokenSettings>(builder.Configuration.GetSection(RefreshTokenSettings.SectionName));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RefreshTokenSettings>>().Value);
builder.Services.Configure<RefreshTokenCookieOptions>(builder.Configuration.GetSection(RefreshTokenCookieOptions.SectionName));
builder.Services.AddSingleton<RefreshTokenCookie>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddHostedService<RefreshTokenCleanupJob>();
builder.Services.AddScoped<IUserService, UserService>();
// E2 security fix: every status / password / role / delete change of a login goes through here.
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<ISubjectService, SubjectService>();

builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
builder.Services.AddScoped<IValidator<ChangeUserStatusRequest>, ChangeUserStatusRequestValidator>();
builder.Services.AddScoped<IValidator<ResetUserPasswordRequest>, ResetUserPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateMyProfileRequest>, UpdateMyProfileRequestValidator>();
// E2: school settings. The provider is a singleton cache (like the permission cache).
builder.Services.AddScoped<ISchoolSettingsRepository, SchoolSettingsRepository>();
builder.Services.AddScoped<ISchoolSettingsService, SchoolSettingsService>();
builder.Services.AddSingleton<ISchoolSettingsProvider, SchoolSettingsProvider>();
builder.Services.AddScoped<IValidator<UpdateSchoolSettingsRequest>, UpdateSchoolSettingsRequestValidator>();

builder.Services.AddScoped<IClassSectionRepository, ClassSectionRepository>();
builder.Services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
builder.Services.AddScoped<IClassSectionService, ClassSectionService>();
builder.Services.AddScoped<IValidator<CreateClassSectionRequest>, CreateClassSectionRequestValidator>();

builder.Services.AddScoped<IAdmissionRepository, AdmissionRepository>();
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddScoped<IValidator<CreateAdmissionRequest>, CreateAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateAdmissionRequest>, UpdateAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<ConfirmAdmissionRequest>, ConfirmAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<EnrollAdmissionRequest>, EnrollAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<RejectAdmissionRequest>, RejectAdmissionRequestValidator>();

builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IStudentPhotoService, StudentPhotoService>();
builder.Services.AddScoped<IAdmissionFeeRepository, AdmissionFeeRepository>();
builder.Services.AddScoped<IAdmissionFeeService, AdmissionFeeService>();
builder.Services.AddScoped<IValidator<SetAdmissionFeeRequest>, SetAdmissionFeeRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStudentRequest>, CreateStudentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStudentFromAdmissionRequest>, CreateStudentFromAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateStudentRequest>, UpdateStudentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateStudentIdentityRequest>, UpdateStudentIdentityRequestValidator>();

builder.Services.AddScoped<IParentRepository, ParentRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IStudentGuardianRepository, StudentGuardianRepository>();
builder.Services.AddScoped<IParentService, ParentService>();
builder.Services.AddScoped<IStudentGuardianService, StudentGuardianService>();
builder.Services.AddScoped<IValidator<CreateParentRequest>, CreateParentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateParentRequest>, UpdateParentRequestValidator>();
builder.Services.AddScoped<IValidator<LinkGuardianRequest>, LinkGuardianRequestValidator>();
builder.Services.AddScoped<IValidator<LinkStudentRequest>, LinkStudentRequestValidator>();

builder.Services.AddScoped<IValidator<CreateSubjectRequest>, CreateSubjectRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSubjectRequest>, UpdateSubjectRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateClassSectionRequest>, UpdateClassSectionRequestValidator>();

builder.Services.AddScoped<ITeacherRepository, TeacherRepository>();
builder.Services.AddScoped<ITeacherService, TeacherService>();
builder.Services.AddScoped<IValidator<UpdateTeacherRequest>, UpdateTeacherRequestValidator>();
builder.Services.AddScoped<IValidator<AssignSubjectsRequest>, AssignSubjectsRequestValidator>();
builder.Services.AddScoped<IValidator<SetClassTeacherRequest>, SetClassTeacherRequestValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IValidator<SaveAttendanceRequest>, SaveAttendanceRequestValidator>();

// ── G1b: Documents ────────────────────────────────────────────────────
// Settings (size limit, retention, storage folder) come from the "Documents" section and are checked
// at startup: a wrong value stops the API with a clear message instead of failing on the first upload.
builder.Services.AddOptions<DocumentSettings>()
    .Bind(builder.Configuration.GetSection(DocumentSettings.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DocumentSettings>, DocumentSettingsValidator>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<DocumentSettings>>().Value);
// Files live OUTSIDE the web root; an empty StorageRoot means "App_Data/documents" next to the API.
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var settings = sp.GetRequiredService<DocumentSettings>();
    var contentRoot = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
    var root = string.IsNullOrWhiteSpace(settings.StorageRoot)
        ? Path.Combine(contentRoot, "App_Data", "documents")
        : Path.GetFullPath(settings.StorageRoot, contentRoot);
    return new LocalFileStorage(root);
});
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IValidator<UploadDocumentRequest>, UploadDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<ReviewDocumentRequest>, ReviewDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<RejectDocumentRequest>, RejectDocumentRequestValidator>();
builder.Services.AddHostedService<DocumentPurgeJob>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Default 401/403 responses from the auth middleware have empty
        // bodies — override both so every error response, regardless of
        // where in the pipeline it originates, matches the same
        // ApiErrorResponse envelope (master prompt §8).
        options.Events = new JwtBearerEvents
        {
            // ── D1: Security stamp validation on every request ────────
            // After the JWT signature and lifetime pass, verify that
            // the security_stamp in the token still matches the one
            // in the database (via the permission cache).  If the user
            // changed their password or an admin rotated their stamp,
            // the old token is immediately rejected.
            OnTokenValidated = async context =>
            {
                var cache = context.HttpContext.RequestServices
                    .GetRequiredService<IPermissionCacheService>();

                var userIdValue = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                  ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user identity.");
                    return;
                }

                var info = await cache.GetUserInfoAsync(userId);

                // User deleted or doesn't exist.
                if (info is null)
                {
                    context.Fail("User not found.");
                    return;
                }

                // Soft-deleted or inactive user — token is no longer valid.
                if (info.IsDeleted || info.Status != "Active")
                {
                    context.Fail("Account is not active.");
                    return;
                }

                // Security stamp mismatch — password was changed or
                // admin rotated the stamp → old token rejected.
                var tokenStamp = context.Principal?
                    .FindFirst("security_stamp")?.Value;

                if (tokenStamp is null || tokenStamp != info.SecurityStamp)
                {
                    context.Fail("Security stamp mismatch.");
                    return;
                }
            },

            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ApiErrorResponse
                {
                    Message = "Authentication required.",
                    ErrorCode = "UNAUTHENTICATED",
                    TraceId = context.HttpContext.GetCorrelationId(),
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ApiErrorResponse
                {
                    Message = "You do not have permission to perform this action.",
                    ErrorCode = "FORBIDDEN",
                    TraceId = context.HttpContext.GetCorrelationId(),
                });
            },
        };
    });

// ── D1: Authorization — convention-based + composite policies ─────────
// The PermissionPolicyProvider auto-generates policies for every
// permission in Permissions.All.  Only composite policies that combine
// multiple permissions need manual registration here.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // "Any one of" policies, e.g. Attendance.Write = Manage OR Mark, Students.Read = View OR ViewScoped.
    // The list lives in CompositePolicies so the tests read the same definitions.
    foreach (var (name, anyOf) in CompositePolicies.AnyOf)
        options.AddPolicy(name, policy => policy.AddRequirements(new AnyPermissionRequirement(anyOf)));
});

// ── D2: CORS ──────────────────────────────────────────────────────────
// The UI and API are deployed same-origin (dev: the Vite proxy forwards /api), so the
// browser needs no CORS at all and the list is empty by default. It exists for the day a
// trusted origin must call the API directly: list it in Cors:AllowedOrigins per environment.
// Never use a wildcard: browsers refuse "*" together with credentials anyway.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Frontend, policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowCredentials()
              .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Correlation-Id")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE");
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // D2: shows the X-Requested-With header on /api/auth/refresh and /api/auth/logout.
    options.OperationFilter<CsrfHeaderOperationFilter>();

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token below. No need to type the word 'Bearer' — just paste the token itself."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --- Pipeline -----------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseCors(CorsPolicies.Frontend);
app.UseAuthentication();
// D1: MustChangePassword check runs AFTER authentication but BEFORE authorization.
// This means the user is identified (JWT valid) but blocked from everything
// except /auth/me and /auth/change-password.
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }

internal static class CorsPolicies
{
    public const string Frontend = "Frontend";
}
