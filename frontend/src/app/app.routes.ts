import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { platformAdminGuard } from './core/guards/platform-admin.guard';
import { campaignsLockedGuard } from './core/guards/campaigns-locked.guard';
import { redirectIfAuthenticatedGuard } from './core/guards/redirect-if-authenticated.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/landing/landing.component').then((m) => m.LandingComponent),
    canActivate: [redirectIfAuthenticatedGuard],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/shell/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'inbox',
        loadComponent: () =>
          import('./features/inbox/inbox.component').then((m) => m.InboxComponent),
      },
      {
        path: 'contatos',
        loadComponent: () =>
          import('./features/contacts/contacts.component').then((m) => m.ContactsComponent),
      },
      {
        path: 'campanhas',
        loadComponent: () =>
          import('./features/bulk-campaigns/bulk-campaigns.component').then((m) => m.BulkCampaignsComponent),
        canActivate: [campaignsLockedGuard],
      },
      {
        path: 'numeros',
        loadComponent: () =>
          import('./features/whatsapp-connections/whatsapp-connections.component').then(
            (m) => m.WhatsAppConnectionsComponent,
          ),
      },
      {
        path: 'modelos',
        loadComponent: () =>
          import('./features/message-templates/message-templates.component').then(
            (m) => m.MessageTemplatesComponent,
          ),
      },
      {
        path: 'configuracoes',
        loadComponent: () =>
          import('./features/settings/settings.component').then((m) => m.SettingsComponent),
      },
      {
        path: 'agendamentos',
        loadComponent: () =>
          import('./features/appointments/appointments.component').then((m) => m.AppointmentsComponent),
      },
      {
        path: 'propostas',
        loadComponent: () =>
          import('./features/proposals/proposals.component').then((m) => m.ProposalsComponent),
      },
      {
        path: 'admin',
        loadComponent: () =>
          import('./features/admin/admin.component').then((m) => m.AdminComponent),
        canActivate: [platformAdminGuard],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
