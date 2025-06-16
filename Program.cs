using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Z_TRIP.Helpers;
using Z_TRIP.Services;
using Z_TRIP.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Biarkan ASP.NET Core listen ke semua IP di port 5165
builder.WebHost.UseUrls("http://0.0.0.0:5165");

// Tambahkan layanan (services)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Tambahkan default value handling
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Konfigurasi CORS (jika diperlukan untuk Flutter/Web)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Tambahkan Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Z-TRIP",
        Version = "v1"
    });

    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Harap masukkan token JWT di field: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Sebelum menambahkan JWT authentication, tambahkan pengecekan dan fallback:
if (string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]))
{
    // Log warning
    Console.WriteLine("WARNING: JWT key not found in configuration. Using hardcoded key for development.");

    // Add hardcoded JWT config for development
    builder.Configuration["Jwt:Key"] = "secret_key_untuk_token_jwt_yang_aman_development_only";
    builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Issuer"] ?? "Z-TRIP";
    builder.Configuration["Jwt:Audience"] = builder.Configuration["Jwt:Audience"] ?? "Z-TRIP-Mobile";
}

// Konfigurasi JWT Authentication setelahnya...
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
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:Key"] ??
                // Gunakan hardcoded value sebagai fallback
                "secret_key_untuk_token_jwt_yang_aman_sebagai_fallback_value"
            )
        )
    };
});

// Tambahkan kebijakan otorisasi
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Tambahkan menggunakan dependency injection
builder.Services.AddSingleton<EmailService>();

// Perbaiki middleware order
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Gunakan CORS sebelum routing
app.UseCors("AllowAll");

// Gunakan error handling middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();