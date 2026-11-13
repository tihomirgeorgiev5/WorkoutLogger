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

  // Progress chart
  readonly chartW = 480;
  readonly chartH = 240;
  private readonly pad = { l: 44, r: 16, t: 16, b: 32 };

  selectedExercise = signal('');

  exercises = computed(() => {
    const map = new Map<string, { name: string; dates: Set<string> }>();
    for (const w of this.workouts()) {
      for (const s of w.sets) {
        const name = s.exercise.trim();
        if (!name) continue;
        const key = name.toLowerCase();
        const entry = map.get(key) ?? { name, dates: new Set<string>() };
        entry.dates.add(w.date ?? '');
        map.set(key, entry);
      }
    }
    return [...map.values()]
      .map(e => ({ name: e.name, sessions: e.dates.size }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  // The chosen exercise, or the one with the most sessions by default
  activeExercise = computed(() => {
    const list = this.exercises();
    const sel = this.selectedExercise().toLowerCase();
    const found = list.find(e => e.name.toLowerCase() === sel);
    if (found) return found.name;
    const best = [...list].sort((a, b) => b.sessions - a.sessions)[0];
    return best?.name ?? '';
  });

  chart = computed(() => {
    const name = this.activeExercise().toLowerCase();
    const bestByDate = new Map<string, number>();
    for (const w of this.workouts()) {
      for (const s of w.sets) {
        if (s.exercise.trim().toLowerCase() !== name) continue;
        const d = w.date ?? '';
        bestByDate.set(d, Math.max(bestByDate.get(d) ?? 0, s.weightKg));
      }
    }
    const rows = [...bestByDate.entries()].sort(([a], [b]) => a.localeCompare(b));
    const n = rows.length;
    if (n === 0) return null;

    const { l, r, t, b } = this.pad;
    const innerW = this.chartW - l - r;
    const innerH = this.chartH - t - b;
    const values = rows.map(([, v]) => v);

    let min = Math.min(...values);
    let max = Math.max(...values);
    if (min === max) { min = Math.max(0, min - 5); max = max + 5; }
    const span = max - min;
    min = Math.max(0, min - span * 0.1);
    max = max + span * 0.1;

    const xAt = (i: number) => (n === 1 ? l + innerW / 2 : l + (i / (n - 1)) * innerW);
    const yAt = (v: number) => t + innerH - ((v - min) / (max - min)) * innerH;

    // Show about 5 date labels, always keeping them from overlapping
    const step = Math.ceil(n / 5);
    const points = rows.map(([date, weight], i) => ({
      date,
      weight,
      x: xAt(i),
      y: yAt(weight),
      label: `${date.slice(8, 10)}.${date.slice(5, 7)}`,
      showLabel: i % step === 0 || (i === n - 1 && (n - 1) % step >= Math.ceil(step / 2))
    }));

    const line = points.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
    const baseY = t + innerH;
    const area = n > 1
      ? `${line} L${points[n - 1].x.toFixed(1)},${baseY} L${points[0].x.toFixed(1)},${baseY} Z`
      : '';
    const ticks = Array.from({ length: 4 }, (_, i) => {
      const v = min + ((max - min) * i) / 3;
      return { y: yAt(v), label: Math.round(v).toString() };
    });

    return {
      points, line, area, ticks,
      plotLeft: l,
      plotRight: this.chartW - r,
      best: Math.max(...values),
      latest: values[n - 1],
      change: Math.round((values[n - 1] - values[0]) * 10) / 10,
      sessions: n
    };
  });

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