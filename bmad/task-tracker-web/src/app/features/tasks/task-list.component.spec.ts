import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { TaskService } from '../../shared/services/task.service';
import { TaskListComponent } from './task-list.component';

describe('TaskListComponent', () => {
  let fixture: ComponentFixture<TaskListComponent>;
  let component: TaskListComponent;
  let taskService: jasmine.SpyObj<TaskService>;

  beforeEach(async () => {
    taskService = jasmine.createSpyObj<TaskService>('TaskService', ['getTasks', 'updateTask', 'toggleTaskCompletion']);
    taskService.getTasks.and.returnValue(of({
      items: [
        {
          id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
          title: 'Plan sprint backlog',
          description: 'Draft story priorities',
          dueAtUtc: null,
          priority: 'medium',
          category: 'work',
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
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-26T09:15:03Z'
    }));
    taskService.toggleTaskCompletion.and.returnValue(of({
      id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
      title: 'Plan sprint backlog',
      description: 'Draft story priorities',
      dueAtUtc: null,
      priority: 'medium',
      category: 'work',
      isCompleted: true,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-26T09:15:03Z'
    }));

    await TestBed.configureTestingModule({
      imports: [TaskListComponent],
      providers: [
        { provide: TaskService, useValue: taskService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TaskListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads all tasks by default on init', () => {
    expect(taskService.getTasks).toHaveBeenCalledWith('all');
    expect(component.activeCount).toBe(1);
    expect(component.completedCount).toBe(0);
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

    expect(taskService.getTasks).toHaveBeenCalledWith('completed');
    expect(component.selectedFilter).toBe('completed');
    expect(component.liveMessage).toContain('completed');
  });

  it('supports keyboard filter selection using Enter', () => {
    const activeButton = fixture.nativeElement.querySelector('button[aria-label="Show active tasks"]') as HTMLButtonElement;
    activeButton.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(taskService.getTasks).toHaveBeenCalledWith('active');
  });

  it('renders explicit state text labels for tasks', () => {
    const stateText = fixture.nativeElement.querySelector('.state-text') as HTMLElement;
    expect(stateText.textContent).toContain('State: Active');
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
      category: 'work'
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
      category: 'work'
    });
    expect(component.tasks[0].title).toBe('Plan sprint backlog updated');
    expect(component.editingTaskId).toBeNull();
  });

  it('keeps user edits when API update fails', () => {
    taskService.updateTask.and.returnValue(throwError(() => ({
      title: 'Validation failed',
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
      category: 'work'
    });

    component.submitEdit();

    expect(component.saveErrorMessage).toBe('Validation failed');
    expect(component.fieldError('title')).toContain('required');
    expect(component.editForm.getRawValue().title).toBe('Potential new title');
  });

  it('toggles completion and updates summary counts from server-confirmed result', () => {
    const task = component.tasks[0];

    component.toggleCompletion(task, true);

    expect(taskService.toggleTaskCompletion).toHaveBeenCalled();
    expect(component.tasks[0].isCompleted).toBeTrue();
    expect(component.activeCount).toBe(0);
    expect(component.completedCount).toBe(1);
    expect(component.liveMessage).toContain('marked completed');
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
      ...task,
      isCompleted: true,
      updatedAtUtc: '2026-04-26T09:15:03Z'
    });
    pending.complete();

    expect(component.isToggleInFlight(task.id)).toBeFalse();
  });
});
