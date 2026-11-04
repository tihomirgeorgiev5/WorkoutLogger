# Workout Logger

A full-stack workout diary. Log workouts as sessions made of sets, see your
personal records and your weekly training volume.

## Features

- Create, view, update and delete workouts (a workout has many sets)
- Input validation with clear error messages
- Personal record per exercise (heaviest set)
- Weekly volume (reps x weight, weeks start on Monday)
- Filter the workout list by date range
- Swagger UI for exploring the API
- Automated API tests

## Tech stack

- **Backend:** ASP.NET Core minimal API (.NET 9), Entity Framework Core, SQLite, migrations
- **Frontend:** Angular 20
- **Tests:** xUnit with an in-memory SQLite database

## Project structure

| Folder | Description |
|---|---|
| `WorkoutLogger` | ASP.NET Core API |
| `WorkoutLogger.Tests` | xUnit API tests |
| `WorkoutLoggerUI` | Angular frontend |

## Run locally

### API

```bash
cd WorkoutLogger
dotnet run
```

The console shows the address (for example `http://localhost:5194`).
Swagger UI is available at `/swagger`. The SQLite database is created
automatically on the first start.

### Frontend

```bash
cd WorkoutLoggerUI
npm install
ng serve
```

Open `http://localhost:4200`. The API must be running.

### Tests

```bash
dotnet test
```

## API

| Method | Endpoint | Description |
|---|---|---|
| GET | `/workouts` | List workouts (optional `?from=` and `?to=`, format `yyyy-MM-dd`) |
| GET | `/workouts/{id}` | Get one workout |
| POST | `/workouts` | Create a workout |
| PUT | `/workouts/{id}` | Replace a workout |
| DELETE | `/workouts/{id}` | Delete a workout |
| GET | `/workouts/records` | Personal records |
| GET | `/workouts/volume/weekly` | Weekly volume |

Example request body:

```json
{
  "notes": "Chest and legs day",
  "sets": [
    { "exercise": "Bench press", "reps": 10, "weightKg": 60 },
    { "exercise": "Squat", "reps": 8, "weightKg": 80 }
  ]
}
```

## Notes

This is a learning project. The API has no authentication.
