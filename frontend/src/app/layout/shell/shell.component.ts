import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { NavigationStart, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { ThemeService } from '../../core/services/theme.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnInit, OnDestroy {
  auth = inject(AuthService);
  theme = inject(ThemeService);
  private realtime = inject(RealtimeService);
  private router = inject(Router);

  mobileMenuOpen = signal(false);

  navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: 'M3 3h6v8H3V3Zm8 0h6v5h-6V3ZM3 13h6v4H3v-4Zm8-3h6v7h-6v-7Z' },
    { path: '/inbox', label: 'Conversas', icon: 'M2 5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H8l-4 3v-3H4a2 2 0 0 1-2-2V5Z' },
    { path: '/contatos', label: 'Contatos', icon: 'M10 2a4 4 0 1 0 0 8 4 4 0 0 0 0-8ZM3 17a7 7 0 0 1 14 0 1 1 0 0 1-1 1H4a1 1 0 0 1-1-1Z' },
    { path: '/agendamentos', label: 'Agendamentos', icon: 'M5 2a1 1 0 0 1 1 1v1h8V3a1 1 0 1 1 2 0v1h1a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h1V3a1 1 0 0 1 1-1Zm11 6H4v8h12V8Z' },
    { path: '/propostas', label: 'Propostas', icon: 'M4 2h9l3 3v13a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1Zm8 1v3h3l-3-3ZM6 9h8v1.5H6V9Zm0 3h8v1.5H6V12Zm0 3h5v1.5H6V15Z' },
    { path: '/numeros', label: 'Números WhatsApp', icon: 'M4 3h8l4 4v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Zm7 6H8v2h3v2H8v2h5v-2h-2V9h2V7h-5v2Z' },
    { path: '/modelos', label: 'Modelos de mensagem', icon: 'M3 4h14v3H3V4Zm0 5h10v3H3V9Zm0 5h14v3H3v-3Z' },
  ];

  ngOnInit(): void {
    this.realtime.connect();
    // Fecha o menu mobile sozinho sempre que o usuário navega pra outra tela.
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationStart) this.mobileMenuOpen.set(false);
    });
  }

  ngOnDestroy(): void {
    this.realtime.disconnect();
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((v) => !v);
  }

  logout(): void {
    this.auth.logout();
  }
}
