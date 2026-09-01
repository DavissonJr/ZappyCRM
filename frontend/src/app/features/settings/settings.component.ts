import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../core/services/settings.service';
import { AiAgentConfig, AiUsageSummary, Me, TeamMember, TenantSettings } from '../../core/models/settings.model';

type SettingsTab = 'perfil' | 'empresa' | 'ia' | 'creditos' | 'equipe';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  private service = inject(SettingsService);

  activeTab = signal<SettingsTab>('perfil');

  // Perfil
  me = signal<Me | null>(null);
  profileFullName = signal('');
  profileEmail = signal('');
  savingProfile = signal(false);
  profileMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  changingPassword = signal(false);
  passwordMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Empresa
  tenant = signal<TenantSettings | null>(null);
  companyName = signal('');
  companySegment = signal('outro');
  savingCompany = signal(false);
  companyMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  segments = [
    { value: 'clinica', label: 'Clínica' },
    { value: 'oficina', label: 'Oficina mecânica' },
    { value: 'advocacia', label: 'Escritório de advocacia' },
    { value: 'imobiliaria', label: 'Imobiliária' },
    { value: 'outro', label: 'Outro' },
  ];

  // Agente de IA
  aiConfig = signal<AiAgentConfig | null>(null);
  aiAgentName = signal('');
  aiSystemPrompt = signal('');
  aiAutoReply = signal(true);
  aiRequireApproval = signal(true);
  aiBusinessHours = signal('08:00-18:00');
  aiFallbackMessage = signal('');
  savingAi = signal(false);
  aiMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Chave da Anthropic (por tenant)
  anthropicHasKey = signal(false);
  anthropicKeyPreview = signal<string | undefined>(undefined);
  anthropicKeyInput = signal('');
  showAnthropicKeyForm = signal(false);
  savingAnthropicKey = signal(false);
  anthropicKeyMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Equipe
  team = signal<TeamMember[]>([]);
  showInviteForm = signal(false);
  inviteFullName = signal('');
  inviteEmail = signal('');
  invitePassword = signal('');
  inviting = signal(false);
  teamMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Créditos de IA (só leitura)
  aiUsage = signal<AiUsageSummary | null>(null);

  ngOnInit(): void {
    this.loadProfile();
    this.loadCompany();
    this.loadAiConfig();
    this.loadTeam();
    this.loadAiUsage();
  }

  setTab(tab: SettingsTab): void {
    this.activeTab.set(tab);
  }

  // ---- Perfil ----
  loadProfile(): void {
    this.service.getMe().subscribe((me) => {
      this.me.set(me);
      this.profileFullName.set(me.fullName);
      this.profileEmail.set(me.email);
    });
  }

  saveProfile(): void {
    this.savingProfile.set(true);
    this.profileMessage.set(null);
    this.service.updateProfile({ fullName: this.profileFullName(), email: this.profileEmail() }).subscribe({
      next: () => {
        this.savingProfile.set(false);
        this.profileMessage.set({ type: 'success', text: 'Perfil atualizado.' });
        this.loadProfile();
      },
      error: (err) => {
        this.savingProfile.set(false);
        this.profileMessage.set({ type: 'error', text: err?.error?.message ?? 'Não foi possível salvar.' });
      },
    });
  }

  savePassword(): void {
    if (this.newPassword() !== this.confirmPassword()) {
      this.passwordMessage.set({ type: 'error', text: 'As senhas novas não coincidem.' });
      return;
    }
    if (this.newPassword().length < 6) {
      this.passwordMessage.set({ type: 'error', text: 'A nova senha precisa ter pelo menos 6 caracteres.' });
      return;
    }

    this.changingPassword.set(true);
    this.passwordMessage.set(null);
    this.service
      .changePassword({ currentPassword: this.currentPassword(), newPassword: this.newPassword() })
      .subscribe({
        next: () => {
          this.changingPassword.set(false);
          this.passwordMessage.set({ type: 'success', text: 'Senha alterada com sucesso.' });
          this.currentPassword.set('');
          this.newPassword.set('');
          this.confirmPassword.set('');
        },
        error: (err) => {
          this.changingPassword.set(false);
          this.passwordMessage.set({ type: 'error', text: err?.error?.message ?? 'Não foi possível trocar a senha.' });
        },
      });
  }

  // ---- Empresa ----
  loadCompany(): void {
    this.service.getTenant().subscribe((tenant) => {
      this.tenant.set(tenant);
      this.companyName.set(tenant.name);
      this.companySegment.set(tenant.segment);
    });
  }

  saveCompany(): void {
    this.savingCompany.set(true);
    this.companyMessage.set(null);
    this.service.updateTenant({ name: this.companyName(), segment: this.companySegment() }).subscribe({
      next: () => {
        this.savingCompany.set(false);
        this.companyMessage.set({ type: 'success', text: 'Dados da empresa atualizados.' });
      },
      error: () => {
        this.savingCompany.set(false);
        this.companyMessage.set({ type: 'error', text: 'Não foi possível salvar.' });
      },
    });
  }

  // ---- Agente de IA ----
  loadAiConfig(): void {
    this.service.getAiAgentConfig().subscribe((config) => {
      this.aiConfig.set(config);
      this.aiAgentName.set(config.agentName);
      this.aiSystemPrompt.set(config.systemPrompt);
      this.aiAutoReply.set(config.autoReplyEnabled);
      this.aiRequireApproval.set(config.requireHumanApproval);
      this.aiBusinessHours.set(config.businessHours);
      this.aiFallbackMessage.set(config.fallbackMessage ?? '');
      this.anthropicHasKey.set(config.hasAnthropicApiKey);
      this.anthropicKeyPreview.set(config.anthropicApiKeyPreview);
    });
  }

  saveAiConfig(): void {
    this.savingAi.set(true);
    this.aiMessage.set(null);
    this.service
      .updateAiAgentConfig({
        agentName: this.aiAgentName(),
        systemPrompt: this.aiSystemPrompt(),
        autoReplyEnabled: this.aiAutoReply(),
        requireHumanApproval: this.aiRequireApproval(),
        businessHours: this.aiBusinessHours(),
        fallbackMessage: this.aiFallbackMessage() || undefined,
      })
      .subscribe({
        next: () => {
          this.savingAi.set(false);
          this.aiMessage.set({ type: 'success', text: 'Configuração do assistente salva.' });
        },
        error: () => {
          this.savingAi.set(false);
          this.aiMessage.set({ type: 'error', text: 'Não foi possível salvar.' });
        },
      });
  }

  // ---- Equipe ----
  loadTeam(): void {
    this.service.getTeam().subscribe((data) => this.team.set(data));
  }

  openInviteForm(): void {
    this.inviteFullName.set('');
    this.inviteEmail.set('');
    this.invitePassword.set('');
    this.teamMessage.set(null);
    this.showInviteForm.set(true);
  }

  closeInviteForm(): void {
    this.showInviteForm.set(false);
  }

  inviteMember(): void {
    if (!this.inviteFullName() || !this.inviteEmail() || this.invitePassword().length < 6) {
      this.teamMessage.set({ type: 'error', text: 'Preencha nome, e-mail e uma senha com pelo menos 6 caracteres.' });
      return;
    }

    this.inviting.set(true);
    this.teamMessage.set(null);
    this.service
      .inviteTeamMember({
        fullName: this.inviteFullName(),
        email: this.inviteEmail(),
        temporaryPassword: this.invitePassword(),
      })
      .subscribe({
        next: () => {
          this.inviting.set(false);
          this.showInviteForm.set(false);
          this.teamMessage.set({ type: 'success', text: 'Atendente adicionado. Compartilhe a senha temporária com ele(a).' });
          this.loadTeam();
        },
        error: (err) => {
          this.inviting.set(false);
          this.teamMessage.set({ type: 'error', text: err?.error?.message ?? 'Não foi possível adicionar.' });
        },
      });
  }

  toggleActive(member: TeamMember): void {
    this.service.setTeamMemberActive(member.id, !member.isActive).subscribe({
      next: () => this.loadTeam(),
      error: () => this.teamMessage.set({ type: 'error', text: 'Não foi possível atualizar esse membro.' }),
    });
  }

  // ---- Chave da Anthropic ----
  openAnthropicKeyForm(): void {
    this.anthropicKeyInput.set('');
    this.anthropicKeyMessage.set(null);
    this.showAnthropicKeyForm.set(true);
  }

  cancelAnthropicKeyForm(): void {
    this.showAnthropicKeyForm.set(false);
  }

  saveAnthropicKey(): void {
    const key = this.anthropicKeyInput().trim();
    if (key.length < 10) {
      this.anthropicKeyMessage.set({ type: 'error', text: 'Cole uma chave válida (começa com "sk-ant-").' });
      return;
    }

    this.savingAnthropicKey.set(true);
    this.anthropicKeyMessage.set(null);
    this.service.setAnthropicApiKey(key).subscribe({
      next: () => {
        this.savingAnthropicKey.set(false);
        this.showAnthropicKeyForm.set(false);
        this.aiMessage.set({ type: 'success', text: 'Chave da Anthropic salva com sucesso.' });
        this.loadAiConfig();
      },
      error: (err) => {
        this.savingAnthropicKey.set(false);
        this.anthropicKeyMessage.set({ type: 'error', text: err?.error?.message ?? 'Não foi possível salvar a chave.' });
      },
    });
  }

  removeAnthropicKey(): void {
    if (!confirm('Remover a chave da Anthropic? A IA para de responder automaticamente até você cadastrar uma nova.')) return;

    this.service.removeAnthropicApiKey().subscribe({
      next: () => {
        this.aiMessage.set({ type: 'success', text: 'Chave removida.' });
        this.loadAiConfig();
      },
      error: () => this.aiMessage.set({ type: 'error', text: 'Não foi possível remover a chave.' }),
    });
  }

  // ---- Créditos de IA ----
  loadAiUsage(): void {
    this.service.getAiUsage().subscribe((data) => this.aiUsage.set(data));
  }
}
