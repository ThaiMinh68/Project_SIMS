using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;
using SIMS.Persistence.Data;
using SIMS.Persistence.Repositories;
using SIMS.Application.Interfaces;
using SIMS.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

// Add DI for Repositories and Unit of Work
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add DI for Services
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IFacultyService, FacultyService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IStudentPortalService, StudentPortalService>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Apply any pending migrations (including Attendances) at startup
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        // Ensure new Student columns exist (FirstName, LastName, Email) for runtime compatibility
        // If migrations were not created/applied, add columns safely.
        var ensureColumnsSql = @"
IF COL_LENGTH('Students', 'FirstName') IS NULL
BEGIN
    ALTER TABLE Students ADD FirstName nvarchar(max) DEFAULT('') NOT NULL;
END
IF COL_LENGTH('Students', 'LastName') IS NULL
BEGIN
    ALTER TABLE Students ADD LastName nvarchar(max) DEFAULT('') NOT NULL;
END
IF COL_LENGTH('Students', 'Email') IS NULL
BEGIN
    ALTER TABLE Students ADD Email nvarchar(max) DEFAULT('') NOT NULL;
END
-- Backfill any existing NULLs to empty string and ensure NOT NULL
UPDATE Students SET FirstName = '' WHERE FirstName IS NULL;
UPDATE Students SET LastName = '' WHERE LastName IS NULL;
UPDATE Students SET Email = '' WHERE Email IS NULL;
";
        // Ensure student columns
        await db.Database.ExecuteSqlRawAsync(ensureColumnsSql);

        // Ensure course schedule columns exist (MeetingDay, StartTime, EndTime)
        var ensureCourseColumnsSql = @"
IF COL_LENGTH('Courses', 'MeetingDay') IS NULL
BEGIN
    ALTER TABLE Courses ADD MeetingDay nvarchar(200) DEFAULT('') NOT NULL;
END
IF COL_LENGTH('Courses', 'StartTime') IS NULL
BEGIN
    ALTER TABLE Courses ADD StartTime time NULL;
END
IF COL_LENGTH('Courses', 'EndTime') IS NULL
BEGIN
    ALTER TABLE Courses ADD EndTime time NULL;
END
-- Backfill any existing NULLs to empty string where applicable
UPDATE Courses SET MeetingDay = '' WHERE MeetingDay IS NULL;
";

        await db.Database.ExecuteSqlRawAsync(ensureCourseColumnsSql);

        await DbInitializer.SeedUsersAndRolesAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
