import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppointmentService } from '../../core/services/appointment.service';
import { WhatsAppConnectionService } from '../../core/services/whatsapp-connection.service';
import { ToastService } from '../../core/services/toast.service';
import { Appointment } from '../../core/models/appointment.model';
import { WhatsAppConnection } from '../../core/models/whatsapp-connection.model';
import { PhoneFormatPipe } from '../../shared/pipes/phone-format.pipe';
import { PhoneMaskDirective } from '../../shared/phone-mask.directive';

const STATUS_LABELS: Record<string, string> = {
  Scheduled: 'Agendado',
  Confirmed: 'Confirmado',
  Cancelled: 'Cancelado',
  Completed: 'Concluído',
  NoShow: 'Não compareceu',
};

// Sugestões prontas — mas o usuário pode adicionar qualquer valor customizado
// em minutos também (útil pra testar sem precisar esperar 1 hora).
const REMINDER_PRESETS = [
  { minutes: 1440, label: '1 dia antes' },
  { minutes: 180, label: '3 horas antes' },
  { minutes: 60, label: '1 hora antes' },
  { minutes: 10, label: '10 min antes' },
];

function formatMinutesLabel(minutes: number): string {
  if (minutes % 1440 === 0) return `${minutes / 1440} dia(s) antes`;
  if (minutes % 60 === 0) return `${minutes / 60}h antes`;
  return `${minutes} min antes`;
}

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [CommonModule, FormsModule, PhoneMaskDirective, PhoneFormatPipe],
  templateUrl: './appointments.component.html',
  styleUrl: './appointments.component.scss',
})
export class AppointmentsComponent implements OnInit {
  private service = inject(AppointmentService);
  private connectionService = inject(WhatsAppConnectionService);
  private toast = inject(ToastService);

  statusLabels = STATUS_LABELS;
  reminderPresets = REMINDER_PRESETS;
  formatMinutesLabel = formatMinutesLabel;

  appointments = signal<Appointment[]>([]);
  connections = signal<WhatsAppConnection[]>([]);

  showForm = signal(false);
  formConnectionId = signal('');
  formPhoneNumber = signal('');
  formContactName = signal('');
  formTitle = signal('');
  formDateTime = signal('');
  formNotes = signal('');
  formReminderMinutes = signal<number[]>([1440, 60]);
  formCustomMinutesInput = signal<number | null>(null);
  formCustomMessage = signal('');
  saving = signal(false);

  confirmDeleteFor = signal<Appointment | null>(null);

  ngOnInit(): void {
    this.load();
    this.connectionService.getAll().subscribe((data) => {
      this.connections.set(data);
      if (data.length) this.formConnectionId.set(data[0].id);
    });
  }

  load(): void {
    this.service.getAll().subscribe({
      next: (data) => this.appointments.set(data),
      error: () => this.toast.error('Não foi possível carregar os agendamentos.'),
    });
  }

  isPast(scheduledForUtc: string): boolean {
    return new Date(scheduledForUtc).getTime() < Date.now();
  }

  toggleReminder(minutes: number): void {
    this.formReminderMinutes.update((list) =>
      list.includes(minutes) ? list.filter((m) => m !== minutes) : [...list, minutes],
    );
  }

  addCustomReminder(): void {
    const minutes = this.formCustomMinutesInput();
    if (!minutes || minutes <= 0) return;

    this.formReminderMinutes.update((list) =>
      list.includes(minutes) ? list : [...list, minutes].sort((a, b) => a - b),
    );
    this.formCustomMinutesInput.set(null);
  }

  removeReminder(minutes: number): void {
    this.formReminderMinutes.update((list) => list.filter((m) => m !== minutes));
  }

  isCustomReminder(minutes: number): boolean {
    return !this.reminderPresets.some((p) => p.minutes === minutes);
  }

  openForm(): void {
    this.formPhoneNumber.set('');
    this.formContactName.set('');
    this.formTitle.set('');
    this.formDateTime.set('');
    this.formNotes.set('');
    this.formReminderMinutes.set([1440, 60]);
    this.formCustomMinutesInput.set(null);
    this.formCustomMessage.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  submit(): void {
    if (!this.formConnectionId()) {
      this.toast.error('Conecte um número de WhatsApp antes.');
      return;
    }
    if (this.formPhoneNumber().length < 10) {
      this.toast.error('Digite um número de WhatsApp válido.');
      return;
    }
    if (!this.formTitle().trim()) {
      this.toast.error('Dê um título pro agendamento.');
      return;
    }
    if (!this.formDateTime()) {
      this.toast.error('Escolha a data e hora.');
      return;
    }

    this.saving.set(true);
    this.service
      .create({
        whatsAppConnectionId: this.formConnectionId(),
        phoneNumber: this.formPhoneNumber(),
        contactName: this.formContactName() || undefined,
        title: this.formTitle(),
        scheduledForUtc: new Date(this.formDateTime()).toISOString(),
        notes: this.formNotes() || undefined,
        reminderOffsetMinutes: this.formReminderMinutes(),
        reminderMessageTemplate: this.formCustomMessage() || undefined,
      })
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          this.showForm.set(false);
          this.load();

          if (res.remindersScheduled > 0) {
            this.toast.success(
              `Agendamento criado com ${res.remindersScheduled} lembrete(s) programado(s).`,
            );
          } else if (res.remindersSkippedPast > 0) {
            this.toast.info(
              'Agendamento criado, mas nenhum lembrete foi programado: todos os horários escolhidos já tinham passado (o compromisso está muito perto). Escolha um valor menor, tipo "5 min antes".',
            );
          } else {
            this.toast.success('Agendamento criado (sem lembretes).');
          }
        },
        error: (err) => {
          this.saving.set(false);
          this.toast.error(err?.error?.message ?? 'Não foi possível criar o agendamento.');
        },
      });
  }

  markCompleted(appt: Appointment): void {
    this.service.updateStatus(appt.id, 'Completed').subscribe({
      next: () => {
        this.toast.success('Marcado como concluído.');
        this.load();
      },
      error: () => this.toast.error('Não foi possível atualizar.'),
    });
  }

  cancelAppointment(appt: Appointment): void {
    this.service.updateStatus(appt.id, 'Cancelled').subscribe({
      next: () => {
        this.toast.success('Agendamento cancelado — lembretes pendentes também foram cancelados.');
        this.load();
      },
      error: () => this.toast.error('Não foi possível cancelar.'),
    });
  }

  askDelete(appt: Appointment): void {
    this.confirmDeleteFor.set(appt);
  }

  cancelDeleteDialog(): void {
    this.confirmDeleteFor.set(null);
  }

  confirmDelete(): void {
    const appt = this.confirmDeleteFor();
    if (!appt) return;

    this.confirmDeleteFor.set(null);
    this.service.delete(appt.id).subscribe({
      next: () => {
        this.toast.success('Agendamento removido.');
        this.load();
      },
      error: () => this.toast.error('Não foi possível remover.'),
    });
  }
}
