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
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import {
  TASK_CATEGORY_OPTIONS,
  TaskCategory,
  TaskCompletionProgression,
  TaskDifficulty,
  TaskEnergyLevel,
  TaskListFilters,
  TaskListState,
  TaskPriority,
  TaskProblemDetails,
  TaskResponse,
  TaskUiState,
  isTaskCategory,
  toTaskCategoryLabel
} from '../../shared/models/task.models';
import { ProgressStreakSnapshot, ProgressXpSummary } from '../../shared/models/progress.models';
import { ProgressService } from '../../shared/services/progress.service';
import { TaskService } from '../../shared/services/task.service';

type ProgressCardState = 'loading' | 'ready' | 'error';
type FeedbackTone = 'success' | 'info';

interface CompletionFeedback {
  message: string;
  streakMessage: string;
  tone: FeedbackTone;
  replay: boolean;
  xpGranted: number;
}

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.scss'
})
export class TaskListComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly progressService = inject(ProgressService);
  private readonly formBuilder = inject(FormBuilder);

  readonly categoryOptions = TASK_CATEGORY_OPTIONS;
  readonly minDueDateTimeLocal = buildTodayMinDateTimeLocal();

  readonly filterOptions: ReadonlyArray<{ value: TaskListState; label: string; ariaLabel: string }> = [
    { value: 'all', label: 'All tasks', ariaLabel: 'Show all tasks' },
    { value: 'active', label: 'Active tasks', ariaLabel: 'Show active tasks' },
    { value: 'completed', label: 'Completed tasks', ariaLabel: 'Show completed tasks' }
  ];

  selectedFilter: TaskListState = 'all';
  tabSwitchAnimationPhase: 'a' | 'b' = 'a';
  titleFilterInput = '';
  selectedPriorityFilter: TaskPriority | '' = '';
  selectedEnergyFilter: TaskEnergyLevel | '' = '';
  selectedDifficultyFilter: TaskDifficulty | '' = '';
  contextFilterInput = '';
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
  xpSummary: ProgressXpSummary | null = null;
  streakSnapshot: ProgressStreakSnapshot | null = null;
  progressState: ProgressCardState = 'loading';
  progressMessage = '';
  progressSupportCode?: string;
  progressSupportTraceId?: string;
  completionFeedback: CompletionFeedback | null = null;
  progressAnnouncement = '';
  isDeleting = false;
  private readonly completionToggleInFlight = new Set<string>();
  private readonly latestFeedbackKeyByTaskId: Record<string, string> = {};
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
    category: ['work' as TaskCategory, [Validators.required]],
    difficulty: ['easy' as TaskDifficulty, [Validators.required]],
    energyLevel: ['medium' as TaskEnergyLevel, [Validators.required]],
    contextTag: ['', [Validators.maxLength(64)]],
    effortPoints: [null as number | null, [Validators.min(1), Validators.max(100)]]
  });

  ngOnInit(): void {
    this.loadTasks(false);
    this.loadProgressSnapshot(false);
  }

  setFilter(filter: TaskListState): void {
    if (filter === this.selectedFilter) {
      return;
    }

    this.selectedFilter = filter;
    this.toggleTabSwitchAnimationPhase();
    this.loadTasks(true);
  }

  applyPlanningFilters(): void {
    this.loadTasks(true);
  }

  clearPlanningFilters(): void {
    this.titleFilterInput = '';
    this.selectedPriorityFilter = '';
    this.selectedEnergyFilter = '';
    this.selectedDifficultyFilter = '';
    this.contextFilterInput = '';
    this.loadTasks(true);
  }

  setTitleFilter(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    this.titleFilterInput = target.value;
  }

  setPriorityFilter(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement)) {
      return;
    }

    this.selectedPriorityFilter = target.value as TaskPriority | '';
  }

  setFilterFromKeyboard(event: Event, filter: TaskListState): void {
    event.preventDefault();
    this.setFilter(filter);
  }

  setEnergyFilter(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement)) {
      return;
    }

    this.selectedEnergyFilter = target.value as TaskEnergyLevel | '';
  }

  setContextFilter(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    this.contextFilterInput = target.value;
  }

  setDifficultyFilter(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement)) {
      return;
    }

    this.selectedDifficultyFilter = target.value as TaskDifficulty | '';
  }

  retryLoad(): void {
    this.loadTasks(true);
  }

  retryProgressSnapshot(): void {
    this.loadProgressSnapshot(true);
  }

  resetFilterToAll(): void {
    if (this.selectedFilter === 'all') {
      return;
    }

    this.selectedFilter = 'all';
    this.toggleTabSwitchAnimationPhase();
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

  dismissCompletionFeedback(): void {
    this.completionFeedback = null;
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

  progressSupportText(): string {
    return this.problemSupportText(this.progressSupportCode, this.progressSupportTraceId);
  }

  streakIconLabel(): string {
    if (!this.streakSnapshot) {
      return 'Unknown status';
    }

    if (this.streakSnapshot.outcome === 'continue') {
      return 'Maintained streak';
    }

    if (this.streakSnapshot.outcome === 'restart') {
      return 'Restarted streak';
    }

    return 'Streak reset';
  }

  streakOutcomeLabel(): string {
    if (!this.streakSnapshot) {
      return 'Streak data unavailable';
    }

    if (this.streakSnapshot.outcome === 'continue') {
      return 'Continuity maintained';
    }

    if (this.streakSnapshot.outcome === 'restart') {
      return 'Continuity restarted';
    }

    return 'Continuity reset';
  }

  nextActionCue(): string {
    if (!this.streakSnapshot) {
      return 'Complete a task to refresh continuity guidance.';
    }

    if (this.streakSnapshot.outcome === 'continue') {
      return 'Complete at least one task in your next local-day window to keep momentum.';
    }

    if (this.streakSnapshot.outcome === 'restart') {
      return 'Great recovery. Complete another task tomorrow to continue the renewed streak.';
    }

    return 'Start a new streak by completing a task in the current local-day window.';
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
      category: this.toEditableCategory(task.category),
      difficulty: task.difficulty,
      energyLevel: task.energyLevel,
      contextTag: task.contextTag ?? '',
      effortPoints: task.effortPoints ?? 50
    });
  }

  toggleEdit(task: TaskResponse): void {
    if (this.isEditing(task)) {
      this.cancelEdit();
      return;
    }

    this.startEdit(task);
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
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: '',
      effortPoints: null
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
      category: rawValue.category,
      difficulty: rawValue.difficulty,
      energyLevel: rawValue.energyLevel,
      contextTag: rawValue.contextTag.trim() === '' ? null : rawValue.contextTag.trim().toLowerCase(),
      effortPoints: rawValue.effortPoints
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

  fieldError(fieldName: 'title' | 'description' | 'dueAtUtc' | 'priority' | 'category' | 'difficulty' | 'energyLevel' | 'contextTag' | 'effortPoints'): string {
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
        next: (toggleResponse) => {
          const updatedTask = toggleResponse.task;
          this.reconcileTaskAfterCompletionToggle(task, updatedTask);
          this.refreshUiStateFromTasks();
          this.liveMessage = this.buildCompletionLiveMessage(updatedTask, toggleResponse.progression);
          this.captureCompletionFeedback(updatedTask, toggleResponse.progression);
          this.loadProgressSnapshot(false);
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
          this.deleteErrorMessage = this.resolveDeleteErrorMessage(error);
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

    const filters: TaskListFilters = {};
    if (this.titleFilterInput.trim() !== '') {
      filters.title = this.titleFilterInput.trim();
    }

    if (this.selectedPriorityFilter !== '') {
      filters.priority = this.selectedPriorityFilter;
    }

    if (this.selectedEnergyFilter !== '') {
      filters.energyLevel = this.selectedEnergyFilter;
    }

    if (this.selectedDifficultyFilter !== '') {
      filters.difficulty = this.selectedDifficultyFilter;
    }

    if (this.contextFilterInput.trim() !== '') {
      filters.contextTag = this.contextFilterInput.trim();
    }

    const hasPlanningFilters = Boolean(filters.title || filters.priority || filters.energyLevel || filters.difficulty || filters.contextTag);
    this.taskService.getTasks(this.selectedFilter, hasPlanningFilters ? filters : undefined).subscribe({
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

    if (
      this.titleFilterInput.trim() !== ''
      || this.selectedPriorityFilter
      || this.selectedEnergyFilter
      || this.selectedDifficultyFilter
      || this.contextFilterInput.trim() !== ''
    ) {
      return `Showing ${this.tasks.length} tasks for selected planning filters.`;
    }

    return `Showing ${this.activeCount} active and ${this.completedCount} completed tasks.`;
  }

  private toggleTabSwitchAnimationPhase(): void {
    this.tabSwitchAnimationPhase = this.tabSwitchAnimationPhase === 'a' ? 'b' : 'a';
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

  private buildCompletionLiveMessage(task: TaskResponse, progression: TaskCompletionProgression): string {
    if (!task.isCompleted) {
      return `Task ${task.title} marked active.`;
    }

    if (progression.xpGranted > 0) {
      return `Task ${task.title} marked completed. +${progression.xpGranted} XP awarded.`;
    }

    if (progression.idempotentReplay) {
      return `Task ${task.title} completion confirmed with no duplicate XP.`;
    }

    return `Task ${task.title} marked completed.`;
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
    delete this.latestFeedbackKeyByTaskId[deletedTask.id];
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

  private resolveDeleteErrorMessage(error: TaskProblemDetails): string {
    if (error.code === 'tasks.delete.completed.blocked') {
      return error.detail
        ?? 'Completed tasks cannot be deleted. Mark the task as active if you need to change it, then keep it in completed history.';
    }

    return error.title ?? error.detail ?? 'Task deletion failed.';
  }

  private loadProgressSnapshot(shouldAnnounceError: boolean): void {
    if (!this.xpSummary || !this.streakSnapshot) {
      this.progressState = 'loading';
    }

    this.progressMessage = '';
    this.progressSupportCode = undefined;
    this.progressSupportTraceId = undefined;

    forkJoin({
      xpSummary: this.progressService.getXpSummary().pipe(catchError(() => of(null))),
      streakSnapshot: this.progressService.getStreakSnapshot().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ xpSummary, streakSnapshot }) => {
        if (xpSummary) {
          this.xpSummary = xpSummary;
        }

        if (streakSnapshot) {
          this.streakSnapshot = streakSnapshot;
        }

        if (this.xpSummary || this.streakSnapshot) {
          this.progressState = 'ready';
          return;
        }

        this.progressState = 'error';
        this.progressMessage = 'Unable to load dashboard progress right now.';
        if (shouldAnnounceError) {
          this.progressAnnouncement = this.progressMessage;
        }
      },
      error: (error: TaskProblemDetails) => {
        this.progressState = 'error';
        this.progressSupportCode = error.code;
        this.progressSupportTraceId = error.traceId;
        this.progressMessage = error.title ?? error.detail ?? 'Unable to load dashboard progress right now.';
        if (shouldAnnounceError) {
          this.progressAnnouncement = this.progressMessage;
        }
      }
    });
  }

  private captureCompletionFeedback(task: TaskResponse, progression: TaskCompletionProgression): void {
    if (!task.isCompleted) {
      this.completionFeedback = null;
      return;
    }

    const eventKey = progression.completionEventId ?? progression.idempotencyKey;
    if (this.latestFeedbackKeyByTaskId[task.id] === eventKey) {
      return;
    }

    this.latestFeedbackKeyByTaskId[task.id] = eventKey;

    const streakMessage = this.buildStreakFeedbackMessage(progression);
    const replay = progression.idempotentReplay;
    const message = replay
      ? `Completion confirmed for ${task.title}. No duplicate XP was granted.`
      : progression.xpGranted > 0
        ? `+${progression.xpGranted} XP for completing ${task.title}.`
        : `Completion recorded for ${task.title}.`;

    this.completionFeedback = {
      message,
      streakMessage,
      tone: replay ? 'info' : 'success',
      replay,
      xpGranted: progression.xpGranted
    };

    this.progressAnnouncement = `${message} ${streakMessage}`.trim();
  }

  private buildStreakFeedbackMessage(progression: TaskCompletionProgression): string {
    if (!progression.streak) {
      return 'Streak status is refreshing.';
    }

    if (progression.streak.outcome === 'continue') {
      return `Streak continues at ${progression.streak.currentStreakDays} day(s).`;
    }

    if (progression.streak.outcome === 'restart') {
      return `Streak restarted at ${progression.streak.currentStreakDays} day(s).`;
    }

    return 'Streak reset. Complete another task to begin again.';
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
