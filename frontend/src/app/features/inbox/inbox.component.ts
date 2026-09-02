import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { ConversationService } from '../../core/services/conversation.service';
import { WhatsAppConnectionService } from '../../core/services/whatsapp-connection.service';
import { MessageTemplateService } from '../../core/services/message-template.service';
import { PhoneFormatPipe } from '../../shared/pipes/phone-format.pipe';
import { ToastService } from '../../core/services/toast.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { Conversation, ConversationSummary } from '../../core/models/conversation.model';
import { WhatsAppConnection } from '../../core/models/whatsapp-connection.model';
import { MessageTemplate, SCOPE_LABELS } from '../../core/models/message-template.model';
import { PhoneMaskDirective } from '../../shared/phone-mask.directive';
import { OnboardingChecklistComponent } from '../../shared/onboarding/onboarding-checklist.component';
import { ProposalService } from '../../core/services/proposal.service';

// Fallback: se o SignalR cair por algum motivo, o polling garante que a tela
// não fica desatualizada por muito tempo (a atualização "de verdade" vem do hub).
const POLL_FALLBACK_INTERVAL_MS = 15000;

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [CommonModule, FormsModule, PhoneMaskDirective, OnboardingChecklistComponent, PhoneFormatPipe],
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.scss',
})
export class InboxComponent implements OnInit, OnDestroy {
  private conversationService = inject(ConversationService);
  private connectionService = inject(WhatsAppConnectionService);
  private templateService = inject(MessageTemplateService);
  private toast = inject(ToastService);
  private proposalService = inject(ProposalService);
  private realtime = inject(RealtimeService);
  private route = inject(ActivatedRoute);
  private pollSubscription?: Subscription;
  private realtimeSubscription?: Subscription;
  private lastKnownMessageTimes = new Map<string, string>();
  /** Preenchido quando se chega no Inbox vindo da tela de Contatos (?phone=...). */
  private pendingPhoneToSelect?: string;

  conversations = signal<ConversationSummary[]>([]);
  selectedConversation = signal<Conversation | null>(null);
  draftMessage = signal('');
  sendingMessage = signal(false);
  sendError = signal<string | null>(null);

  connections = signal<WhatsAppConnection[]>([]);
  showNewConversation = signal(false);
  newPhoneNumber = signal('');
  newContactName = signal('');
  newConnectionId = signal('');
  newFirstMessage = signal('');
  startingConversation = signal(false);
  newConversationError = signal<string | null>(null);

  templates = signal<MessageTemplate[]>([]);
  scopeLabels = SCOPE_LABELS;
  showTemplatePicker = signal(false);

  confirmDeleteConversation = signal(false);
  deletingConversation = signal(false);
  generatingProposal = signal(false);

  ngOnInit(): void {
    this.pendingPhoneToSelect = this.route.snapshot.queryParamMap.get('phone') ?? undefined;
    this.loadConversations();
    this.connectionService.getAll().subscribe((data) => {
      this.connections.set(data);
      if (data.length) this.newConnectionId.set(data[0].id);
    });
    this.templateService.getAll().subscribe((data) => this.templates.set(data.filter((t) => t.isActive)));

    // Caminho principal: o backend avisa via SignalR assim que algo muda.
    this.realtimeSubscription = this.realtime.conversationUpdated$.subscribe((conversationId) => {
      this.handleConversationUpdated(conversationId);
    });

    // Caminho de segurança: caso o SignalR não conecte por algum motivo de rede.
    this.pollSubscription = interval(POLL_FALLBACK_INTERVAL_MS).subscribe(() => this.pollForUpdates());
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.realtimeSubscription?.unsubscribe();
  }

  private handleConversationUpdated(conversationId: string): void {
    // Sempre atualiza a lista (pra mostrar preview/ordem novos).
    this.conversationService.getAll().subscribe({
      next: (data) => {
        this.notifyNewMessages(data, this.selectedConversation()?.id);
        this.conversations.set(data);
      },
      error: () => {},
    });

    // Se a conversa que mudou é a que está aberta, atualiza ela também.
    if (this.selectedConversation()?.id === conversationId) {
      this.conversationService.getById(conversationId).subscribe({
        next: (full) => this.selectedConversation.set(full),
        error: () => {},
      });
    }
  }

  private pollForUpdates(): void {
    const currentId = this.selectedConversation()?.id;

    this.conversationService.getAll().subscribe({
      next: (data) => {
        this.notifyNewMessages(data, currentId);
        this.conversations.set(data);
      },
      error: () => {}, // silencioso — não incomoda o usuário a cada 4s se a rede falhar uma vez
    });

    if (currentId) {
      this.conversationService.getById(currentId).subscribe({
        next: (full) => {
          // Só atualiza se realmente mudou algo (evita "piscar" a tela à toa).
          const current = this.selectedConversation();
          if (!current || current.messages.length !== full.messages.length) {
            this.selectedConversation.set(full);
          }
        },
        error: () => {},
      });
    }
  }

  private notifyNewMessages(data: ConversationSummary[], currentlyOpenId: string | undefined): void {
    for (const conv of data) {
      const lastKnown = this.lastKnownMessageTimes.get(conv.id);
      const isNew = lastKnown && lastKnown !== conv.lastMessageAtUtc;

      // Só avisa se a conversa NÃO é a que está aberta na tela (essa já atualiza sozinha).
      if (isNew && conv.id !== currentlyOpenId) {
        this.toast.info(`Nova mensagem de ${conv.contact.name || conv.contact.phoneNumber}`);
      }
      this.lastKnownMessageTimes.set(conv.id, conv.lastMessageAtUtc);
    }
  }

  loadConversations(selectId?: string): void {
    this.conversationService.getAll().subscribe({
      next: (data) => {
        this.conversations.set(data);

        let toSelect: ConversationSummary | undefined;
        if (selectId) {
          toSelect = data.find((c) => c.id === selectId);
        } else if (this.pendingPhoneToSelect) {
          toSelect = data.find((c) => c.contact.phoneNumber === this.pendingPhoneToSelect);
          if (!toSelect) this.toast.info('Esse contato ainda não tem nenhuma conversa.');
          this.pendingPhoneToSelect = undefined; // só usa uma vez, no carregamento inicial
        } else {
          toSelect = this.selectedConversation() ?? data[0];
        }

        if (toSelect) this.select(toSelect);
      },
      error: () => this.sendError.set('Não foi possível carregar as conversas. Recarregue a página.'),
    });
  }

  select(conversation: ConversationSummary): void {
    this.sendError.set(null);
    this.conversationService.getById(conversation.id).subscribe({
      next: (full) => this.selectedConversation.set(full),
      error: () => this.sendError.set('Não foi possível abrir essa conversa.'),
    });
  }

  /** Só usado no mobile — volta pra lista sem perder o que já foi carregado. */
  backToList(): void {
    this.selectedConversation.set(null);
  }

  send(): void {
    const conv = this.selectedConversation();
    const text = this.draftMessage().trim();
    if (!conv || !text) return;

    this.sendingMessage.set(true);
    this.sendError.set(null);

    this.conversationService.sendMessage(conv.id, text).subscribe({
      next: () => {
        this.draftMessage.set('');
        this.sendingMessage.set(false);
        // Em produção: atualizar via SignalR em tempo real em vez de refetch manual.
        this.conversationService.getById(conv.id).subscribe((updated) => {
          this.selectedConversation.set(updated);
        });
      },
      error: (err) => {
        this.sendingMessage.set(false);
        this.sendError.set(
          err?.error?.message ?? 'Não foi possível enviar a mensagem. Verifique se o número ainda está conectado.',
        );
      },
    });
  }

  toggleTemplatePicker(): void {
    this.showTemplatePicker.update((v) => !v);
  }

  useTemplate(template: MessageTemplate): void {
    this.draftMessage.set(template.content);
    this.showTemplatePicker.set(false);
  }

  initials(name: string | undefined, phone: string): string {
    const source = name?.trim() || phone;
    return source.charAt(0).toUpperCase();
  }

  useSuggestion(suggestion: string): void {
    this.draftMessage.set(suggestion);
  }

  sendSuggestionDirectly(): void {
    const conv = this.selectedConversation();
    if (!conv?.pendingAiSuggestion) return;
    this.draftMessage.set(conv.pendingAiSuggestion);
    this.send();
  }

  dismissSuggestion(): void {
    const conv = this.selectedConversation();
    if (!conv) return;

    this.conversationService.dismissSuggestion(conv.id).subscribe({
      next: () => {
        this.toast.info('Sugestão descartada.');
        this.selectedConversation.update((c) => (c ? { ...c, pendingAiSuggestion: undefined } : c));
        this.loadConversations(conv.id);
      },
      error: () => this.toast.error('Não foi possível descartar a sugestão.'),
    });
  }

  askDeleteConversation(): void {
    this.confirmDeleteConversation.set(true);
  }

  cancelDeleteConversation(): void {
    this.confirmDeleteConversation.set(false);
  }

  confirmDeleteConversationAction(): void {
    const conv = this.selectedConversation();
    if (!conv) return;

    this.deletingConversation.set(true);
    this.conversationService.delete(conv.id).subscribe({
      next: () => {
        this.deletingConversation.set(false);
        this.confirmDeleteConversation.set(false);
        this.selectedConversation.set(null);
        this.toast.success('Conversa removida.');
        this.loadConversations();
      },
      error: () => {
        this.deletingConversation.set(false);
        this.toast.error('Não foi possível remover essa conversa.');
      },
    });
  }

  openNewConversation(): void {
    this.newPhoneNumber.set('');
    this.newContactName.set('');
    this.newFirstMessage.set('');
    this.newConversationError.set(null);
    this.showNewConversation.set(true);
  }

  closeNewConversation(): void {
    this.showNewConversation.set(false);
  }

  startConversation(): void {
    const phoneNumber = this.newPhoneNumber().trim();
    const content = this.newFirstMessage().trim();
    const connectionId = this.newConnectionId();

    if (!connectionId) {
      this.newConversationError.set('Conecte um número de WhatsApp antes de iniciar uma conversa.');
      return;
    }
    if (phoneNumber.length < 10) {
      this.newConversationError.set('Digite um número de WhatsApp válido, com DDI e DDD.');
      return;
    }
    if (!content) {
      this.newConversationError.set('Escreva a primeira mensagem.');
      return;
    }

    this.startingConversation.set(true);
    this.newConversationError.set(null);

    this.conversationService
      .startConversation({
        whatsAppConnectionId: connectionId,
        phoneNumber,
        contactName: this.newContactName() || undefined,
        content,
      })
      .subscribe({
        next: (res) => {
          this.startingConversation.set(false);
          this.showNewConversation.set(false);
          this.loadConversations(res.conversationId);
        },
        error: (err) => {
          this.startingConversation.set(false);
          this.newConversationError.set(
            err?.error?.message ?? 'Não foi possível iniciar a conversa. Verifique se o número está conectado.',
          );
        },
      });
  }

  generateProposal(): void {
    const conv = this.selectedConversation();
    if (!conv) return;

    this.generatingProposal.set(true);
    this.proposalService.generate(conv.id).subscribe({
      next: () => {
        this.generatingProposal.set(false);
        this.toast.success('Proposta gerada! Vá em "Propostas" pra revisar e enviar.');
      },
      error: (err) => {
        this.generatingProposal.set(false);
        this.toast.error(err?.error?.message ?? 'Não foi possível gerar a proposta.');
      },
    });
  }
}
