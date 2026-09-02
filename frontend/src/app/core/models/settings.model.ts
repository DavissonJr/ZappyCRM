export interface Me {
  id: string;
  fullName: string;
  email: string;
  role: string;
}

export interface TenantSettings {
  id: string;
  name: string;
  segment: string;
  plan: string;
}

export interface AiAgentConfig {
  agentName: string;
  systemPrompt: string;
  autoReplyEnabled: boolean;
  requireHumanApproval: boolean;
  businessHours: string;
  fallbackMessage?: string;
}

export interface AiCreditsStatus {
  planName: string;
  creditsUsedThisMonth: number;
  creditsBudgetThisMonth: number;
  monthStartUtc: string;
}

export interface TeamMember {
  id: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
}
