import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { TASK_CATEGORY_OPTIONS, TaskCategory, TaskListState, TaskPriority, TaskProblemDetails, TaskResponse, isTaskCategory, toTaskCategoryLabel } from '../../shared/models/task.models';
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

  readonly categoryOptions = TASK_CATEGORY_OPTIONS;

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
  toggleErrors: Record<string, string> = {};
  private readonly completionToggleInFlight = new Set<string>();

  readonly editForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.maxLength(2000)]],
    dueAtUtc: [''],
    priority: ['medium' as TaskPriority, [Validators.required]],
    category: ['work' as TaskCategory, [Validators.required]]
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
      category: this.toEditableCategory(task.category)
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
      category: 'work'
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
      category: rawValue.category
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

  toggleError(taskId: string): string {
    return this.toggleErrors[taskId] ?? '';
  }

  isToggleInFlight(taskId: string): boolean {
    return this.completionToggleInFlight.has(taskId);
  }

  toggleCompletion(task: TaskResponse, isCompleted: boolean): void {
    if (this.completionToggleInFlight.has(task.id)) {
      return;
    }

    delete this.toggleErrors[task.id];
    this.completionToggleInFlight.add(task.id);

    this.taskService
      .toggleTaskCompletion(task.id, { isCompleted }, this.newIdempotencyKey())
      .pipe(finalize(() => { this.completionToggleInFlight.delete(task.id); }))
      .subscribe({
        next: (updatedTask) => {
          this.reconcileTaskAfterCompletionToggle(task, updatedTask);
          this.liveMessage = updatedTask.isCompleted
            ? `Task ${updatedTask.title} marked completed.`
            : `Task ${updatedTask.title} marked active.`;
        },
        error: (error: TaskProblemDetails) => {
          this.toggleErrors[task.id] = error.title ?? error.detail ?? 'Task completion update failed.';
          this.liveMessage = this.toggleErrors[task.id];
        }
      });
  }

  toCategoryLabel(category: string): string {
    return toTaskCategoryLabel(category);
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

  private reconcileTaskAfterCompletionToggle(previousTask: TaskResponse, updatedTask: TaskResponse): void {
    if (!previousTask.isCompleted && updatedTask.isCompleted) {
      this.activeCount = Math.max(0, this.activeCount - 1);
      this.completedCount += 1;
    }

    if (previousTask.isCompleted && !updatedTask.isCompleted) {
      this.completedCount = Math.max(0, this.completedCount - 1);
      this.activeCount += 1;
    }

    if (this.selectedFilter === 'active' && updatedTask.isCompleted) {
      this.tasks = this.tasks.filter((task) => task.id !== updatedTask.id);
      return;
    }

    if (this.selectedFilter === 'completed' && !updatedTask.isCompleted) {
      this.tasks = this.tasks.filter((task) => task.id !== updatedTask.id);
      return;
    }

    this.tasks = this.tasks.map((task) => task.id === updatedTask.id ? updatedTask : task);
  }

  private newIdempotencyKey(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
      const bytes = new Uint8Array(16);
      crypto.getRandomValues(bytes);

      bytes[6] = (bytes[6] & 0x0f) | 0x40;
      bytes[8] = (bytes[8] & 0x3f) | 0x80;

      const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
      return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    }

    return `${this.randomHex(8)}-${this.randomHex(4)}-4${this.randomHex(3)}-${this.variantHex()}${this.randomHex(3)}-${this.randomHex(12)}`;
  }

  private toEditableCategory(category: string): TaskCategory {
    if (isTaskCategory(category)) {
      return category;
    }

    return 'other';
  }

  private randomHex(length: number): string {
    let buffer = '';
    while (buffer.length < length) {
      buffer += Math.floor(Math.random() * 16).toString(16);
    }

    return buffer.slice(0, length);
  }

  private variantHex(): string {
    return ['8', '9', 'a', 'b'][Math.floor(Math.random() * 4)];
  }
}
