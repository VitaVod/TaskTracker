import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import {
  TASK_CATEGORY_OPTIONS,
  TaskCategory,
  TaskListState,
  TaskPriority,
  TaskProblemDetails,
  TaskResponse,
  TaskUiState,
  isTaskCategory,
  toTaskCategoryLabel
} from '../../shared/models/task.models';
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
  readonly minDueDateTimeLocal = buildTodayMinDateTimeLocal();

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
  uiState: TaskUiState = { kind: 'loading' };
  isSaving = false;
  saveErrorMessage = '';
  deleteErrorMessage = '';
  saveErrorCode?: string;
  saveErrorTraceId?: string;
  deleteErrorCode?: string;
  deleteErrorTraceId?: string;
  liveMessage = '';
  saveFieldErrors: Record<string, string[]> = {};
  toggleErrors: Record<string, string> = {};
  toggleErrorCodes: Record<string, string | undefined> = {};
  toggleErrorTraceIds: Record<string, string | undefined> = {};
  pendingDeleteTask: TaskResponse | null = null;
  isDeleting = false;
  private readonly completionToggleInFlight = new Set<string>();
  private readonly toggleTargetByTaskId: Record<string, boolean> = {};
  private deleteInFlightTaskId: string | null = null;
  private deleteReturnFocusElement: HTMLElement | null = null;
  private latestLoadRequestId = 0;

  @ViewChild('deleteConfirmButton') private deleteConfirmButton?: ElementRef<HTMLButtonElement>;

  readonly editForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.maxLength(2000)]],
    dueAtUtc: ['', [notPastDueDateValidator()]],
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

  retryLoad(): void {
    this.loadTasks(true);
  }

  resetFilterToAll(): void {
    if (this.selectedFilter === 'all') {
      return;
    }

    this.selectedFilter = 'all';
    this.loadTasks(true);
  }

  dismissSaveError(): void {
    this.saveErrorMessage = '';
    this.saveErrorCode = undefined;
    this.saveErrorTraceId = undefined;
  }

  dismissDeleteError(): void {
    this.deleteErrorMessage = '';
    this.deleteErrorCode = undefined;
    this.deleteErrorTraceId = undefined;
  }

  dismissToggleError(taskId: string): void {
    delete this.toggleErrors[taskId];
    delete this.toggleErrorCodes[taskId];
    delete this.toggleErrorTraceIds[taskId];
  }

  retryToggle(task: TaskResponse): void {
    const isCompleted = this.toggleTargetByTaskId[task.id];
    if (typeof isCompleted !== 'boolean') {
      return;
    }

    this.toggleCompletion(task, isCompleted);
  }

  isUiLoading(): boolean {
    return this.uiState.kind === 'loading';
  }

  isUiLoadError(): boolean {
    return this.uiState.kind === 'error' && this.uiState.scope === 'load';
  }

  isUiEmpty(): boolean {
    return this.uiState.kind === 'empty';
  }

  currentLoadErrorMessage(): string {
    const state = this.uiState;
    if (state.kind !== 'error' || state.scope !== 'load') {
      return '';
    }

    return state.message;
  }

  currentLoadErrorSupportText(): string {
    const state = this.uiState;
    if (state.kind !== 'error' || state.scope !== 'load') {
      return '';
    }

    return this.problemSupportText(state.code, state.traceId);
  }

  currentEmptyHeading(): string {
    if (this.selectedFilter === 'completed') {
      return 'No completed tasks yet';
    }

    if (this.selectedFilter === 'active') {
      return 'No active tasks found';
    }

    return 'No tasks yet';
  }

  currentEmptyGuidance(): string {
    if (this.selectedFilter === 'completed') {
      return 'Complete a task to move it into this view, or return to all tasks.';
    }

    if (this.selectedFilter === 'active') {
      return 'Create your next task to start building momentum.';
    }

    return 'Create your first task to get started.';
  }

  saveErrorSupportText(): string {
    return this.problemSupportText(this.saveErrorCode, this.saveErrorTraceId);
  }

  deleteErrorSupportText(): string {
    return this.problemSupportText(this.deleteErrorCode, this.deleteErrorTraceId);
  }

  toggleErrorSupportText(taskId: string): string {
    return this.problemSupportText(this.toggleErrorCodes[taskId], this.toggleErrorTraceIds[taskId]);
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
    this.saveErrorCode = undefined;
    this.saveErrorTraceId = undefined;
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
    this.saveErrorCode = undefined;
    this.saveErrorTraceId = undefined;
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
    this.saveErrorCode = undefined;
    this.saveErrorTraceId = undefined;
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

          this.saveErrorCode = error.code;
          this.saveErrorTraceId = error.traceId;
          this.saveErrorMessage = error.title ?? error.detail ?? 'Task update failed.';
          this.liveMessage = this.saveErrorMessage;
        }
      });
  }

  fieldError(fieldName: 'title' | 'description' | 'dueAtUtc' | 'priority' | 'category'): string {
    return this.saveFieldErrors[fieldName]?.[0] ?? '';
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

    this.toggleTargetByTaskId[task.id] = isCompleted;
    delete this.toggleErrors[task.id];
    delete this.toggleErrorCodes[task.id];
    delete this.toggleErrorTraceIds[task.id];
    this.completionToggleInFlight.add(task.id);

    this.taskService
      .toggleTaskCompletion(task.id, { isCompleted }, this.newIdempotencyKey())
      .pipe(finalize(() => { this.completionToggleInFlight.delete(task.id); }))
      .subscribe({
        next: (updatedTask) => {
          this.reconcileTaskAfterCompletionToggle(task, updatedTask);
          this.refreshUiStateFromTasks();
          this.liveMessage = updatedTask.isCompleted
            ? `Task ${updatedTask.title} marked completed.`
            : `Task ${updatedTask.title} marked active.`;
        },
        error: (error: TaskProblemDetails) => {
          this.toggleErrorCodes[task.id] = error.code;
          this.toggleErrorTraceIds[task.id] = error.traceId;
          this.toggleErrors[task.id] = error.title ?? error.detail ?? 'Task completion update failed.';
          this.liveMessage = this.toggleErrors[task.id];
        }
      });
  }

  requestDelete(task: TaskResponse, event: Event): void {
    if (this.isDeleting) {
      return;
    }

    this.pendingDeleteTask = task;
    this.deleteErrorMessage = '';
    this.deleteErrorCode = undefined;
    this.deleteErrorTraceId = undefined;
    this.deleteReturnFocusElement = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;

    setTimeout(() => {
      this.deleteConfirmButton?.nativeElement.focus();
    }, 0);
  }

  cancelDelete(): void {
    if (this.isDeleting) {
      return;
    }

    this.pendingDeleteTask = null;
    this.deleteErrorMessage = '';
    this.deleteErrorCode = undefined;
    this.deleteErrorTraceId = undefined;
    this.restoreDeleteTriggerFocus();
  }

  confirmDelete(): void {
    if (!this.pendingDeleteTask || this.isDeleting) {
      return;
    }

    const taskToDelete = this.pendingDeleteTask;
    this.isDeleting = true;
    this.deleteInFlightTaskId = taskToDelete.id;
    this.deleteErrorMessage = '';
    this.deleteErrorCode = undefined;
    this.deleteErrorTraceId = undefined;

    this.taskService
      .deleteTask(taskToDelete.id)
      .pipe(finalize(() => {
        this.isDeleting = false;
        this.deleteInFlightTaskId = null;
      }))
      .subscribe({
        next: () => {
          this.reconcileTaskAfterDelete(taskToDelete);
          this.refreshUiStateFromTasks();
          this.pendingDeleteTask = null;
          this.liveMessage = `Task ${taskToDelete.title} deleted.`;
          this.restoreDeleteTriggerFocus();
        },
        error: (error: TaskProblemDetails) => {
          this.deleteErrorCode = error.code;
          this.deleteErrorTraceId = error.traceId;
          this.deleteErrorMessage = error.title ?? error.detail ?? 'Task deletion failed.';
          this.liveMessage = this.deleteErrorMessage;
        }
      });
  }

  closeDeleteOnEscape(event: KeyboardEvent): void {
    if (event.key !== 'Escape') {
      return;
    }

    event.preventDefault();
    this.cancelDelete();
  }

  isDeleteInFlight(taskId: string): boolean {
    return this.isDeleting && this.deleteInFlightTaskId === taskId;
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
    const requestId = ++this.latestLoadRequestId;
    this.uiState = { kind: 'loading' };

    this.taskService.getTasks(this.selectedFilter).subscribe({
      next: (response) => {
        if (requestId !== this.latestLoadRequestId) {
          return;
        }

        this.tasks = response.items;
        this.activeCount = response.summary.activeCount;
        this.completedCount = response.summary.completedCount;
        this.refreshUiStateFromTasks();

        if (shouldAnnounce) {
          this.liveMessage = this.buildResultAnnouncement();
        }
      },
      error: (error: TaskProblemDetails) => {
        if (requestId !== this.latestLoadRequestId) {
          return;
        }

        const message = error.title ?? error.detail ?? 'Unable to load tasks right now.';
        this.uiState = {
          kind: 'error',
          scope: 'load',
          message,
          code: error.code,
          traceId: error.traceId
        };

        if (shouldAnnounce) {
          this.liveMessage = message;
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

  private reconcileTaskAfterDelete(deletedTask: TaskResponse): void {
    this.tasks = this.tasks.filter((task) => task.id !== deletedTask.id);

    if (deletedTask.isCompleted) {
      this.completedCount = Math.max(0, this.completedCount - 1);
    } else {
      this.activeCount = Math.max(0, this.activeCount - 1);
    }

    if (this.editingTaskId === deletedTask.id) {
      this.cancelEdit();
    }

    delete this.toggleErrors[deletedTask.id];
    delete this.toggleErrorCodes[deletedTask.id];
    delete this.toggleErrorTraceIds[deletedTask.id];
    delete this.toggleTargetByTaskId[deletedTask.id];
  }

  private refreshUiStateFromTasks(): void {
    if (this.tasks.length === 0) {
      this.uiState = { kind: 'empty', filter: this.selectedFilter };
      return;
    }

    this.uiState = { kind: 'ready', tasks: this.tasks };
  }

  private problemSupportText(code?: string, traceId?: string): string {
    const parts: string[] = [];

    if (code) {
      parts.push(`Code: ${code}`);
    }

    if (traceId) {
      parts.push(`Trace ID: ${traceId}`);
    }

    return parts.join(' | ');
  }

  private restoreDeleteTriggerFocus(): void {
    const focusTarget = this.deleteReturnFocusElement;
    this.deleteReturnFocusElement = null;

    if (!focusTarget) {
      return;
    }

    setTimeout(() => {
      focusTarget.focus();
    }, 0);
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
