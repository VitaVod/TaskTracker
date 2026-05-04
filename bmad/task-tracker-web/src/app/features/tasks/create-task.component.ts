import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { CreateTaskFormValue } from './create-task-form.model';
import {
  TASK_CATEGORY_OPTIONS,
  TaskCategory,
  TaskDifficulty,
  TaskEnergyLevel,
  TaskProblemDetails,
  TaskPriority
} from '../../shared/models/task.models';
import { TaskService } from '../../shared/services/task.service';

@Component({
  selector: 'app-create-task',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './create-task.component.html',
  styleUrl: './create-task.component.scss'
})
export class CreateTaskComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly taskService = inject(TaskService);
  private readonly router = inject(Router);
  private readonly recentContextTagsStorageKey = 'tasktracker.recentContextTags';

  readonly categoryOptions = TASK_CATEGORY_OPTIONS;
  readonly minDueDateTimeLocal = buildTodayMinDateTimeLocal();
  recentContextTags: string[] = this.loadRecentContextTags();

  readonly form = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.maxLength(2000)]],
    dueAtUtc: ['', [notPastDueDateValidator()]],
    priority: ['medium' as TaskPriority, [Validators.required]],
    category: ['work' as TaskCategory, [Validators.required]],
    difficulty: ['easy' as TaskDifficulty, [Validators.required]],
    energyLevel: ['medium' as TaskEnergyLevel, [Validators.required]],
    contextTag: ['', [Validators.maxLength(64)]],
    effortPoints: [50 as number | null, [Validators.min(1), Validators.max(100)]]
  });

  isSubmitting = false;
  successMessage = '';
  errorMessage = '';
  fieldErrors: Record<string, string[]> = {};

  async submit(): Promise<void> {
    this.successMessage = '';
    this.errorMessage = '';
    this.fieldErrors = {};

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const rawValue = this.form.getRawValue();
    const payload = this.mapFormToRequest(rawValue);

    this.isSubmitting = true;
    this.taskService
      .createTask(payload)
      .pipe(finalize(() => { this.isSubmitting = false; }))
      .subscribe({
        next: async () => {
          this.persistRecentContextTag(payload.contextTag);
          this.successMessage = 'Task created successfully.';
          await this.router.navigate(['/dashboard']);
        },
        error: (error: TaskProblemDetails) => {
          if (error.errors) {
            this.fieldErrors = error.errors;
          }

          this.errorMessage = error.title ?? error.detail ?? 'Task creation failed.';
        }
      });
  }

  fieldError(fieldName: keyof CreateTaskFormValue): string {
    return this.fieldErrors[fieldName]?.[0] ?? '';
  }

  closeDatePicker(event: Event): void {
    if (event.type !== 'change') {
      return;
    }

    const target = event.target;
    if (target instanceof HTMLInputElement && target.value !== '') {
      queueMicrotask(() => target.blur());
    }
  }

  private mapFormToRequest(value: CreateTaskFormValue & { dueAtUtc: string }): {
    title: string;
    description: string;
    dueAtUtc: string | null;
    priority: TaskPriority;
    category: TaskCategory;
    difficulty: TaskDifficulty;
    energyLevel: TaskEnergyLevel;
    contextTag: string | null;
    effortPoints: number | null;
  } {
    const dueAtUtc = value.dueAtUtc.trim() === '' ? null : new Date(value.dueAtUtc).toISOString();
    const contextTag = value.contextTag.trim() === '' ? null : value.contextTag.trim().toLowerCase();

    return {
      title: value.title.trim(),
      description: value.description.trim(),
      dueAtUtc,
      priority: value.priority,
      category: value.category,
      difficulty: value.difficulty,
      energyLevel: value.energyLevel,
      contextTag,
      effortPoints: value.effortPoints
    };
  }

  private loadRecentContextTags(): string[] {
    if (typeof window === 'undefined') {
      return [];
    }

    const stored = window.localStorage.getItem(this.recentContextTagsStorageKey);
    if (!stored) {
      return [];
    }

    try {
      const parsed = JSON.parse(stored) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed
        .filter((value): value is string => typeof value === 'string')
        .map((value) => value.trim().toLowerCase())
        .filter((value) => value.length > 0)
        .slice(0, 8);
    } catch {
      return [];
    }
  }

  private persistRecentContextTag(contextTag: string | null): void {
    if (!contextTag || typeof window === 'undefined') {
      return;
    }

    const normalized = contextTag.trim().toLowerCase();
    if (!normalized) {
      return;
    }

    const nextTags = [normalized, ...this.recentContextTags.filter((tag) => tag !== normalized)].slice(0, 8);
    this.recentContextTags = nextTags;
    window.localStorage.setItem(this.recentContextTagsStorageKey, JSON.stringify(nextTags));
  }
}

function notPastDueDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string;
    if (!value || value.trim() === '') {
      return null;
    }

    const selectedDateTime = new Date(value);
    if (Number.isNaN(selectedDateTime.getTime())) {
      return { invalidDate: true };
    }

    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    return selectedDateTime < startOfToday ? { pastDueDate: true } : null;
  };
}

function buildTodayMinDateTimeLocal(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = `${now.getMonth() + 1}`.padStart(2, '0');
  const day = `${now.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}T00:00`;
}