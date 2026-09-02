import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

const WHATSAPP_NUMBER = '5581996533458';

function waLink(message: string): string {
  return `https://wa.me/${WHATSAPP_NUMBER}?text=${encodeURIComponent(message)}`;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent {
  ctaLinkPrincipal = waLink('Olá! Vi o Zappy CRM e quero fazer um orçamento para minha empresa.');
  ctaLinkFinal = waLink('Olá! Quero começar a usar o Zappy CRM na minha empresa. Pode me passar mais detalhes?');

  planLink(planName: string): string {
    return waLink(`Olá! Tenho interesse no plano ${planName} do Zappy CRM. Pode me passar mais detalhes?`);
  }

  features = [
    {
      title: 'Responde sozinho, na hora',
      body: 'Enquanto você atende presencialmente, a IA já está respondendo quem chama no WhatsApp — com o tom da sua empresa, não um robô genérico.',
    },
    {
      title: 'Marca horário sem você mexer um dedo',
      body: 'Cliente confirma data e horário na própria conversa, e o agendamento já entra no seu sistema — com lembrete automático antes da hora.',
    },
    {
      title: 'Nunca deixa ninguém esperando',
      body: 'Fora do horário de atendimento ou se algo falhar, seu cliente recebe uma resposta educada na hora — não fica olhando "visto" sem retorno.',
    },
    {
      title: 'Você vê tudo num painel só',
      body: 'Quantos clientes novos, quantas conversas, quantos agendamentos — sem precisar abrir o WhatsApp pra somar nada na cabeça.',
    },
  ];

  steps = [
    {
      title: 'Você me chama no WhatsApp',
      body: 'Conta um pouco do seu negócio — clínica, oficina, escritório, imobiliária, o que for.',
    },
    {
      title: 'Eu configuro tudo pra você',
      body: 'Conecto seu número, ajusto a IA com a linguagem certa pro seu tipo de cliente, e deixo pronto.',
    },
    {
      title: 'Seu WhatsApp começa a trabalhar sozinho',
      body: 'Você recebe acesso ao painel, acompanha tudo, e sua equipe entra só quando precisa mesmo de um humano.',
    },
  ];

  chatMessages = [
    { from: 'them', text: 'Oi, vocês têm horário pra amanhã de manhã?' },
    { from: 'us', text: 'Bom dia! Temos sim 😊 Às 9h ou às 10h30 — qual fica melhor pra você?' },
    { from: 'them', text: 'Pode ser 9h' },
    { from: 'us', text: 'Prontinho! Agendado pra amanhã às 9h. Te aviso 1h antes ✅' },
  ];

  plans = [
    {
      name: 'Basic',
      price: '149',
      highlight: false,
      features: ['1 número de WhatsApp', 'Até 2 usuários', '300 créditos de IA por usuário/mês', 'IA que responde e agenda sozinha', 'Dashboard e relatórios'],
    },
    {
      name: 'Pro',
      price: '289',
      highlight: true,
      features: ['Até 3 números de WhatsApp', 'Até 6 usuários', '600 créditos de IA por usuário/mês', 'Tudo do Basic', 'Propostas comerciais geradas por IA'],
    },
    {
      name: 'Business',
      price: '549',
      highlight: false,
      features: ['Números ilimitados', 'Usuários ilimitados', '1000 créditos de IA por usuário/mês', 'Tudo do Pro', 'Suporte prioritário'],
    },
  ];
}
