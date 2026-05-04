import { Routes } from '@angular/router';
import { authGuard } from './shared/guards/auth.guard';
import { adminGuard } from './shared/guards/admin.guard';
import { supportGuard } from './shared/guards/support.guard';
import { redirectAuthenticatedToDashboardGuard } from './shared/guards/redirect-authenticated.guard';
import { ForgotPasswordComponent, LoginComponent, RegisterComponent, ResetPasswordComponent } from './features/auth';
import { AccountSettingsComponent, MyProfilePreviewComponent } from './features/account';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { DayDetailComponent } from './features/dashboard/day-detail.component';
import { CreateTaskComponent, TaskListComponent } from './features/tasks';
import { LeaderboardComponent, PublicProfileComponent } from './features/leaderboards';
import { OpsSuspiciousCasesComponent } from './features/ops-suspicious-cases/ops-suspicious-cases.component';
import { SupportDiagnosticsComponent } from './features/support-diagnostics/support-diagnostics.component';
import { LandingComponent } from './features/landing/landing.component';

export const routes: Routes = [
	{
		path: '',
		component: LandingComponent
	},
	{
		path: 'landing',
		component: LandingComponent
	},
	{
		path: 'login',
		component: LoginComponent,
		canActivate: [redirectAuthenticatedToDashboardGuard]
	},
	{
		path: 'register',
		component: RegisterComponent,
		canActivate: [redirectAuthenticatedToDashboardGuard]
	},
	{
		path: 'forgot-password',
		component: ForgotPasswordComponent,
		canActivate: [redirectAuthenticatedToDashboardGuard]
	},
	{
		path: 'reset-password',
		component: ResetPasswordComponent,
		canActivate: [redirectAuthenticatedToDashboardGuard]
	},
	{
		path: 'dashboard',
		component: DashboardComponent,
		canActivate: [authGuard]
	},
	{
		path: 'momentum',
		component: DashboardComponent,
		canActivate: [authGuard]
	},
	{
		path: 'dashboard/day/:date',
		component: DayDetailComponent,
		canActivate: [authGuard]
	},
	{
		path: 'account',
		component: AccountSettingsComponent,
		canActivate: [authGuard]
	},
	{
		path: 'profile/public/:handle',
		component: PublicProfileComponent,
		canActivate: [authGuard]
	},
	{
		path: 'profile',
		component: AccountSettingsComponent,
		canActivate: [authGuard]
	},
	{
		path: 'my-profile',
		component: MyProfilePreviewComponent,
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
		path: 'leaderboard',
		component: LeaderboardComponent,
		canActivate: [authGuard]
	},
	{
		path: 'ops/suspicious-cases',
		component: OpsSuspiciousCasesComponent,
		canActivate: [authGuard, adminGuard]
	},
	{
		path: 'ops/support/diagnostics',
		component: SupportDiagnosticsComponent,
		canActivate: [authGuard, supportGuard]
	},
	{
		path: '**',
		redirectTo: ''
	}
];
