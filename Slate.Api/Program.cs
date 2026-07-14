using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Slate.Api.Data;
using Slate.Api.Hubs;
using Slate.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// --- Auth ---
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
        };

        // SignalR's WebSocket transport can't set an Authorization header, so the
        // client passes the JWT as a query string param instead (?access_token=...).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ITokenService, TokenService>();

// --- CORS: allow the Vite dev server (and the deployed frontend origin) ---
const string CorsPolicy = "SlateFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5173" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // needed once SignalR is added in Phase 2
    });
});

// --- API infra ---
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Apply any pending EF Core migrations automatically on startup, so the
// production database schema always matches the code without a manual step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // raw OpenAPI JSON at /openapi/v1.json — add a UI package later if you want a browsable page
}

// No HTTPS redirection: this project only serves plain http://localhost:5000
// locally (there's no HTTPS cert/port configured, which is why this used to
// print "Failed to determine the https port for redirect" and — worse —
// silently broke CORS in Production, since IsDevelopment() was false there
// and the 307 redirect it issued ran *before* UseCors below, stripping the
// CORS headers and making the browser report a misleading CORS error. Once
// this is actually deployed (Railway/Render etc.), those platforms terminate
// HTTPS themselves before the request reaches this app, so this isn't needed
// there either — revisit only if a specific hosting setup calls for it.
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");
app.MapHub<ChessHub>("/hubs/chess");
app.MapHub<CardGameHub>("/hubs/cardgame");

app.Run();
