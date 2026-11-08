# Workout Logger

A full-stack workout diary. Log workouts as sessions made of sets, see your
personal records and your weekly training volume.

**Live demo:** https://blue-field-047855810.4.azurestaticapps.net
**API:** https://workoutlogger-api-tihomir-hbf7esh6f6bmhjde.westeurope-01.azurewebsites.net/workouts

> Hosted on Azure free tiers: the first request after a period of inactivity
> can be slower.

## Features

- Create, view, update and delete workouts (a workout has many sets)
- Input validation with clear error messages
- Personal record per exercise (heaviest set)
- Weekly volume (reps x weight, weeks start on Monday)
- Filter the workout list by date range
- Swagger UI for exploring the API (development only)
- Automated API tests (xUnit)
- Deployed to Azure with automatic frontend deployment

## Tech stack

- **Backend:** ASP.NET Core minimal API (.NET 9), Entity Framework Core, SQLite, migrations
- **Frontend:** Angular 20
- **Tests:** xUnit with an in-memory SQLite database
- **Hosting:** Azure Static Web Apps (frontend), Azure App Service (API)
- **CI/CD:** GitHub Actions (frontend)

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

### Validation

Invalid requests return `400 Bad Request` with the problems listed per field.

| Field | Rule |
|---|---|
| `date` | Optional, cannot be in the future |
| `notes` | Up to 500 characters |
| `sets` | 1 to 50 sets |
| `exercise` | Required, up to 100 characters |
| `reps` | 1 to 1000 |
| `weightKg` | 0 to 1000 (0 is allowed for bodyweight exercises) |

## Deployment

| Part | Azure service | How it is deployed |
|---|---|---|
| Angular frontend | Static Web Apps (Free) | GitHub Actions on every push to `master` |
| .NET API | App Service (Free F1, Windows) | Publish from Visual Studio |
| Database | SQLite file in `D:\home\data` | Created by migrations on startup |

### Configuration (App settings)

| Name | Example value | Purpose |
|---|---|---|
| `AllowedOrigins__0` | `https://blue-field-047855810.4.azurestaticapps.net` | Origin allowed by CORS |
| `ConnectionStrings__Default` | `Data Source=D:\home\data\workouts.db` | SQLite database path |

Locally, the defaults are used: CORS allows `http://localhost:4200` and the
database is `workouts.db` next to the app.

The frontend reads the API address from `apiUrl` in the Angular
`environment.ts` file.

### Limitations of the free tier

- The SQLite file survives restarts and new deployments, but Free F1 gives no
  durability guarantee. Do not store important data.
- The app can start slowly after being idle.

## Roadmap

- Charts and progress per exercise
- Edit a workout from the UI
- Upgrade to .NET 10
- Move the database to Azure SQL
