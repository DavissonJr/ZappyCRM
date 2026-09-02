import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ContactService } from '../../core/services/contact.service';
import { ToastService } from '../../core/services/toast.service';
import { ContactListItem } from '../../core/models/contact.model';
import { PhoneFormatPipe } from '../../shared/pipes/phone-format.pipe';

type StatusFilter = 'all' | 'active' | 'blocked';

@Component({
  selector: 'app-contacts',
  standalone: true,
  imports: [CommonModule, FormsModule, PhoneFormatPipe],
  templateUrl: './contacts.component.html',
  styleUrl: './contacts.component.scss',
})
export class ContactsComponent implements OnInit {
  private service = inject(ContactService);
  private toast = inject(ToastService);
  private router = inject(Router);

  contacts = signal<ContactListItem[]>([]);
  loading = signal(true);
  searchTerm = signal('');
  statusFilter = signal<StatusFilter>('all');

  useNoConversationFilter = signal(false);
  noConversationDays = signal(30);
  useNoAppointmentFilter = signal(false);
  noAppointmentDays = signal(30);

  editingContact = signal<ContactListItem | null>(null);
  editName = signal('');
  editNotes = signal('');
  editBlocked = signal(false);
  saving = signal(false);

  private searchDebounce?: ReturnType<typeof setTimeout>;

  filteredContacts = computed(() => {
    const filter = this.statusFilter();
    if (filter === 'all') return this.contacts();
    return this.contacts().filter((c) => (filter === 'blocked' ? c.isBlocked : !c.isBlocked));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service
      .getAll({
        search: this.searchTerm() || undefined,
        noConversationInLastDays: this.useNoConversationFilter() ? this.noConversationDays() : undefined,
        noAppointmentInLastDays: this.useNoAppointmentFilter() ? this.noAppointmentDays() : undefined,
      })
      .subscribe({
        next: (data) => {
          this.contacts.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.toast.error('Não foi possível carregar os contatos.');
        },
      });
  }

  onSearchChange(value: string): void {
    this.searchTerm.set(value);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.load(), 350);
  }

  toggleNoConversationFilter(): void {
    this.useNoConversationFilter.update((v) => !v);
    this.load();
  }

  toggleNoAppointmentFilter(): void {
    this.useNoAppointmentFilter.update((v) => !v);
    this.load();
  }

  onInactivityDaysChange(): void {
    // Só recarrega se o filtro correspondente já estiver ligado — evita
    // requisição desnecessária enquanto a pessoa só digita o número.
    if (this.useNoConversationFilter() || this.useNoAppointmentFilter()) {
      clearTimeout(this.searchDebounce);
      this.searchDebounce = setTimeout(() => this.load(), 400);
    }
  }

  setStatusFilter(filter: StatusFilter): void {
    this.statusFilter.set(filter);
  }

  initials(contact: ContactListItem): string {
    const source = contact.name?.trim() || contact.phoneNumber;
    return source.charAt(0).toUpperCase();
  }

  openEdit(contact: ContactListItem): void {
    this.editingContact.set(contact);
    this.editName.set(contact.name ?? '');
    this.editNotes.set(contact.notes ?? '');
    this.editBlocked.set(contact.isBlocked);
  }

  closeEdit(): void {
    this.editingContact.set(null);
  }

  saveEdit(): void {
    const contact = this.editingContact();
    if (!contact) return;

    this.saving.set(true);
    this.service
      .update(contact.id, {
        name: this.editName() || undefined,
        notes: this.editNotes() || undefined,
        isBlocked: this.editBlocked(),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.editingContact.set(null);
          this.toast.success('Contato atualizado.');
          this.load();
        },
        error: () => {
          this.saving.set(false);
          this.toast.error('Não foi possível salvar as alterações.');
        },
      });
  }

  openInInbox(contact: ContactListItem): void {
    this.router.navigate(['/inbox'], { queryParams: { phone: contact.phoneNumber } });
  }
}
