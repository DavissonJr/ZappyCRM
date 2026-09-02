import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WhatsAppConnectionService } from '../../core/services/whatsapp-connection.service';
import { ToastService } from '../../core/services/toast.service';
import { WhatsAppConnection } from '../../core/models/whatsapp-connection.model';
import { PhoneFormatPipe } from '../../shared/pipes/phone-format.pipe';

@Component({
  selector: 'app-whatsapp-connections',
  standalone: true,
  imports: [CommonModule, FormsModule, PhoneFormatPipe],
  templateUrl: './whatsapp-connections.component.html',
  styleUrl: './whatsapp-connections.component.scss',
})
export class WhatsAppConnectionsComponent implements OnInit {
  private service = inject(WhatsAppConnectionService);
  private toast = inject(ToastService);

  connections = signal<WhatsAppConnection[]>([]);
  newLabel = signal('');
  creating = signal(false);
  qrCodeFor = signal<string | null>(null);
  qrCodeBase64 = signal<string | null>(null);
  loadingQr = signal(false);
  qrError = signal<string | null>(null);

  deleting = signal<string | null>(null);
  confirmDeleteFor = signal<WhatsAppConnection | null>(null);
  confirmDisconnectFor = signal<WhatsAppConnection | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.getAll().subscribe({
      next: (data) => this.connections.set(data),
      error: () => this.toast.error('Não foi possível carregar seus números. Recarregue a página.'),
    });
  }

  create(): void {
    const label = this.newLabel().trim();
    if (!label) {
      this.toast.error('Dê um nome para o número antes de continuar.');
      return;
    }

    this.creating.set(true);
    this.service.create(label).subscribe({
      next: (conn) => {
        this.connections.update((list) => [...list, conn]);
        this.newLabel.set('');
        this.creating.set(false);
        this.toast.success(`Número "${conn.label}" criado. Escaneie o QR code para conectar.`);
        this.showQrCode(conn);
      },
      error: (err) => {
        this.creating.set(false);
        this.toast.error(err?.error?.message ?? 'Não foi possível criar o número. Tente novamente.');
      },
    });
  }

  showQrCode(conn: WhatsAppConnection): void {
    this.qrCodeFor.set(conn.id);
    this.loadingQr.set(true);
    this.qrCodeBase64.set(null);
    this.qrError.set(null);

    this.service.getQrCode(conn.id).subscribe({
      next: (res) => {
        this.qrCodeBase64.set(res.qrCodeBase64);
        this.loadingQr.set(false);
      },
      error: (err) => {
        this.loadingQr.set(false);
        this.qrError.set(err?.error?.message ?? 'Não foi possível gerar o QR code. Tente novamente.');
      },
    });
  }

  closeQrCode(): void {
    this.qrCodeFor.set(null);
    this.qrCodeBase64.set(null);
    this.qrError.set(null);
  }

  checkStatus(conn: WhatsAppConnection): void {
    this.service.refreshStatus(conn.id).subscribe({
      next: (res) => {
        this.connections.update((list) =>
          list.map((c) => (c.id === conn.id ? { ...c, isConnected: res.isConnected } : c)),
        );
        if (res.isConnected) {
          this.toast.success(`"${conn.label}" está conectado!`);
          this.closeQrCode();
        } else {
          this.toast.info('Ainda não conectado. Escaneie o QR code.');
        }
      },
      error: () => this.toast.error('Não foi possível checar o status agora.'),
    });
  }

  askDisconnect(conn: WhatsAppConnection): void {
    this.confirmDisconnectFor.set(conn);
  }

  cancelDisconnect(): void {
    this.confirmDisconnectFor.set(null);
  }

  confirmDisconnect(): void {
    const conn = this.confirmDisconnectFor();
    if (!conn) return;

    this.confirmDisconnectFor.set(null);
    this.service.disconnect(conn.id).subscribe({
      next: () => {
        this.connections.update((list) =>
          list.map((c) => (c.id === conn.id ? { ...c, isConnected: false, phoneNumber: undefined } : c)),
        );
        this.toast.success(`"${conn.label}" foi desconectado.`);
      },
      error: (err) => this.toast.error(err?.error?.message ?? 'Não foi possível desconectar.'),
    });
  }

  askDelete(conn: WhatsAppConnection): void {
    this.confirmDeleteFor.set(conn);
  }

  cancelDelete(): void {
    this.confirmDeleteFor.set(null);
  }

  confirmDelete(): void {
    const conn = this.confirmDeleteFor();
    if (!conn) return;

    this.confirmDeleteFor.set(null);
    this.deleting.set(conn.id);
    this.service.delete(conn.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.connections.update((list) => list.filter((c) => c.id !== conn.id));
        this.toast.success(`"${conn.label}" foi removido.`);
      },
      error: (err) => {
        this.deleting.set(null);
        this.toast.error(err?.error?.message ?? 'Não foi possível remover esse número.');
      },
    });
  }
}
