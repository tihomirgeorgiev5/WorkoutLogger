using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Starts the API in memory and swaps the file database for an in-memory SQLite one
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<WorkoutDb>));
            services.Remove(descriptor);
            services.AddDbContext<WorkoutDb>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

// Shapes used to build requests and read responses
record SetIn(string Exercise, int Reps, double WeightKg);
record WorkoutIn(string? Notes, List<SetIn> Sets);
record SetOut(int Id, string Exercise, int Reps, double WeightKg);
record WorkoutOut(int Id, DateOnly Date, string? Notes, List<SetOut> Sets);
record RecordOut(string Exercise, double MaxWeightKg, int RepsAtMax, DateOnly Date, int TotalSets);

public class WorkoutApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public WorkoutApiTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task<WorkoutOut> CreateAsync(WorkoutIn workout)
    {
        var response = await _client.PostAsJsonAsync("/workouts", workout);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkoutOut>())!;
    }

    [Fact]
    public async Task Post_ValidWorkout_ReturnsCreatedWithSets()
    {
        var created = await CreateAsync(new WorkoutIn("Test", [new SetIn("Squat", 5, 100)]));

        Assert.True(created.Id > 0);
        Assert.Single(created.Sets);
        Assert.Equal("Squat", created.Sets[0].Exercise);
    }

    [Fact]
    public async Task Post_ZeroReps_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/workouts",
            new WorkoutIn(null, [new SetIn("Squat", 0, 100)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("sets[0].reps", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_NoSets_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/workouts", new WorkoutIn(null, []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_FutureDate_ReturnsBadRequest()
    {
        var body = new { date = "2099-01-01", sets = new[] { new SetIn("Squat", 5, 100) } };

        var response = await _client.PostAsJsonAsync("/workouts", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/workouts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ReplacesSetsAndKeepsDate()
    {
        var created = await CreateAsync(new WorkoutIn("Before",
            [new SetIn("Bench press", 10, 60), new SetIn("Squat", 8, 80)]));

        var response = await _client.PutAsJsonAsync($"/workouts/{created.Id}",
            new WorkoutIn("After", [new SetIn("Deadlift", 3, 140)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<WorkoutOut>())!;
        Assert.Equal("After", updated.Notes);
        Assert.Equal(created.Date, updated.Date);
        Assert.Single(updated.Sets);
        Assert.Equal("Deadlift", updated.Sets[0].Exercise);
    }

    [Fact]
    public async Task Put_InvalidData_ReturnsBadRequestAndKeepsWorkout()
    {
        var created = await CreateAsync(new WorkoutIn("Keep me", [new SetIn("Squat", 5, 100)]));

        var response = await _client.PutAsJsonAsync($"/workouts/{created.Id}",
            new WorkoutIn("Broken", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await _client.GetFromJsonAsync<WorkoutOut>($"/workouts/{created.Id}");
        Assert.Equal("Keep me", current!.Notes);
    }

    [Fact]
    public async Task Delete_RemovesWorkout()
    {
        var created = await CreateAsync(new WorkoutIn("Temp", [new SetIn("Squat", 5, 100)]));

        var delete = await _client.DeleteAsync($"/workouts/{created.Id}");
        var get = await _client.GetAsync($"/workouts/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Records_ReturnsHeaviestSetPerExercise()
    {
        // Unique name so other tests sharing the database do not interfere
        var name = "test-" + Guid.NewGuid().ToString("N");
        await CreateAsync(new WorkoutIn(null, [new SetIn(name, 10, 50), new SetIn(name, 5, 60)]));

        var records = await _client.GetFromJsonAsync<List<RecordOut>>("/workouts/records");

        var record = Assert.Single(records!, r => r.Exercise == name);
        Assert.Equal(60, record.MaxWeightKg);
        Assert.Equal(5, record.RepsAtMax);
        Assert.Equal(2, record.TotalSets);
    }
}