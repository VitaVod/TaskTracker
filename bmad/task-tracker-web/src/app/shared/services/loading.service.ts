import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private activeRequests = 0;
  private readonly loadingSubject = new BehaviorSubject<boolean>(false);

  readonly isLoading$ = this.loadingSubject.asObservable();

  start(): void {
    this.activeRequests += 1;
    if (this.activeRequests === 1) {
      this.loadingSubject.next(true);
    }
  }

  stop(): void {
    if (this.activeRequests === 0) {
      return;
    }

    this.activeRequests -= 1;
    if (this.activeRequests === 0) {
      this.loadingSubject.next(false);
    }
  }
}
