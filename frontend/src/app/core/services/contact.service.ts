import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ContactListItem } from '../models/contact.model';

export interface ContactFilters {
  search?: string;
  noConversationInLastDays?: number;
  noAppointmentInLastDays?: number;
}

@Injectable({ providedIn: 'root' })
export class ContactService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/contacts`;

  getAll(filters?: ContactFilters): Observable<ContactListItem[]> {
    const params: Record<string, string> = {};
    if (filters?.search) params['search'] = filters.search;
    if (filters?.noConversationInLastDays) params['noConversationInLastDays'] = String(filters.noConversationInLastDays);
    if (filters?.noAppointmentInLastDays) params['noAppointmentInLastDays'] = String(filters.noAppointmentInLastDays);

    return this.http.get<ContactListItem[]>(this.baseUrl, { params });
  }

  update(id: string, payload: { name?: string; notes?: string; isBlocked: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, payload);
  }
}
