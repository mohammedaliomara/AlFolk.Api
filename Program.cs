using System.Text;
using AlFolk.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AlFolk API", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

// Configure EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Services
builder.Services.AddScoped<AlFolk.Api.Services.IAuthService, AlFolk.Api.Services.AuthService>();
builder.Services.AddScoped<AlFolk.Api.Services.IParentService, AlFolk.Api.Services.ParentService>();
builder.Services.AddScoped<AlFolk.Api.Services.IChildService, AlFolk.Api.Services.ChildService>();
builder.Services.AddScoped<AlFolk.Api.Services.IContentService, AlFolk.Api.Services.ContentService>();
builder.Services.AddScoped<AlFolk.Api.Services.IAnalyticsService, AlFolk.Api.Services.AnalyticsService>();
builder.Services.AddScoped<AlFolk.Api.Services.ISearchService, AlFolk.Api.Services.SearchService>();
builder.Services.AddScoped<AlFolk.Api.Services.IActivityLogger, AlFolk.Api.Services.ActivityLogger>();
builder.Services.AddScoped<AlFolk.Api.Services.IFirebaseMessagingService, AlFolk.Api.Services.FirebaseMessagingService>();
builder.Services.AddHttpContextAccessor();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDeveloperExceptionPage(); // Always show detailed errors during development/fix phase
app.UseCors("AllowAll");

// Enable Swagger UI (both in development and production for this stage)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AlFolk API V1");
    c.RoutePrefix = string.Empty; // Set Swagger as the root page
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "App is running", time = DateTime.UtcNow }));

app.MapPost("/api/db-reset", async (AppDbContext db) =>
{
    try
    {
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.EnsureSchemaAsync(db);  // add missing columns first
        await DbSeeder.SeedAsync(db);
        return Results.Ok(new { message = "تمت إعادة ضبط الجداول وحقن البيانات بنجاح" });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message + " | " + ex.InnerException?.Message);
    }
});

app.MapGet("/api/db-fix", async (AppDbContext db) =>
{
    try
    {
        await DbSeeder.EnsureSchemaAsync(db);  // add missing columns first
        await DbSeeder.SeedAsync(db);
        return Results.Ok(new { message = "تم تحديث الهيكل وحقن البيانات بنجاح" });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message + " | " + ex.InnerException?.Message);
    }
});

app.MapGet("/api/db-diag", async (AppDbContext db) =>
{
    try
    {
        var diag = new Dictionary<string, object>();
        
        diag["AdminsCount"] = await db.Users.CountAsync(u => u.Role == "Admin");
        diag["ParentsCount"] = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Role == "Parent");
        diag["ActiveParentsCount"] = await db.Users.CountAsync(u => u.Role == "Parent");
        diag["ChildrenCount"] = await db.Children.IgnoreQueryFilters().CountAsync();
        diag["PlaylistsCount"] = await db.Playlists.CountAsync();
        diag["VideosCount"] = await db.Videos.CountAsync();
        
        diag["UserIds"] = await db.Users.IgnoreQueryFilters().OrderBy(p => p.Id).Select(p => p.Id).Take(10).ToListAsync();
        
        var connection = db.Database.GetDbConnection();
        diag["Database"] = connection.Database;
        diag["DataSource"] = connection.DataSource;

        return Results.Ok(diag);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message + " | " + ex.InnerException?.Message);
    }
});

app.MapGet("/api/db-check", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        var adminCount = canConnect ? await db.Users.CountAsync(u => u.Role == "Admin") : 0;
        return Results.Ok(new { 
            connected = canConnect, 
            adminCount = adminCount,
            message = canConnect ? "Connected to database" : "Cannot connect to database"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Apply migrations or create database (for development/demo purposes)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();

        db.Database.EnsureCreated();
        await DbSeeder.EnsureSchemaAsync(db);  // add missing columns before any query
        await DbSeeder.SeedAsync(db);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during database initialization.");
    
    Console.WriteLine($"[DB ERROR]: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"[DB INNER ERROR]: {ex.InnerException.Message}");
}

app.Run();
