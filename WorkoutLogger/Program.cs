var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var workouts = new List<Workout>();
var nextId = 1;

var group = app.MapGroup("/workouts");

group.MapGet("/", () => workouts);

group.MapGet("/{id}", (int id) =>
    workouts.FirstOrDefault(w => w.Id == id) is { } w
        ? Results.Ok(w)
        : Results.NotFound());

group.MapPost("/", (NewWorkout input) =>
{
    var workout = new Workout(
        nextId++,
        input.Date ?? DateOnly.FromDateTime(DateTime.Today),
        input.Exercise, input.Sets, input.Reps, input.WeightKg);

    workouts.Add(workout);
    return Results.Created($"/workouts/{workout.Id}", workout);
});

group.MapDelete("/{id}", (int id) =>
    workouts.RemoveAll(w => w.Id == id) > 0
        ? Results.NoContent()
        : Results.NotFound());

app.Run();

record Workout(int Id, DateOnly Date, string Exercise, int Sets, int Reps, double WeightKg);
record NewWorkout(string Exercise, int Sets, int Reps, double WeightKg, DateOnly? Date);
