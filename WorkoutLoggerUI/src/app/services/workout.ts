import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Workout, ExerciseRecord, WeeklyVolume } from '../workout.model';

@Injectable({ providedIn: 'root' })
export class WorkoutService {
  private apiUrl = 'http://localhost:5194/workouts';

  constructor(private http: HttpClient) {}

  getWorkouts(): Observable<Workout[]> {
    return this.http.get<Workout[]>(this.apiUrl);
  }

  addWorkout(workout: Workout): Observable<Workout> {
    return this.http.post<Workout>(this.apiUrl, workout);
  }

  deleteWorkout(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getRecords(): Observable<ExerciseRecord[]> {
    return this.http.get<ExerciseRecord[]>(`${this.apiUrl}/records`);
  }

  getWeeklyVolume(): Observable<WeeklyVolume[]> {
    return this.http.get<WeeklyVolume[]>(`${this.apiUrl}/volume/weekly`);
  }
}