import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface ProductFeature {
  title: string;
  summary: string;
  badge: string;
}

interface ProductHighlight {
  title: string;
  value: string;
  detail: string;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent {
  readonly year = new Date().getFullYear();

  readonly highlights: ReadonlyArray<ProductHighlight> = [
    {
      title: 'Version',
      value: 'v1.0',
      detail: 'Production-ready release with a complete personal productivity loop.'
    },
    {
      title: 'Core Modules',
      value: '6',
      detail: 'Auth, tasks, progress, momentum dashboard, leaderboards, profile settings.'
    },
    {
      title: 'UX States',
      value: '100%',
      detail: 'Purpose-built empty, loading, and error states for key pages.'
    }
  ];

  readonly features: ReadonlyArray<ProductFeature> = [
    {
      title: 'Secure account system',
      summary: 'Registration, login, password recovery, role policies, and session lifecycle controls.',
      badge: 'Security'
    },
    {
      title: 'Task command center',
      summary: 'Create, organize, complete, edit, and safely delete tasks with strong UI feedback loops.',
      badge: 'Tasks'
    },
    {
      title: 'XP and streak engine',
      summary: 'Deterministic streak rules and idempotent completion processing keep scoring consistent.',
      badge: 'Progress'
    },
    {
      title: 'Momentum dashboard',
      summary: 'Track XP, streaks, and recent trend snapshots to understand execution over time.',
      badge: 'Insights'
    },
    {
      title: 'Leaderboards and profiles',
      summary: 'Community ranking views and profile previews help benchmark progress and motivation.',
      badge: 'Social'
    },
    {
      title: 'Operational visibility',
      summary: 'Support diagnostics and suspicious case surfaces for trustworthy platform operations.',
      badge: 'Reliability'
    }
  ];
}
