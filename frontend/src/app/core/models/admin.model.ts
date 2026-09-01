export interface AdminPlanOption {
  tier: string;
  displayName: string;
  maxWhatsAppConnections: number; // -1 = ilimitado
  maxUsers: number; // -1 = ilimitado
}

export interface AdminTenantSummary {
  id: string;
  name: string;
  segment: string;
  plan: string;
  isActive: boolean;
  createdAtUtc: string;
  ownerName?: string;
  ownerEmail?: string;
  userCount: number;
  whatsAppConnectionCount: number;
  connectedWhatsAppCount: number;
  contactCount: number;
  conversationCount: number;
  messageCount: number;
  appointmentCount: number;
  proposalCount: number;
  totalAiInputTokens: number;
  totalAiOutputTokens: number;
  totalAiEstimatedCostUsd: number;
  lastActivityUtc?: string;
}
