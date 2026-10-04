using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkoutDb>(options =>
    options.UseSqlite("Data Source=workouts.db"));

builder.Services.AddDbContext<WorkoutDb>(options =>
    options.UseSqlite("Data Source=workouts.db"));

// Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI is available only while developing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
    // Reject invalid input with a 400 response listing every problem
    var errors = Validate(input);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var workout = new Workout
    {
        Date = input.Date ?? DateOnly.FromDateTime(DateTime.Today),
        Exercise = input.Exercise!.Trim(),
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

// Collects validation errors as field name -> list of messages
static Dictionary<string, string[]> Validate(NewWorkout input)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(input.Exercise))
        errors["exercise"] = ["Exercise is required."];
    else if (input.Exercise.Trim().Length > 100)
        errors["exercise"] = ["Exercise must be 100 characters or fewer."];

    if (input.Sets < 1 || input.Sets > 100)
        errors["sets"] = ["Sets must be between 1 and 100."];

    if (input.Reps < 1 || input.Reps > 1000)
        errors["reps"] = ["Reps must be between 1 and 1000."];

    if (input.WeightKg < 0 || input.WeightKg > 1000)
        errors["weightKg"] = ["Weight must be between 0 and 1000 kg."];

    if (input.Date is { } date && date > DateOnly.FromDateTime(DateTime.Today))
        errors["date"] = ["Date cannot be in the future."];

    return errors;
}

class Workout
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Exercise { get; set; } = "";
    public int Sets { get; set; }
    public int Reps { get; set; }
    public double WeightKg { get; set; }
}

record NewWorkout(string? Exercise, int Sets, int Reps, double WeightKg, DateOnly? Date);

class WorkoutDb : DbContext
{
    public WorkoutDb(DbContextOptions<WorkoutDb> options) : base(options) { }
    public DbSet<Workout> Workouts => Set<Workout>();
}