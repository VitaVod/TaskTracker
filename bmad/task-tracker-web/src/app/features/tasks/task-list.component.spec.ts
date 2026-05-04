import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ProgressService } from '../../shared/services/progress.service';
import { TaskService } from '../../shared/services/task.service';
import { TaskListComponent } from './task-list.component';

describe('TaskListComponent', () => {
  let fixture: ComponentFixture<TaskListComponent>;
  let component: TaskListComponent;
  let taskService: jasmine.SpyObj<TaskService>;
  let progressService: jasmine.SpyObj<ProgressService>;

  beforeEach(async () => {
    taskService = jasmine.createSpyObj<TaskService>('TaskService', ['getTasks', 'updateTask', 'toggleTaskCompletion', 'deleteTask']);
    progressService = jasmine.createSpyObj<ProgressService>('ProgressService', ['getXpSummary', 'getStreakSnapshot']);
    taskService.getTasks.and.returnValue(of({
      items: [
        {
          id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
          title: 'Plan sprint backlog',
          description: 'Draft story priorities',
          dueAtUtc: null,
          priority: 'medium',
          category: 'work',
          difficulty: 'easy',
          energyLevel: 'medium',
          contextTag: null,
          effortPoints: null,
          predictedDurationMinutes: null,
          isCompleted: false,
          createdAtUtc: '2026-04-25T11:30:12Z',
          updatedAtUtc: '2026-04-25T11:30:12Z'
        }
      ],
      summary: {
        activeCount: 1,
        completedCount: 0
      }
    }));
    taskService.updateTask.and.returnValue(of({
      id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
      title: 'Plan sprint backlog updated',
      description: 'Draft story priorities',
      dueAtUtc: null,
      priority: 'high',
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: null,
      effortPoints: null,
      predictedDurationMinutes: null,
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-26T09:15:03Z'
    }));
    taskService.toggleTaskCompletion.and.returnValue(of({
      task: {
        id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
        title: 'Plan sprint backlog',
        description: 'Draft story priorities',
        dueAtUtc: null,
        priority: 'medium',
        category: 'work',
        difficulty: 'easy',
        energyLevel: 'medium',
        contextTag: null,
        effortPoints: null,
        predictedDurationMinutes: null,
        isCompleted: true,
        createdAtUtc: '2026-04-25T11:30:12Z',
        updatedAtUtc: '2026-04-26T09:15:03Z'
      },
      progression: {
        completionEventId: '2d912ba8-f0d2-4d59-a5ec-8ef0f2d5cae2',
        xpLedgerEntryId: '68e4bba9-b3ef-4d15-bf33-8ea47d3fbf56',
        xpGranted: 10,
        eligibleForXp: true,
        idempotentReplay: false,
        idempotencyKey: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
        traceId: '0HNXP123'
      }
    }));
    taskService.deleteTask.and.returnValue(of(void 0));
    progressService.getXpSummary.and.returnValue(of({
      totalXp: 110,
      ledgerEntryCount: 11,
      lastGrantedAtUtc: '2026-04-26T09:15:03Z',
      levelProgress: {
        currentLevel: 2,
        currentLevelThresholdXp: 100,
        nextLevel: 3,
        nextLevelThresholdXp: 225,
        percentToNextLevel: 8,
        bandMilestoneLevels: [3, 5, 10, 20, 30, 50],
        reachedBandCount: 0,
        nextBandLevel: 3
      },
      outcomeExplanation: {
        reasonCode: 'xp-earned-from-completions',
        message: 'XP increased from eligible task completion events processed by the progression engine.'
      }
    }));
    progressService.getStreakSnapshot.and.returnValue(of({
      outcome: 'continue',
      currentStreakDays: 4,
      longestStreakDays: 10,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-25T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-26T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-26T09:15:03Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 4 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: null
    }));

    await TestBed.configureTestingModule({
      imports: [TaskListComponent],
      providers: [
        { provide: TaskService, useValue: taskService },
        { provide: ProgressService, useValue: progressService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads all tasks by default on init', () => {
    expect(taskService.getTasks).toHaveBeenCalledWith('all', undefined);
    expect(progressService.getXpSummary).toHaveBeenCalled();
    expect(progressService.getStreakSnapshot).toHaveBeenCalled();
    expect(component.activeCount).toBe(1);
    expect(component.completedCount).toBe(0);
  });

  it('renders completion feedback and refreshes progress after successful completion', () => {
    const task = component.tasks[0];
    progressService.getXpSummary.calls.reset();
    progressService.getStreakSnapshot.calls.reset();

    component.toggleCompletion(task, true);

    expect(component.completionFeedback).not.toBeNull();
    expect(component.completionFeedback?.message).toContain('+10 XP');
    expect(component.progressAnnouncement).toContain('XP');
    expect(progressService.getXpSummary).toHaveBeenCalled();
    expect(progressService.getStreakSnapshot).toHaveBeenCalled();
  });

  it('does not duplicate celebratory feedback for replayed completion events', () => {
    taskService.toggleTaskCompletion.and.returnValues(
      of({
        task: {
          ...component.tasks[0],
          isCompleted: true,
          updatedAtUtc: '2026-04-26T09:15:03Z'
        },
        progression: {
          completionEventId: 'event-1',
          xpLedgerEntryId: 'ledger-1',
          xpGranted: 10,
          eligibleForXp: true,
          idempotentReplay: false,
          idempotencyKey: 'idem-1',
          traceId: 'trace-1',
          streak: {
            outcome: 'continue',
            currentStreakDays: 4,
            longestStreakDays: 10,
            timeZoneId: 'UTC',
            evaluationWindowStartUtc: '2026-04-25T00:00:00Z',
            evaluationWindowEndUtc: '2026-04-26T00:00:00Z'
          }
        }
      }),
      of({
        task: {
          ...component.tasks[0],
          isCompleted: true,
          updatedAtUtc: '2026-04-26T09:16:03Z'
        },
        progression: {
          completionEventId: 'event-1',
          xpLedgerEntryId: 'ledger-1',
          xpGranted: 10,
          eligibleForXp: true,
          idempotentReplay: true,
          idempotencyKey: 'idem-2',
          traceId: 'trace-2',
          streak: {
            outcome: 'continue',
            currentStreakDays: 4,
            longestStreakDays: 10,
            timeZoneId: 'UTC',
            evaluationWindowStartUtc: '2026-04-25T00:00:00Z',
            evaluationWindowEndUtc: '2026-04-26T00:00:00Z'
          }
        }
      })
    );

    const task = component.tasks[0];
    component.toggleCompletion(task, true);
    const firstMessage = component.completionFeedback?.message;
    component.toggleCompletion(component.tasks[0], true);

    expect(component.completionFeedback?.message).toBe(firstMessage);
  });

  it('shows an action-oriented empty state when no tasks are returned', () => {
    taskService.getTasks.and.returnValues(
      of({
        items: [],
        summary: { activeCount: 0, completedCount: 0 }
      }),
      of({
        items: [],
        summary: { activeCount: 0, completedCount: 0 }
      })
    );

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const emptyHeading = fixture.nativeElement.querySelector('.empty-state h2') as HTMLElement;
    expect(emptyHeading.textContent).toContain('No tasks yet');

    const activeButton = fixture.nativeElement.querySelector('button[aria-label="Show active tasks"]') as HTMLButtonElement;
    activeButton.click();
    fixture.detectChanges();

    const filteredHeading = fixture.nativeElement.querySelector('.empty-state h2') as HTMLElement;
    const resetFilterButton = fixture.nativeElement.querySelector('.empty-state .cancel-button') as HTMLButtonElement;
    expect(filteredHeading.textContent).toContain('No active tasks found');
    expect(resetFilterButton.textContent).toContain('View all tasks');
  });

  it('reuses active-task empty-state guidance when all filter has only completed tasks', () => {
    taskService.getTasks.and.returnValues(
      of({
        items: [
          {
            id: 'completed-only-1',
            title: 'Ship release notes',
            description: 'done',
            dueAtUtc: null,
            priority: 'low',
            category: 'work',
            difficulty: 'easy',
            energyLevel: 'low',
            contextTag: null,
            effortPoints: null,
            predictedDurationMinutes: null,
            isCompleted: true,
            createdAtUtc: '2026-04-25T11:30:12Z',
            updatedAtUtc: '2026-04-26T09:15:03Z'
          }
        ],
        summary: { activeCount: 0, completedCount: 1 }
      })
    );

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const activeEmptyHeading = fixture.nativeElement.querySelector('.active-empty-state h3') as HTMLElement;
    const activeEmptyMessage = fixture.nativeElement.querySelector('.active-empty-state .status') as HTMLElement;
    const createButton = fixture.nativeElement.querySelector('.active-empty-state .create-primary') as HTMLAnchorElement;

    expect(activeEmptyHeading.textContent).toContain('No active tasks found');
    expect(activeEmptyMessage.textContent).toContain('Create your next task to start building momentum');
    expect(createButton.textContent).toContain('Create a task');
  });

  it('limits task edit description textarea resizing to vertical only', () => {
    const editButton = fixture.nativeElement.querySelector('button[aria-label="Edit task Plan sprint backlog"]') as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();

    const descriptionTextarea = fixture.nativeElement.querySelector('textarea#edit-description') as HTMLTextAreaElement;
    expect(descriptionTextarea.getAttribute('style')).toContain('resize: vertical');
  });

  it('renders planning filters in requested order', () => {
    const labels = Array.from(
      fixture.nativeElement.querySelectorAll('.planning-filter-grid label span') as NodeListOf<HTMLElement>
    ).map((item) => item.textContent?.trim());

    expect(labels).toEqual(['Title', 'Priority', 'Difficulty', 'Energy', 'Context']);
  });

  it('clears title and priority planning filters', () => {
    component.titleFilterInput = 'Sprint';
    component.selectedPriorityFilter = 'high';
    component.selectedDifficultyFilter = 'medium';
    component.selectedEnergyFilter = 'high';
    component.contextFilterInput = 'office';

    component.clearPlanningFilters();

    expect(component.titleFilterInput).toBe('');
    expect(component.selectedPriorityFilter).toBe('');
    expect(component.selectedDifficultyFilter).toBe('');
    expect(component.selectedEnergyFilter).toBe('');
    expect(component.contextFilterInput).toBe('');
  });

  it('shows loading placeholders while list request is in flight', () => {
    const pending = new Subject<any>();
    taskService.getTasks.and.returnValue(pending.asObservable());

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.isUiLoading()).toBeTrue();
    const skeletons = fixture.nativeElement.querySelectorAll('.loading-skeleton') as NodeListOf<HTMLElement>;
    expect(skeletons.length).toBe(3);
    expect(Array.from(skeletons).every((skeleton) => skeleton.getAttribute('aria-hidden') === 'true')).toBeTrue();

    pending.next({
      items: [],
      summary: { activeCount: 0, completedCount: 0 }
    });
    pending.complete();
    fixture.detectChanges();

    expect(component.isUiEmpty()).toBeTrue();
  });

  it('ignores stale filter responses and keeps the latest selected filter results', () => {
    const activeResponse = new Subject<any>();
    const completedResponse = new Subject<any>();

    taskService.getTasks.and.callFake((state) => {
      if (state === 'active') {
        return activeResponse.asObservable();
      }

      if (state === 'completed') {
        return completedResponse.asObservable();
      }

      return of({
        items: [],
        summary: { activeCount: 0, completedCount: 0 }
      });
    });

    component.setFilter('active');
    component.setFilter('completed');

    completedResponse.next({
      items: [
        {
          id: 'completed-1',
          title: 'Completed first',
          description: 'done',
          dueAtUtc: null,
          priority: 'low',
          category: 'work',
          difficulty: 'easy',
          energyLevel: 'medium',
          contextTag: null,
          effortPoints: null,
          predictedDurationMinutes: null,
          isCompleted: true,
          createdAtUtc: '2026-04-25T11:30:12Z',
          updatedAtUtc: '2026-04-25T12:30:12Z'
        }
      ],
      summary: { activeCount: 3, completedCount: 7 }
    });
    completedResponse.complete();

    // Simulate slower, stale active response completing after completed filter result.
    activeResponse.next({
      items: [
        {
          id: 'active-1',
          title: 'Active late response',
          description: 'in progress',
          dueAtUtc: null,
          priority: 'medium',
          category: 'work',
          difficulty: 'easy',
          energyLevel: 'medium',
          contextTag: null,
          effortPoints: null,
          predictedDurationMinutes: null,
          isCompleted: false,
          createdAtUtc: '2026-04-25T11:30:12Z',
          updatedAtUtc: '2026-04-25T12:30:12Z'
        }
      ],
      summary: { activeCount: 11, completedCount: 1 }
    });
    activeResponse.complete();

    fixture.detectChanges();

    expect(component.selectedFilter).toBe('completed');
    expect(component.tasks.length).toBe(1);
    expect(component.tasks[0].id).toBe('completed-1');
    expect(component.activeCount).toBe(3);
    expect(component.completedCount).toBe(7);
  });

  it('renders load error recovery actions and retries list loading', () => {
    taskService.getTasks.calls.reset();
    taskService.getTasks.and.returnValues(
      throwError(() => ({
        title: 'Request failed',
        code: 'task.request.failed',
        traceId: '0HNTRACE123'
      })),
      of({
        items: [],
        summary: { activeCount: 0, completedCount: 0 }
      })
    );

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.isUiLoadError()).toBeTrue();

    const retryButton = fixture.nativeElement.querySelector('.error-state .save-button') as HTMLButtonElement;
    retryButton.click();
    fixture.detectChanges();

    expect(taskService.getTasks.calls.count()).toBe(2);
    expect(taskService.getTasks.calls.mostRecent().args[0]).toBe('all');
    expect(component.isUiEmpty()).toBeTrue();
  });

  it('changes filter on click and requests completed tasks', () => {
    taskService.getTasks.and.returnValues(
      of({
        items: [],
        summary: { activeCount: 1, completedCount: 0 }
      }),
      of({
        items: [
          {
            id: '1f8d3d3f-1bba-4b43-8de6-2bf5f83e8a33',
            title: 'Completed item',
            description: 'done',
            dueAtUtc: null,
            priority: 'low',
            category: 'personal',
            difficulty: 'easy',
            energyLevel: 'medium',
            contextTag: null,
            effortPoints: null,
            predictedDurationMinutes: null,
            isCompleted: true,
            createdAtUtc: '2026-04-25T11:30:12Z',
            updatedAtUtc: '2026-04-25T12:30:12Z'
          }
        ],
        summary: { activeCount: 0, completedCount: 1 }
      })
    );

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const completedButton = fixture.nativeElement.querySelector('button[aria-label="Show completed tasks"]') as HTMLButtonElement;
    completedButton.click();
    fixture.detectChanges();

    expect(taskService.getTasks).toHaveBeenCalledWith('completed', undefined);
    expect(component.selectedFilter).toBe('completed');
    expect(component.liveMessage).toContain('completed');
    const completedEditButton = fixture.nativeElement.querySelector('button[aria-label="Edit task Completed item"]') as HTMLButtonElement | null;
    expect(completedEditButton).toBeNull();
  });

  it('does not allow entering edit mode for completed tasks', () => {
    const completedTask = {
      ...component.tasks[0],
      id: 'completed-locked',
      title: 'Completed lock',
      isCompleted: true
    };

    component.startEdit(completedTask);

    expect(component.editingTaskId).toBeNull();
  });

  it('does not render edit form for completed tasks even if editingTaskId is stale', () => {
    const completedTask = {
      ...component.tasks[0],
      id: 'completed-stale',
      title: 'Completed stale',
      isCompleted: true
    };

    component.selectedFilter = 'completed';
    component.tasks = [completedTask];
    component.editingTaskId = completedTask.id;
    fixture.detectChanges();

    const editForm = fixture.nativeElement.querySelector('.edit-form') as HTMLFormElement | null;
    expect(editForm).toBeNull();
  });

  it('toggles tab-switch animation phase when changing task-state tabs', () => {
    const initialPhase = component.tabSwitchAnimationPhase;

    component.setFilter('active');
    const afterFirstSwitch = component.tabSwitchAnimationPhase;
    component.setFilter('completed');

    expect(afterFirstSwitch).not.toBe(initialPhase);
    expect(component.tabSwitchAnimationPhase).toBe(initialPhase);
  });

  it('supports keyboard filter selection using Enter', () => {
    const activeButton = fixture.nativeElement.querySelector('button[aria-label="Show active tasks"]') as HTMLButtonElement;
    activeButton.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(taskService.getTasks).toHaveBeenCalledWith('active', undefined);
  });

  it('renders explicit state text labels for tasks', () => {
    const stateText = fixture.nativeElement.querySelector('.state-text') as HTMLElement;
    expect(stateText.textContent).toContain('State: Active');
  });

  it('defaults edit effort points to 50 when task effort is missing', () => {
    const editButton = fixture.nativeElement.querySelector('button[aria-label="Edit task Plan sprint backlog"]') as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();

    expect(component.editForm.getRawValue().effortPoints).toBe(50);
  });

  it('submits edit updates and reconciles local task state', () => {
    const editButton = fixture.nativeElement.querySelector('button[aria-label="Edit task Plan sprint backlog"]') as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();

    component.editForm.patchValue({
      title: ' Plan sprint backlog updated ',
      description: ' Draft story priorities ',
      dueAtUtc: '',
      priority: 'high',
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: '',
      effortPoints: null
    });

    component.submitEdit();
    fixture.detectChanges();

    expect(taskService.updateTask).toHaveBeenCalled();
    const args = taskService.updateTask.calls.mostRecent().args;
    expect(args[0]).toBe('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12');
    expect(args[1]).toEqual({
      title: 'Plan sprint backlog updated',
      description: 'Draft story priorities',
      dueAtUtc: null,
      priority: 'high',
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: null,
      effortPoints: null
    });
    expect(component.tasks[0].title).toBe('Plan sprint backlog updated');
    expect(component.editingTaskId).toBeNull();
  });

  it('keeps user edits when API update fails', () => {
    taskService.updateTask.and.returnValue(throwError(() => ({
      title: 'Validation failed',
      code: 'validation.request.invalid',
      traceId: '0HN1FDHJ123',
      errors: {
        title: ['The title field is required.']
      }
    })));

    const editButton = fixture.nativeElement.querySelector('button[aria-label="Edit task Plan sprint backlog"]') as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();

    component.editForm.patchValue({
      title: 'Potential new title',
      description: 'Draft story priorities',
      dueAtUtc: '',
      priority: 'medium',
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: '',
      effortPoints: null
    });

    component.submitEdit();

    expect(component.saveErrorMessage).toBe('Validation failed');
    expect(component.saveErrorCode).toBe('validation.request.invalid');
    expect(component.saveErrorTraceId).toBe('0HN1FDHJ123');
    expect(component.fieldError('title')).toContain('required');
    expect(component.editForm.getRawValue().title).toBe('Potential new title');
  });

  it('blocks submitEdit when the editing task is already completed', () => {
    const task = component.tasks[0];
    component.startEdit(task);
    component.tasks = [{ ...task, isCompleted: true }];

    component.submitEdit();

    expect(taskService.updateTask).not.toHaveBeenCalled();
    expect(component.editingTaskId).toBeNull();
  });

  it('toggles completion and updates summary counts from server-confirmed result', () => {
    const task = component.tasks[0];
    component.startEdit(task);

    component.toggleCompletion(task, true);

    expect(taskService.toggleTaskCompletion).toHaveBeenCalled();
    expect(component.tasks[0].isCompleted).toBeTrue();
    expect(component.activeCount).toBe(0);
    expect(component.completedCount).toBe(1);
    expect(component.editingTaskId).toBeNull();
    expect(component.liveMessage).toContain('+10 XP awarded');
  });

  it('prevents duplicate toggle submissions while request is in-flight', () => {
    const pending = new Subject<any>();
    taskService.toggleTaskCompletion.and.returnValue(pending.asObservable());

    const task = component.tasks[0];
    component.toggleCompletion(task, true);
    component.toggleCompletion(task, true);

    expect(taskService.toggleTaskCompletion.calls.count()).toBe(1);
    expect(component.isToggleInFlight(task.id)).toBeTrue();

    pending.next({
      task: {
        ...task,
        isCompleted: true,
        updatedAtUtc: '2026-04-26T09:15:03Z'
      },
      progression: {
        completionEventId: '2d912ba8-f0d2-4d59-a5ec-8ef0f2d5cae2',
        xpLedgerEntryId: '68e4bba9-b3ef-4d15-bf33-8ea47d3fbf56',
        xpGranted: 10,
        eligibleForXp: true,
        idempotentReplay: false,
        idempotencyKey: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
        traceId: '0HNXP123'
      }
    });
    pending.complete();

    expect(component.isToggleInFlight(task.id)).toBeFalse();
  });

  it('offers retry for toggle failures and reuses the original intended completion target', () => {
    taskService.toggleTaskCompletion.and.returnValues(
      throwError(() => ({
        title: 'Conflict',
        code: 'task.completion.conflict',
        traceId: '0HNTOGGLE123'
      })),
      of({
        task: {
          ...component.tasks[0],
          isCompleted: true,
          updatedAtUtc: '2026-04-26T09:15:03Z'
        },
        progression: {
          completionEventId: '2d912ba8-f0d2-4d59-a5ec-8ef0f2d5cae2',
          xpLedgerEntryId: '68e4bba9-b3ef-4d15-bf33-8ea47d3fbf56',
          xpGranted: 10,
          eligibleForXp: true,
          idempotentReplay: true,
          idempotencyKey: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
          traceId: '0HNXP123'
        }
      })
    );

    const task = component.tasks[0];
    component.toggleCompletion(task, true);

    expect(component.toggleError(task.id)).toBe('Conflict');
    expect(component.toggleErrorSupportText(task.id)).toContain('task.completion.conflict');

    component.retryToggle(task);

    expect(taskService.toggleTaskCompletion.calls.count()).toBe(2);
    expect(taskService.toggleTaskCompletion.calls.mostRecent().args[1]).toEqual({ isCompleted: true });
  });

  it('opens delete confirmation and allows cancel without removing task', () => {
    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Delete task Plan sprint backlog"]') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();

    expect(component.pendingDeleteTask?.id).toBe('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12');

    const cancelButton = fixture.nativeElement.querySelector('.delete-dialog .cancel-button') as HTMLButtonElement;
    cancelButton.click();
    fixture.detectChanges();

    expect(component.pendingDeleteTask).toBeNull();
    expect(component.tasks.length).toBe(1);
  });

  it('confirms delete and reconciles list state after server success', () => {
    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Delete task Plan sprint backlog"]') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();

    const confirmButton = fixture.nativeElement.querySelector('.delete-confirm-button') as HTMLButtonElement;
    confirmButton.click();
    fixture.detectChanges();

    expect(taskService.deleteTask).toHaveBeenCalledWith('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12');
    expect(component.tasks.length).toBe(0);
    expect(component.activeCount).toBe(0);
    expect(component.completedCount).toBe(0);
    expect(component.pendingDeleteTask).toBeNull();
    expect(component.liveMessage).toContain('deleted');
  });

  it('keeps confirmation open and shows API error when delete fails', () => {
    taskService.deleteTask.and.returnValue(throwError(() => ({
      title: 'Forbidden',
      detail: 'Cannot delete this task.'
    })));

    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Delete task Plan sprint backlog"]') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();

    const confirmButton = fixture.nativeElement.querySelector('.delete-confirm-button') as HTMLButtonElement;
    confirmButton.click();
    fixture.detectChanges();

    expect(component.pendingDeleteTask).not.toBeNull();
    expect(component.deleteErrorMessage).toBe('Forbidden');
    expect(component.tasks.length).toBe(1);
  });

  it('shows actionable guidance when completed-task deletion is blocked by progression rules', () => {
    taskService.deleteTask.and.returnValue(throwError(() => ({
      code: 'tasks.delete.completed.blocked',
      detail: 'Completed tasks cannot be deleted because progress must remain deterministic.'
    })));

    const deleteButton = fixture.nativeElement.querySelector('button[aria-label="Delete task Plan sprint backlog"]') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();

    const confirmButton = fixture.nativeElement.querySelector('.delete-confirm-button') as HTMLButtonElement;
    confirmButton.click();
    fixture.detectChanges();

    expect(component.pendingDeleteTask).not.toBeNull();
    expect(component.deleteErrorCode).toBe('tasks.delete.completed.blocked');
    expect(component.deleteErrorMessage).toContain('cannot be deleted');
    expect(component.deleteErrorMessage).toContain('deterministic');
  });
});
