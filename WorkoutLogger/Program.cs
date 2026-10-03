using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the database (SQLite file named workouts.db)
builder.Services.AddDbContext<WorkoutDb>(options =>
    options.UseSqlite("Data Source=workouts.db"));

var app = builder.Build();

// Create the database file and tables if they do not exist yet
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WorkoutDb>().Database.EnsureCreated();
}

var group = app.MapGroup("/workouts");

group.MapGet("/", async (WorkoutDb db) => await db.Workouts.ToListAsync());

group.MapGet("/{id}", async (int id, WorkoutDb db) =>
    await db.Workouts.FindAsync(id) is { } workout
        ? Results.Ok(workout)
        : Results.NotFound());

group.MapPost("/", async (NewWorkout input, WorkoutDb db) =>
{
    var workout = new Workout
    {
        Date = input.Date ?? DateOnly.FromDateTime(DateTime.Today),
        Exercise = input.Exercise,
        Sets = input.Sets,
        Reps = input.Reps,
        WeightKg = input.WeightKg
    };

    db.Workouts.Add(workout);
    await db.SaveChangesAsync();
    return Results.Created($"/workouts/{workout.Id}", workout);
});

group.MapDelete("/{id}", async (int id, WorkoutDb db) =>
{
    var workout = await db.Workouts.FindAsync(id);
    if (workout is null) return Results.NotFound();

    db.Workouts.Remove(workout);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

class Workout
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Exercise { get; set; } = "";
    public int Sets { get; set; }
    public int Reps { get; set; }
    public double WeightKg { get; set; }
}

record NewWorkout(string Exercise, int Sets, int Reps, double WeightKg, DateOnly? Date);

class WorkoutDb : DbContext
{
    public WorkoutDb(DbContextOptions<WorkoutDb> options) : base(options) { }
    public DbSet<Workout> Workouts => Set<Workout>();
}