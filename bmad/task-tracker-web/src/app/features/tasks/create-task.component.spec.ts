import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TaskService } from '../../shared/services/task.service';
import { CreateTaskComponent } from './create-task.component';

describe('CreateTaskComponent', () => {
  let fixture: ComponentFixture<CreateTaskComponent>;
  let component: CreateTaskComponent;
  let taskService: jasmine.SpyObj<TaskService>;
  let router: Router;

  beforeEach(async () => {
    taskService = jasmine.createSpyObj<TaskService>('TaskService', ['createTask']);

    await TestBed.configureTestingModule({
      imports: [CreateTaskComponent],
      providers: [
        { provide: TaskService, useValue: taskService },
        provideRouter([])
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(CreateTaskComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('marks form touched and skips submit when invalid', async () => {
    await component.submit();

    expect(component.form.touched).toBeTrue();
    expect(taskService.createTask).not.toHaveBeenCalled();
  });

  it('maps form values to create payload and navigates on success', async () => {
    const futureLocalDueAt = new Date();
    futureLocalDueAt.setDate(futureLocalDueAt.getDate() + 2);
    futureLocalDueAt.setHours(18, 0, 0, 0);
    const futureDueAtUtc = futureLocalDueAt.toISOString();

    taskService.createTask.and.returnValue(of({
      id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
      title: 'Plan sprint backlog',
      description: 'Draft story priorities',
      dueAtUtc: futureDueAtUtc,
      priority: 'medium',
      category: 'work',
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-25T11:30:12Z'
    }));

    component.form.setValue({
      title: ' Plan sprint backlog ',
      description: ' Draft story priorities ',
      dueAtUtc: `${futureLocalDueAt.getFullYear()}-${`${futureLocalDueAt.getMonth() + 1}`.padStart(2, '0')}-${`${futureLocalDueAt.getDate()}`.padStart(2, '0')}T18:00`,
      priority: 'medium',
      category: 'work'
    });

    await component.submit();

    expect(taskService.createTask).toHaveBeenCalled();
    const sentPayload = taskService.createTask.calls.mostRecent().args[0] as {
      title: string;
      description: string;
      dueAtUtc: string | null;
      priority: string;
      category: string;
    };

    expect(sentPayload.title).toBe('Plan sprint backlog');
    expect(sentPayload.description).toBe('Draft story priorities');
    expect(sentPayload.category).toBe('work');
    expect(sentPayload.priority).toBe('medium');
    expect(sentPayload.dueAtUtc).toBe(futureDueAtUtc);
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('shows Problem Details errors from API', async () => {
    taskService.createTask.and.returnValue(throwError(() => ({
      title: 'Validation failed',
      errors: {
        title: ['The title field is required.']
      }
    })));

    component.form.setValue({
      title: 'Task title',
      description: '',
      dueAtUtc: '',
      priority: 'medium',
      category: 'work'
    });

    await component.submit();

    expect(component.errorMessage).toBe('Validation failed');
    expect(component.fieldErrors['title'][0]).toContain('required');
  });
});