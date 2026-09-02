import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { OnboardingService } from '../../core/services/onboarding.service';
import { OnboardingStatus } from '../../core/models/onboarding.model';

/// Simplificado de propósito: agora que o cliente não configura mais chave
/// nenhuma, o único passo que realmente falta pra IA funcionar é conectar um
/// número de WhatsApp — então é só um aviso, não mais um checklist inteiro.
@Component({
  selector: 'app-onboarding-checklist',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (status(); as s) {
      @if (!s.hasConnectedWhatsApp && !dismissed()) {
        <div class="onboarding-warning">
          <div class="warning-text">
            <strong>Conecte um número de WhatsApp pra começar</strong>
            <span>Sua IA já está configurada — só falta escanear o QR code do seu número.</span>
          </div>
          <a class="warning-cta" routerLink="/numeros">Conectar agora</a>
          <button class="dismiss-btn" (click)="dismiss()" title="Esconder por agora">✕</button>
        </div>
      }
    }
  `,
  styleUrl: './onboarding-checklist.component.scss',
})
export class OnboardingChecklistComponent implements OnInit {
  private service = inject(OnboardingService);
  private router = inject(Router);

  status = signal<OnboardingStatus | null>(null);
  dismissed = signal(false);

  ngOnInit(): void {
    this.dismissed.set(sessionStorage.getItem('onboarding_dismissed') === 'true');
    this.load();

    // Recarrega o status toda vez que o usuário navega, pro aviso sumir
    // sozinho assim que o número for conectado.
    this.router.events.subscribe(() => this.load());
  }

  load(): void {
    this.service.getStatus().subscribe((data) => this.status.set(data));
  }

  dismiss(): void {
    sessionStorage.setItem('onboarding_dismissed', 'true');
    this.dismissed.set(true);
  }
}
