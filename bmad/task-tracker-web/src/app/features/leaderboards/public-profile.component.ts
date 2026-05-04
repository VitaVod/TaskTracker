import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicProfileResponse } from '../../shared/models/leaderboard.models';
import { LeaderboardService } from '../../shared/services/leaderboard.service';

type PublicProfileState = 'loading' | 'public' | 'anonymous' | 'error';

@Component({
  selector: 'app-public-profile',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './public-profile.component.html',
  styleUrl: './public-profile.component.scss'
})
export class PublicProfileComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly leaderboardService = inject(LeaderboardService);

  state: PublicProfileState = 'loading';
  profile: PublicProfileResponse | null = null;
  errorMessage = '';
  errorSupportText = '';

  constructor() {
    this.loadProfile();
  }

  retry(): void {
    this.loadProfile();
  }

  private loadProfile(): void {
    const handle = this.route.snapshot.paramMap.get('handle')?.trim() ?? '';
    if (!handle) {
      this.state = 'anonymous';
      this.profile = {
        visibility: 'anonymous',
        publicIdentity: null,
        avatarMarker: null,
        statistics: null,
        message: 'This participant keeps leaderboard participation anonymous. Public statistics are unavailable.'
      };
      return;
    }

    this.state = 'loading';
    this.errorMessage = '';
    this.errorSupportText = '';

    this.leaderboardService.getPublicProfile(handle).subscribe({
      next: (response) => {
        this.profile = response;
        this.state = response.visibility === 'public' ? 'public' : 'anonymous';
      },
      error: (problem) => {
        this.state = 'error';
        this.errorMessage = 'Unable to load this profile right now.';
        const support = [problem.code, problem.traceId].filter((value: string | undefined): value is string => Boolean(value));
        this.errorSupportText = support.length > 0 ? `Support: ${support.join(' | ')}` : '';
      }
    });
  }
}
