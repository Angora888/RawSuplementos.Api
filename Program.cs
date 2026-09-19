using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// CONTROLLERS
// =========================

builder.Services.AddControllers();
builder.Services.AddScoped<UsuarioTenantService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<VentaConsultaService>();


// =========================
// SWAGGER / OPENAPI
// =========================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenApi();


// =========================
// POSTGRESQL
// =========================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection"
    );

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection no está configurado."
    );
}

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseNpgsql(connectionString)
);


// =========================
// JWT
// =========================

var jwtKey =
    builder.Configuration["Jwt:Key"];

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key no está configurado."
    );
}

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer no está configurado."
    );
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Audience no está configurado."
    );
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey
                        )
                    ),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();


// =========================
// CORS
// =========================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173",
                    "https://raw-suplementos.vercel.app",
                    "https://miemprendimiento.vercel.app"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


// =========================
// BUILD APP
// =========================

var app = builder.Build();


// =========================
// DEVELOPMENT
// =========================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Mi Emprendimiento API v1"
        );

        options.RoutePrefix =
            "swagger";
    });
}


// =========================
// MIDDLEWARE
// =========================

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();