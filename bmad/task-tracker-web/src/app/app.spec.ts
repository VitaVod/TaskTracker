import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { AuthService } from './shared/services/auth.service';

@Component({ standalone: true, template: '<p>stub</p>' })
class StubPageComponent {}

describe('App', () => {
  let router: Router;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['logout']);
    authService.logout.and.returnValue(of({}));
    authService.isAuthenticated = jasmine.createSpy('isAuthenticated').and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        { provide: AuthService, useValue: authService },
        provideRouter([
          { path: '', component: StubPageComponent },
          { path: 'landing', component: StubPageComponent },
          { path: 'login', component: StubPageComponent },
          { path: 'register', component: StubPageComponent },
          { path: 'dashboard', component: StubPageComponent },
          { path: 'tasks', component: StubPageComponent },
          { path: 'momentum', component: StubPageComponent },
          { path: 'leaderboard', component: StubPageComponent },
          { path: 'my-profile', component: StubPageComponent },
          { path: 'profile', component: StubPageComponent }
        ])
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render router outlet host', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('renders primary tabs on authenticated app routes', async () => {
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/dashboard');
    fixture.detectChanges();

    const tabElements = fixture.nativeElement.querySelectorAll('.app-tab') as NodeListOf<HTMLElement>;
    const tabLabels = Array.from(tabElements).map((element) => {
      const textSpans = element.querySelectorAll('span');
      return textSpans.length > 1 ? textSpans[1].textContent?.trim() : element.textContent?.trim();
    });
    expect(tabLabels).toEqual(['Dashboard', 'Tasks', 'Leaderboard', 'My Profile', 'Settings']);

    const brand = fixture.nativeElement.querySelector('.app-brand') as HTMLAnchorElement | null;
    const logoutButton = fixture.nativeElement.querySelector('.logout-button') as HTMLButtonElement | null;
    expect(brand?.textContent?.trim()).toBe('TASKTRACKER v1.0');
    expect(logoutButton?.textContent).toContain('Logout');
  });

  it('keeps route state in sync when tabs navigate to a section', async () => {
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/tasks');
    fixture.detectChanges();

    const taskTab = Array.from(fixture.nativeElement.querySelectorAll('.app-tab') as NodeListOf<HTMLAnchorElement>)
      .find((element) => element.textContent?.includes('Tasks')) as HTMLAnchorElement | undefined;

    expect(router.url).toBe('/tasks');
    expect(taskTab).toBeDefined();
  });

  it('hides primary tabs on public auth routes', async () => {
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/login');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-tabs')).toBeNull();
  });

  it('hides primary tabs on the landing route', async () => {
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/landing');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-tabs')).toBeNull();
  });

  it('shows primary tabs on landing when user is authenticated', async () => {
    (authService.isAuthenticated as jasmine.Spy).and.returnValue(true);
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/landing');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-tabs')).not.toBeNull();
  });
});
