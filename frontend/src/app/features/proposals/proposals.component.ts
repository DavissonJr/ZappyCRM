import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProposalService } from '../../core/services/proposal.service';
import { ToastService } from '../../core/services/toast.service';
import { Proposal } from '../../core/models/proposal.model';
import { PhoneFormatPipe } from '../../shared/pipes/phone-format.pipe';

const STATUS_LABELS: Record<string, string> = {
  Draft: 'Rascunho',
  SentToClient: 'Enviada',
  Accepted: 'Aceita',
  Rejected: 'Recusada',
  Expired: 'Expirada',
};

@Component({
  selector: 'app-proposals',
  standalone: true,
  imports: [CommonModule, FormsModule, PhoneFormatPipe],
  templateUrl: './proposals.component.html',
  styleUrl: './proposals.component.scss',
})
export class ProposalsComponent implements OnInit {
  private service = inject(ProposalService);
  private toast = inject(ToastService);

  statusLabels = STATUS_LABELS;
  proposals = signal<Proposal[]>([]);

  editingProposal = signal<Proposal | null>(null);
  editTitle = signal('');
  editDescription = signal('');
  editValue = signal<number | null>(null);
  saving = signal(false);
  sendingId = signal<string | null>(null);

  confirmDeleteFor = signal<Proposal | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.getAll().subscribe({
      next: (data) => this.proposals.set(data),
      error: () => this.toast.error('Não foi possível carregar as propostas.'),
    });
  }

  openEdit(proposal: Proposal): void {
    this.editingProposal.set(proposal);
    this.editTitle.set(proposal.title);
    this.editDescription.set(proposal.description);
    this.editValue.set(proposal.value ?? null);
  }

  closeEdit(): void {
    this.editingProposal.set(null);
  }

  saveEdit(): void {
    const proposal = this.editingProposal();
    if (!proposal) return;

    this.saving.set(true);
    this.service
      .update(proposal.id, {
        title: this.editTitle(),
        description: this.editDescription(),
        value: this.editValue() ?? undefined,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.editingProposal.set(null);
          this.toast.success('Proposta atualizada.');
          this.load();
        },
        error: () => {
          this.saving.set(false);
          this.toast.error('Não foi possível salvar as alterações.');
        },
      });
  }

  sendProposal(proposal: Proposal): void {
    this.sendingId.set(proposal.id);
    this.service.send(proposal.id).subscribe({
      next: () => {
        this.sendingId.set(null);
        this.toast.success('Proposta enviada pelo WhatsApp.');
        this.load();
      },
      error: (err) => {
        this.sendingId.set(null);
        this.toast.error(err?.error?.message ?? 'Não foi possível enviar a proposta.');
      },
    });
  }

  markAccepted(proposal: Proposal): void {
    this.service.updateStatus(proposal.id, 'Accepted').subscribe({
      next: () => { this.toast.success('Marcada como aceita.'); this.load(); },
      error: () => this.toast.error('Não foi possível atualizar.'),
    });
  }

  markRejected(proposal: Proposal): void {
    this.service.updateStatus(proposal.id, 'Rejected').subscribe({
      next: () => { this.toast.success('Marcada como recusada.'); this.load(); },
      error: () => this.toast.error('Não foi possível atualizar.'),
    });
  }

  askDelete(proposal: Proposal): void {
    this.confirmDeleteFor.set(proposal);
  }

  cancelDeleteDialog(): void {
    this.confirmDeleteFor.set(null);
  }

  confirmDelete(): void {
    const proposal = this.confirmDeleteFor();
    if (!proposal) return;

    this.confirmDeleteFor.set(null);
    this.service.delete(proposal.id).subscribe({
      next: () => { this.toast.success('Proposta removida.'); this.load(); },
      error: () => this.toast.error('Não foi possível remover.'),
    });
  }
}
