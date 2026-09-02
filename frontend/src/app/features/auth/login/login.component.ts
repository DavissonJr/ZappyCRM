import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

const WHATSAPP_NUMBER = '5581996533458';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: '../auth-shared.scss',
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = signal('');
  password = signal('');
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  orcamentoLink = `https://wa.me/${WHATSAPP_NUMBER}?text=${encodeURIComponent(
    'Olá! Ainda não sou cliente do Zappy CRM e quero fazer um orçamento para minha empresa.',
  )}`;

  submit(): void {
    if (!this.email() || !this.password()) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.login({ email: this.email(), password: this.password() }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Não foi possível entrar. Tente novamente.');
      },
    });
  }
}
