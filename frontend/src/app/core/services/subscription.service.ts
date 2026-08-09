import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SubscriptionStatus } from '../models/subscription.model';

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/subscription`;

  get(): Observable<SubscriptionStatus> {
    return this.http.get<SubscriptionStatus>(this.baseUrl);
  }

  createCheckout(plan: string): Observable<{ checkoutUrl: string }> {
    return this.http.post<{ checkoutUrl: string }>(`${this.baseUrl}/checkout`, { plan });
  }

  cancel(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/cancel`, {});
  }
}
