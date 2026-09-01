import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services/admin.service';
import { ToastService } from '../../core/services/toast.service';
import { AdminPlanOption, AdminTenantSummary } from '../../core/models/admin.model';

const SEGMENTS = [
  { value: 'clinica', label: 'Clínica' },
  { value: 'oficina', label: 'Oficina mecânica' },
  { value: 'advocacia', label: 'Escritório de advocacia' },
  { value: 'imobiliaria', label: 'Imobiliária' },
  { value: 'outro', label: 'Outro' },
];

function generatePassword(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
  let result = '';
  for (let i = 0; i < 10; i++) result += chars.charAt(Math.floor(Math.random() * chars.length));
  return result;
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss',
})
export class AdminComponent implements OnInit {
  private service = inject(AdminService);
  private toast = inject(ToastService);

  segments = SEGMENTS;

  tenants = signal<AdminTenantSummary[]>([]);
  plans = signal<AdminPlanOption[]>([]);
  loading = signal(true);

  // Criar empresa
  showCreateForm = signal(false);
  creating = signal(false);
  newCompanyName = signal('');
  newSegment = signal('clinica');
  newPlan = signal('Trial');
  newOwnerFullName = signal('');
  newOwnerEmail = signal('');
  newTemporaryPassword = signal(generatePassword());

  // Feedback pós-criação (mostra a senha uma vez só, pra você copiar/repassar)
  justCreated = signal<{ companyName: string; email: string; password: string } | null>(null);

  ngOnInit(): void {
    this.load();
    this.service.getPlans().subscribe((data) => this.plans.set(data));
  }

  load(): void {
    this.loading.set(true);
    this.service.getTenants().subscribe({
      next: (data) => {
        this.tenants.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Não foi possível carregar as empresas.');
      },
    });
  }

  toggleActive(tenant: AdminTenantSummary): void {
    const next = !tenant.isActive;
    const action = next ? 'reativar' : 'suspender';
    if (!confirm(`Tem certeza que quer ${action} "${tenant.name}"?`)) return;

    this.service.setTenantActive(tenant.id, next).subscribe({
      next: () => {
        this.toast.success(next ? 'Empresa reativada.' : 'Empresa suspensa.');
        this.load();
      },
      error: () => this.toast.error('Não foi possível atualizar essa empresa.'),
    });
  }

  changePlan(tenant: AdminTenantSummary, newPlanTier: string): void {
    this.service.updateTenantPlan(tenant.id, newPlanTier).subscribe({
      next: () => {
        this.toast.success(`Plano de "${tenant.name}" atualizado.`);
        this.load();
      },
      error: () => this.toast.error('Não foi possível trocar o plano.'),
    });
  }

  activeCount(): number {
    return this.tenants().filter((t) => t.isActive).length;
  }

  totalAiCost(): number {
    return this.tenants().reduce((sum, t) => sum + t.totalAiEstimatedCostUsd, 0);
  }

  totalContacts(): number {
    return this.tenants().reduce((sum, t) => sum + t.contactCount, 0);
  }

  // ---- Criar empresa ----
  openCreateForm(): void {
    this.newCompanyName.set('');
    this.newSegment.set('clinica');
    this.newPlan.set('Trial');
    this.newOwnerFullName.set('');
    this.newOwnerEmail.set('');
    this.newTemporaryPassword.set(generatePassword());
    this.justCreated.set(null);
    this.showCreateForm.set(true);
  }

  closeCreateForm(): void {
    this.showCreateForm.set(false);
  }

  regeneratePassword(): void {
    this.newTemporaryPassword.set(generatePassword());
  }

  submitCreateTenant(): void {
    if (!this.newCompanyName().trim() || !this.newOwnerFullName().trim() || !this.newOwnerEmail().trim()) {
      this.toast.error('Preenche nome da empresa, nome do dono e e-mail.');
      return;
    }

    this.creating.set(true);
    this.service
      .createTenant({
        companyName: this.newCompanyName(),
        segment: this.newSegment(),
        plan: this.newPlan(),
        ownerFullName: this.newOwnerFullName(),
        ownerEmail: this.newOwnerEmail(),
        temporaryPassword: this.newTemporaryPassword(),
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.showCreateForm.set(false);
          this.justCreated.set({
            companyName: this.newCompanyName(),
            email: this.newOwnerEmail(),
            password: this.newTemporaryPassword(),
          });
          this.toast.success('Empresa criada com sucesso.');
          this.load();
        },
        error: (err) => {
          this.creating.set(false);
          this.toast.error(err?.error?.message ?? 'Não foi possível criar a empresa.');
        },
      });
  }

  dismissJustCreated(): void {
    this.justCreated.set(null);
  }
}
