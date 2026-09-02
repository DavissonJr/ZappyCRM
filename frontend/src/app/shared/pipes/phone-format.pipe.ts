import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formata um telefone armazenado em formato "cru" (ex: "5581996533458" ou
 * "+5581996533458") pro padrão brasileiro legível: "(81) 99653-3458".
 * Cai de volta pro valor original se não conseguir reconhecer o formato
 * (números de outros países, por exemplo).
 */
@Pipe({ name: 'phoneFormat', standalone: true })
export class PhoneFormatPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';

    const digits = value.replace(/\D/g, '');

    // Com DDI do Brasil (55) + DDD (2) + número (8 ou 9 dígitos) = 12 ou 13 dígitos.
    if (digits.length === 13 && digits.startsWith('55')) {
      const ddd = digits.slice(2, 4);
      const number = digits.slice(4);
      return `(${ddd}) ${number.slice(0, 5)}-${number.slice(5)}`;
    }
    if (digits.length === 12 && digits.startsWith('55')) {
      const ddd = digits.slice(2, 4);
      const number = digits.slice(4);
      return `(${ddd}) ${number.slice(0, 4)}-${number.slice(4)}`;
    }

    // Sem DDI, já vem só DDD + número (10 ou 11 dígitos) — trata como BR também.
    if (digits.length === 11) {
      return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
    }
    if (digits.length === 10) {
      return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
    }

    // Formato não reconhecido (outro país, número incompleto etc.) — mostra como veio.
    return value;
  }
}
