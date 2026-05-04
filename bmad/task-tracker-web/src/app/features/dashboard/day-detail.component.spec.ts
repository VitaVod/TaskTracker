import { ComponentFixture, TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { ProgressService } from '../../shared/services/progress.service';
import { TaskService } from '../../shared/services/task.service';
import { DayDetailComponent } from './day-detail.component';

describe('DayDetailComponent', () => {
  let fixture: ComponentFixture<DayDetailComponent>;
  let component: DayDetailComponent;
  let progressService: jasmine.SpyObj<ProgressService>;
  let taskService: jasmine.SpyObj<TaskService>;
  let paramMap$: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    progressService = jasmine.createSpyObj<ProgressService>('ProgressService', ['getTrendSummary', 'getStreakSnapshot']);
    taskService = jasmine.createSpyObj<TaskService>('TaskService', ['getTasks']);
    paramMap$ = new BehaviorSubject(convertToParamMap({ date: '2026-04-03' }));

    progressService.getTrendSummary.and.returnValue(of({
      granularity: 'daily',
      windowDays: 31,
      timeZoneId: 'UTC',
      rangeStartUtc: '2026-04-01T00:00:00Z',
      rangeEndUtc: '2026-04-30T23:59:59Z',
      items: [
        {
          bucketStartUtc: '2026-04-03T00:00:00Z',
          bucketEndUtc: '2026-04-03T23:59:59Z',
          completedTaskCount: 2,
          xpGranted: 40
        }
      ]
    }));

    progressService.getStreakSnapshot.and.returnValue(of({
      outcome: 'continue',
      currentStreakDays: 6,
      longestStreakDays: 8,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-03T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-04T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-04T08:00:00Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Streak continued.'
      },
      recoveryExplanation: null
    }));

    taskService.getTasks.and.returnValue(of({
      summary: { activeCount: 0, completedCount: 2 },
      items: [
        {
          id: 'task-1',
          title: 'Ship heatmap feature',
          description: '',
          dueAtUtc: null,
          priority: 'high',
          category: 'work',
          difficulty: 'medium',
          energyLevel: 'high',
          contextTag: null,
          effortPoints: 5,
          predictedDurationMinutes: 30,
          isCompleted: true,
          createdAtUtc: '2026-04-02T08:00:00Z',
          updatedAtUtc: '2026-04-03T10:00:00Z'
        },
        {
          id: 'task-2',
          title: 'Other day task',
          description: '',
          dueAtUtc: null,
          priority: 'medium',
          category: 'work',
          difficulty: 'easy',
          energyLevel: 'medium',
          contextTag: null,
          effortPoints: 3,
          predictedDurationMinutes: 20,
          isCompleted: true,
          createdAtUtc: '2026-04-01T08:00:00Z',
          updatedAtUtc: '2026-04-02T10:00:00Z'
        }
      ]
    }));

    await TestBed.configureTestingModule({
      imports: [DayDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ProgressService, useValue: progressService },
        { provide: TaskService, useValue: taskService },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMap$.asObservable()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DayDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads day detail from route date and renders metrics', () => {
    expect(component.state).toBe('ready');
    expect(component.selectedDate).toBe('2026-04-03');
    expect(component.xpGranted).toBe(40);
    expect(component.completedTasks.length).toBe(1);
    expect(component.momentumScore).toBe(61);
    expect(progressService.getTrendSummary).toHaveBeenCalledWith('daily', 31);
    expect(taskService.getTasks).toHaveBeenCalledWith('completed');
  });

  it('shows error state for invalid date route parameter', () => {
    paramMap$.next(convertToParamMap({ date: '04-03-2026' }));
    fixture.detectChanges();

    expect(component.state).toBe('error');
    expect(component.errorMessage).toContain('Invalid day selected');
  });

  it('shows error state when loading day detail fails', () => {
    progressService.getTrendSummary.and.returnValue(throwError(() => ({ title: 'Failed' })));

    fixture = TestBed.createComponent(DayDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('error');
    expect(component.errorMessage).toContain('Unable to load day detail');
  });
});
