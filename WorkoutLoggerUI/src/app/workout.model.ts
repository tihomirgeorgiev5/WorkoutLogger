export interface WorkoutSet {
  id?: number;
  exercise: string;
  reps: number;
  weightKg: number;
}

export interface Workout {
  id?: number;
  date: string;
  notes?: string | null;
  sets: WorkoutSet[];
}

export interface ExerciseRecord {
  exercise: string;
  maxWeightKg: number;
  repsAtMax: number;
  date: string;
  totalSets: number;
}

export interface WeeklyVolume {
  weekStart: string;
  totalSets: number;
  totalVolumeKg: number;
}