export interface Plan {
  tier: string;
  displayName: string;
  priceMonthly: number;
  maxWhatsAppConnections: number; // -1 = ilimitado
  maxUsers: number; // -1 = ilimitado
  highlights: string[];
  isCurrent: boolean;
}

export interface SubscriptionStatus {
  currentPlan: string;
  subscriptionStatus: string;
  trialEndsAtUtc?: string;
  currentPeriodEndUtc?: string;
  daysLeftInTrial: number;
  currentWhatsAppConnections: number;
  currentUsers: number;
  availablePlans: Plan[];
}
