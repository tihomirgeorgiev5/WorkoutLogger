import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkoutService } from './services/workout';
import { Workout, ExerciseRecord, WeeklyVolume } from './workout.model';

type Theme = 'light' | 'dark';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private workoutService = inject(WorkoutService);

  workouts = signal<Workout[]>([]);
  records = signal<ExerciseRecord[]>([]);
  weekly = signal<WeeklyVolume[]>([]);
  error = signal('');
  theme = signal<Theme>(this.initialTheme());

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
    this.workoutService.getWorkouts().subscribe(d => this.workouts.set(d));
    this.workoutService.getRecords().subscribe(d => this.records.set(d));
    this.workoutService.getWeeklyVolume().subscribe(d => this.weekly.set(d));
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

  deleteWorkout(id: number): void {
    this.workoutService.deleteWorkout(id).subscribe(() => this.loadData());
  }
}