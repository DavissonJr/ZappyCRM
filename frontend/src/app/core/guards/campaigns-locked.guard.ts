import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ToastService } from '../services/toast.service';

/// Campanhas fica bloqueada até integrarmos a API oficial da Meta — com a
/// Evolution API (não-oficial), o risco de bloqueio do número em disparo em
/// massa é alto demais.
export const campaignsLockedGuard: CanActivateFn = () => {
  const router = inject(Router);
  const toast = inject(ToastService);

  toast.info('Campanhas ainda não está disponível — chega junto com a integração oficial do WhatsApp.');
  router.navigate(['/dashboard']);
  return false;
};
