import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { TaskListState, TaskPriority, TaskProblemDetails, TaskResponse } from '../../shared/models/task.models';
import { TaskService } from '../../shared/services/task.service';

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.scss'
})
export class TaskListComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly formBuilder = inject(FormBuilder);

  readonly filterOptions: ReadonlyArray<{ value: TaskListState; label: string; ariaLabel: string }> = [
    { value: 'all', label: 'All tasks', ariaLabel: 'Show all tasks' },
    { value: 'active', label: 'Active tasks', ariaLabel: 'Show active tasks' },
    { value: 'completed', label: 'Completed tasks', ariaLabel: 'Show completed tasks' }
  ];

  selectedFilter: TaskListState = 'all';
  tasks: TaskResponse[] = [];
  editingTaskId: string | null = null;
  activeCount = 0;
  completedCount = 0;
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  saveErrorMessage = '';
  liveMessage = '';
  saveFieldErrors: Record<string, string[]> = {};

  readonly editForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.maxLength(2000)]],
    dueAtUtc: [''],
    priority: ['medium' as TaskPriority, [Validators.required]],
    category: ['', [Validators.required, Validators.maxLength(64)]]
  });

  ngOnInit(): void {
    this.loadTasks(false);
  }

  setFilter(filter: TaskListState): void {
    if (filter === this.selectedFilter) {
      return;
    }

    this.selectedFilter = filter;
    this.loadTasks(true);
  }

  setFilterFromKeyboard(event: Event, filter: TaskListState): void {
    event.preventDefault();
    this.setFilter(filter);
  }

  trackByTaskId(_index: number, task: TaskResponse): string {
    return task.id;
  }

  isEditing(task: TaskResponse): boolean {
    return this.editingTaskId === task.id;
  }

  startEdit(task: TaskResponse): void {
    this.editingTaskId = task.id;
    this.saveErrorMessage = '';
    this.saveFieldErrors = {};
    this.editForm.reset({
      title: task.title,
      description: task.description,
      dueAtUtc: this.toDateTimeLocal(task.dueAtUtc),
      priority: task.priority,
      category: task.category
    });
  }

  cancelEdit(): void {
    this.editingTaskId = null;
    this.saveErrorMessage = '';
    this.saveFieldErrors = {};
    this.editForm.reset({
      title: '',
      description: '',
      dueAtUtc: '',
      priority: 'medium',
      category: ''
    });
  }

  submitEdit(): void {
    if (this.editingTaskId === null) {
      return;
    }

    this.saveErrorMessage = '';
    this.saveFieldErrors = {};

    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    const rawValue = this.editForm.getRawValue();
    const payload = {
      title: rawValue.title.trim(),
      description: rawValue.description.trim(),
      dueAtUtc: rawValue.dueAtUtc.trim() === '' ? null : new Date(rawValue.dueAtUtc).toISOString(),
      priority: rawValue.priority,
      category: rawValue.category.trim()
    };

    this.isSaving = true;
    this.taskService
      .updateTask(this.editingTaskId, payload)
      .pipe(finalize(() => { this.isSaving = false; }))
      .subscribe({
        next: (updatedTask) => {
          this.tasks = this.tasks.map((task) => task.id === updatedTask.id ? updatedTask : task);
          this.liveMessage = 'Task updated successfully.';
          this.cancelEdit();
        },
        error: (error: TaskProblemDetails) => {
          if (error.errors) {
            this.saveFieldErrors = error.errors;
          }

          this.saveErrorMessage = error.title ?? error.detail ?? 'Task update failed.';
        }
      });
  }

  fieldError(fieldName: 'title' | 'description' | 'dueAtUtc' | 'priority' | 'category'): string {
    return this.saveFieldErrors[fieldName]?.[0] ?? '';
  }

  get activeTasks(): TaskResponse[] {
    return this.tasks.filter((task) => !task.isCompleted);
  }

  get completedTasks(): TaskResponse[] {
    return this.tasks.filter((task) => task.isCompleted);
  }

  private toDateTimeLocal(value: string | null): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    const hours = `${date.getHours()}`.padStart(2, '0');
    const minutes = `${date.getMinutes()}`.padStart(2, '0');

    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  private loadTasks(shouldAnnounce: boolean): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.taskService.getTasks(this.selectedFilter).subscribe({
      next: (response) => {
        this.tasks = response.items;
        this.activeCount = response.summary.activeCount;
        this.completedCount = response.summary.completedCount;
        this.isLoading = false;

        if (shouldAnnounce) {
          this.liveMessage = this.buildResultAnnouncement();
        }
      },
      error: (error: { title?: string; detail?: string }) => {
        this.errorMessage = error.title ?? error.detail ?? 'Unable to load tasks right now.';
        this.isLoading = false;

        if (shouldAnnounce) {
          this.liveMessage = this.errorMessage;
        }
      }
    });
  }

  private buildResultAnnouncement(): string {
    if (this.selectedFilter === 'active') {
      return `Showing ${this.tasks.length} active tasks.`;
    }

    if (this.selectedFilter === 'completed') {
      return `Showing ${this.tasks.length} completed tasks.`;
    }

    return `Showing ${this.activeCount} active and ${this.completedCount} completed tasks.`;
  }
}
