import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminPlanOption, AdminTenantSummary } from '../models/admin.model';

export interface CreateTenantPayload {
  companyName: string;
  segment: string;
  plan: string;
  ownerFullName: string;
  ownerEmail: string;
  temporaryPassword: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/admin`;

  getTenants(): Observable<AdminTenantSummary[]> {
    return this.http.get<AdminTenantSummary[]>(`${this.baseUrl}/tenants`);
  }

  getPlans(): Observable<AdminPlanOption[]> {
    return this.http.get<AdminPlanOption[]>(`${this.baseUrl}/plans`);
  }

  createTenant(payload: CreateTenantPayload): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/tenants`, payload);
  }

  updateTenantPlan(id: string, plan: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/tenants/${id}/plan`, { plan });
  }

  setTenantActive(id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/tenants/${id}/active`, { isActive });
  }
}
