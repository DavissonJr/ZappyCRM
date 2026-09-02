import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiAgentConfig, AiCreditsStatus, Me, TeamMember, TenantSettings } from '../models/settings.model';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  // Perfil
  getMe(): Observable<Me> {
    return this.http.get<Me>(`${this.base}/api/account/me`);
  }
  updateProfile(payload: { fullName: string; email: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/api/account/me`, payload);
  }
  changePassword(payload: { currentPassword: string; newPassword: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/api/account/change-password`, payload);
  }

  // Empresa
  getTenant(): Observable<TenantSettings> {
    return this.http.get<TenantSettings>(`${this.base}/api/tenant`);
  }
  updateTenant(payload: { name: string; segment: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/api/tenant`, payload);
  }

  // Agente de IA
  getAiAgentConfig(): Observable<AiAgentConfig> {
    return this.http.get<AiAgentConfig>(`${this.base}/api/ai-agent-config`);
  }
  updateAiAgentConfig(payload: AiAgentConfig): Observable<void> {
    return this.http.put<void>(`${this.base}/api/ai-agent-config`, payload);
  }

  // Equipe
  getTeam(): Observable<TeamMember[]> {
    return this.http.get<TeamMember[]>(`${this.base}/api/team`);
  }
  inviteTeamMember(payload: { fullName: string; email: string; temporaryPassword: string }): Observable<void> {
    return this.http.post<void>(`${this.base}/api/team`, payload);
  }
  setTeamMemberActive(id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`${this.base}/api/team/${id}/active`, { isActive });
  }

  // Créditos de IA (só leitura — uso do mês contra o limite do plano)
  getAiUsage(): Observable<AiCreditsStatus> {
    return this.http.get<AiCreditsStatus>(`${this.base}/api/ai-usage`);
  }
}
