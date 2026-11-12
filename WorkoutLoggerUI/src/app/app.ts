import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { WorkoutService } from './services/workout';
import { Workout, ExerciseRecord, WeeklyVolume } from './workout.model';

type Theme = 'light' | 'dark';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
  host: { '(document:keydown.escape)': 'cancelDelete()' }
})

export class App implements OnInit {
  private workoutService = inject(WorkoutService);
  private wakeTimer?: ReturnType<typeof setTimeout>;

  workouts = signal<Workout[]>([]);
  records = signal<ExerciseRecord[]>([]);
  weekly = signal<WeeklyVolume[]>([]);
  error = signal('');
  theme = signal<Theme>(this.initialTheme());
  pendingDelete = signal<Workout | null>(null);

  loading = signal(false);
  waking = signal(false);
  loadFailed = signal(false);
  private loaded = false;

  maxVolume = computed(() => Math.max(1, ...this.weekly().map(w => w.totalVolumeKg)));

  newWorkout: Workout = this.emptyWorkout();

  constructor() {
    effect(() => {
      const t = this.theme();
      document.documentElement.dataset['theme'] = t;
      try { localStorage.setItem('theme', t); } catch { /* ignore */ }
    });
  }

  ngOnInit(): void {
    this.loadData();
  }

  private initialTheme(): Theme {
    try {
      const saved = localStorage.getItem('theme');
      if (saved === 'light' || saved === 'dark') return saved;
    } catch { /* ignore */ }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  toggleTheme(): void {
    this.theme.update(t => (t === 'dark' ? 'light' : 'dark'));
  }

  private emptyWorkout(): Workout {
    return {
      date: new Date().toLocaleDateString('sv-SE'), // yyyy-MM-dd in local time
      notes: '',
      sets: [{ exercise: '', reps: 10, weightKg: 0 }]
    };
  }

  loadData(): void {
    // The loading screen is shown only until the first successful load,
    // so saving or deleting a workout does not make the page flicker.
    const showLoader = !this.loaded;
    if (showLoader) {
      this.loading.set(true);
      this.loadFailed.set(false);
      this.wakeTimer = setTimeout(() => this.waking.set(true), 3000);
    }

    forkJoin({
      workouts: this.workoutService.getWorkouts(),
      records: this.workoutService.getRecords(),
      weekly: this.workoutService.getWeeklyVolume()
    }).subscribe({
      next: ({ workouts, records, weekly }) => {
        this.workouts.set(workouts);
        this.records.set(records);
        this.weekly.set(weekly);
        this.loaded = true;
        this.loadFailed.set(false);
        this.finishLoading();
      },
      error: () => {
        this.loadFailed.set(true);
        this.finishLoading();
      }
    });
  }

  private finishLoading(): void {
    clearTimeout(this.wakeTimer);
    this.loading.set(false);
    this.waking.set(false);
  }

  addSet(): void {
    this.newWorkout.sets.push({ exercise: '', reps: 10, weightKg: 0 });
  }

  removeSet(index: number): void {
    this.newWorkout.sets.splice(index, 1);
  }

  saveWorkout(): void {
    this.error.set('');
    this.workoutService.addWorkout(this.newWorkout).subscribe({
      next: () => {
        this.newWorkout = this.emptyWorkout();
        this.loadData();
      },
      error: err => {
        const errors = err.error?.errors as Record<string, string[]> | undefined;
        this.error.set(errors ? Object.values(errors).flat().join(' ') : 'Error while saving.');
      }
    });
  }

    askDelete(workout: Workout): void {
    this.pendingDelete.set(workout);
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const id = this.pendingDelete()?.id;
    this.pendingDelete.set(null);
    if (id == null) return;
    this.workoutService.deleteWorkout(id).subscribe(() => this.loadData());
  }
}