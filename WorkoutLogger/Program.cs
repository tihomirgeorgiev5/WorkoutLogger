using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Register CORS service
// Allowed Angular origins come from configuration (defaults to local development)
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Database path comes from configuration (defaults to a local file)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=workouts.db";

builder.Services.AddDbContext<WorkoutDb>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<WorkoutDb>(options =>
    options.UseSqlite("Data Source=workouts.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ==========================================
// 2. Enable CORS Middleware
// Must be placed HERE – before mapping endpoints (app.MapGroup)
// ==========================================
app.UseCors("AllowAngular");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply pending migrations (creates the database on the first run)
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WorkoutDb>().Database.Migrate();
}

var group = app.MapGroup("/workouts");

// List workouts, optionally filtered by ?from=2026-10-01&to=2026-10-31
group.MapGet("/", async (DateOnly? from, DateOnly? to, WorkoutDb db) =>
{
    var query = db.Workouts.Include(w => w.Sets).AsQueryable();

    if (from is { } f) query = query.Where(w => w.Date >= f);
    if (to is { } t) query = query.Where(w => w.Date <= t);

    var workouts = await query
        .OrderByDescending(w => w.Date)
        .ThenByDescending(w => w.Id)
        .ToListAsync();

    return workouts.Select(ToDto);
});

// Personal records: heaviest set per exercise
group.MapGet("/records", async (WorkoutDb db) =>
{
    var sets = await db.ExerciseSets.Include(s => s.Workout).ToListAsync();

    return sets
        .GroupBy(s => s.Exercise.Trim().ToLowerInvariant())
        .Select(g =>
        {
            var best = g.OrderByDescending(s => s.WeightKg)
                        .ThenByDescending(s => s.Reps)
                        .First();

            return new ExerciseRecord(
                best.Exercise, best.WeightKg, best.Reps, best.Workout!.Date, g.Count());
        })
        .OrderBy(r => r.Exercise)
        .ToList();
});

// Weekly volume: sum of reps x weight per week (weeks start on Monday)
group.MapGet("/volume/weekly", async (WorkoutDb db) =>
{
    var sets = await db.ExerciseSets.Include(s => s.Workout).ToListAsync();

    return sets
        .GroupBy(s => StartOfWeek(s.Workout!.Date))
        .OrderByDescending(g => g.Key)
        .Select(g => new WeeklyVolume(g.Key, g.Count(), g.Sum(s => s.Reps * s.WeightKg)))
        .ToList();
});

// Get one workout with its sets
group.MapGet("/{id}", async (int id, WorkoutDb db) =>
    await db.Workouts.Include(w => w.Sets).FirstOrDefaultAsync(w => w.Id == id) is { } workout
        ? Results.Ok(ToDto(workout))
        : Results.NotFound());

// Create a workout together with its sets
group.MapPost("/", async (NewWorkout input, WorkoutDb db) =>
{
    var errors = Validate(input);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var workout = new Workout
    {
        Date = input.Date ?? DateOnly.FromDateTime(DateTime.Today),
        Notes = input.Notes?.Trim(),
        Sets = input.Sets!.Select(s => new ExerciseSet
        {
            Exercise = s.Exercise!.Trim(),
            Reps = s.Reps,
            WeightKg = s.WeightKg
        }).ToList()
    };

    db.Workouts.Add(workout);
    await db.SaveChangesAsync();
    return Results.Created($"/workouts/{workout.Id}", ToDto(workout));
});

// Replace a workout (date, notes and all sets)
group.MapPut("/{id}", async (int id, NewWorkout input, WorkoutDb db) =>
{
    var workout = await db.Workouts.Include(w => w.Sets).FirstOrDefaultAsync(w => w.Id == id);
    if (workout is null) return Results.NotFound();

    var errors = Validate(input);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    // Keep the current date if none was sent
    workout.Date = input.Date ?? workout.Date;
    workout.Notes = input.Notes?.Trim();

    // Remove the old sets and add the new ones
    db.ExerciseSets.RemoveRange(workout.Sets);
    workout.Sets.Clear();

    foreach (var s in input.Sets!)
    {
        workout.Sets.Add(new ExerciseSet
        {
            Exercise = s.Exercise!.Trim(),
            Reps = s.Reps,
            WeightKg = s.WeightKg
        });
    }

    await db.SaveChangesAsync();
    return Results.Ok(ToDto(workout));
});

// Delete a workout (its sets are deleted automatically by cascade)
group.MapDelete("/{id}", async (int id, WorkoutDb db) =>
{
    var workout = await db.Workouts.FindAsync(id);
    if (workout is null) return Results.NotFound();

    db.Workouts.Remove(workout);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// Maps a database entity to the object we return as JSON (DTO)
static WorkoutDto ToDto(Workout w) =>
    new(w.Id, w.Date, w.Notes,
        w.Sets.OrderBy(s => s.Id)
              .Select(s => new SetDto(s.Id, s.Exercise, s.Reps, s.WeightKg))
              .ToList());

// Collects validation errors as field name -> list of error messages
static Dictionary<string, string[]> Validate(NewWorkout input)
{
    var errors = new Dictionary<string, string[]>();

    if (input.Date is { } date && date > DateOnly.FromDateTime(DateTime.Today))
        errors["date"] = ["Date cannot be in the future."];

    if (input.Notes is { Length: > 500 })
        errors["notes"] = ["Notes must be 500 characters or fewer."];

    if (input.Sets is null || input.Sets.Count == 0)
    {
        errors["sets"] = ["At least one set is required."];
        return errors;
    }

    if (input.Sets.Count > 50)
        errors["sets"] = ["A workout can have at most 50 sets."];

    for (var i = 0; i < input.Sets.Count; i++)
    {
        var item = input.Sets[i];

        if (item is null)
        {
            errors[$"sets[{i}]"] = ["Set cannot be empty."];
            continue;
        }

        if (string.IsNullOrWhiteSpace(item.Exercise))
            errors[$"sets[{i}].exercise"] = ["Exercise is required."];
        else if (item.Exercise.Trim().Length > 100)
            errors[$"sets[{i}].exercise"] = ["Exercise must be 100 characters or fewer."];

        if (item.Reps < 1 || item.Reps > 1000)
            errors[$"sets[{i}].reps"] = ["Reps must be between 1 and 1000."];

        if (item.WeightKg < 0 || item.WeightKg > 1000)
            errors[$"sets[{i}].weightKg"] = ["Weight must be between 0 and 1000 kg."];
    }

    return errors;
}

// Returns the Monday of the week that contains the given date
static DateOnly StartOfWeek(DateOnly d)
{
    var diff = ((int)d.DayOfWeek + 6) % 7;
    return d.AddDays(-diff);
}

record WeeklyVolume(DateOnly WeekStart, int TotalSets, double TotalVolumeKg);

// Database entities
class Workout
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public List<ExerciseSet> Sets { get; set; } = new();
}

class ExerciseSet
{
    public int Id { get; set; }
    public int WorkoutId { get; set; }
    public Workout? Workout { get; set; }
    public string Exercise { get; set; } = "";
    public int Reps { get; set; }
    public double WeightKg { get; set; }
}

// Request and response shapes (DTOs)
record NewSet(string? Exercise, int Reps, double WeightKg);
record NewWorkout(DateOnly? Date, string? Notes, List<NewSet>? Sets);
record SetDto(int Id, string Exercise, int Reps, double WeightKg);
record WorkoutDto(int Id, DateOnly Date, string? Notes, List<SetDto> Sets);
record ExerciseRecord(string Exercise, double MaxWeightKg, int RepsAtMax, DateOnly Date, int TotalSets);

class WorkoutDb : DbContext
{
    public WorkoutDb(DbContextOptions<WorkoutDb> options) : base(options) { }
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
}

public partial class Program { }