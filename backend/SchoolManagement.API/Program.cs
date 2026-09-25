using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Middleware;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Validators;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Infrastructure.Repositories;
using SchoolManagement.Infrastructure.Security;
using Serilog;
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
builder.Services.AddSingleton<AuditableEntitySaveChangesInterceptor>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();

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
builder.Services.AddScoped<IValidator<CreateStudentRequest>, CreateStudentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStudentFromAdmissionRequest>, CreateStudentFromAdmissionRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateStudentRequest>, UpdateStudentRequestValidator>();

builder.Services.AddScoped<IParentRepository, ParentRepository>();
builder.Services.AddScoped<IStudentGuardianRepository, StudentGuardianRepository>();
builder.Services.AddScoped<IParentService, ParentService>();
builder.Services.AddScoped<IStudentGuardianService, StudentGuardianService>();
builder.Services.AddScoped<IValidator<CreateParentRequest>, CreateParentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateParentRequest>, UpdateParentRequestValidator>();
builder.Services.AddScoped<IValidator<LinkGuardianRequest>, LinkGuardianRequestValidator>();

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

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("Users.Create", policy =>
        policy.RequireClaim("permission", "Users.Create"));

    options.AddPolicy("ClassSections.Create", policy =>
    policy.RequireClaim("permission", "ClassSections.Create"));

    options.AddPolicy("Admissions.Manage", policy =>
    policy.RequireClaim("permission", "Admissions.Manage"));

    options.AddPolicy("Students.Manage", policy =>
    policy.RequireClaim("permission", "Students.Manage"));

    options.AddPolicy("Parents.Manage", policy =>
    policy.RequireClaim("permission", "Parents.Manage"));

});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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
app.UseCors("AllowFrontendDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }