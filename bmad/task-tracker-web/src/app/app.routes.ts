import { Routes } from '@angular/router';
import { authGuard } from './shared/guards/auth.guard';
import { ForgotPasswordComponent, LoginComponent, RegisterComponent, ResetPasswordComponent } from './features/auth';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AccountSettingsComponent } from './features/account/account-settings.component';
import { CreateTaskComponent, TaskListComponent } from './features/tasks';
import { LeaderboardComponent } from './features/leaderboards';

export const routes: Routes = [
	{
		path: '',
		pathMatch: 'full',
		redirectTo: 'login'
	},
	{
		path: 'login',
		component: LoginComponent
	},
	{
		path: 'register',
		component: RegisterComponent
	},
	{
		path: 'forgot-password',
		component: ForgotPasswordComponent
	},
	{
		path: 'reset-password',
		component: ResetPasswordComponent
	},
	{
		path: 'dashboard',
		component: DashboardComponent,
		canActivate: [authGuard]
	},
	{
		path: 'account',
		component: AccountSettingsComponent,
		canActivate: [authGuard]
	},
	{
		path: 'tasks',
		component: TaskListComponent,
		canActivate: [authGuard]
	},
	{
		path: 'tasks/new',
		component: CreateTaskComponent,
		canActivate: [authGuard]
	},
	{
		path: 'leaderboards',
		component: LeaderboardComponent,
		canActivate: [authGuard]
	},
	{
		path: '**',
		redirectTo: 'login'
	}
];
