import { SubscriptionStatus } from '../restaurants/admin-restaurants.models';

export type PaymentMethod = 'BankTransfer' | 'Cash' | 'Card' | 'Other';

export interface BillingAccountSummary {
  restaurantId: string;
  restaurantName: string;
  slug: string;
  plan: string;
  monthlyPrice: number;
  currency: string;
  status: SubscriptionStatus;
  expiresOn: string;
  gracePeriodEndsOn: string | null;
  lastPaidOn: string | null;
  lastPaymentAmount: number | null;
}

export interface BillingOverview {
  monthlyRecurringRevenue: { currency: string; amount: number }[];
  paidThisMonth: { currency: string; amount: number }[];
  overdueCount: number;
  expiringSoon: number;
  accounts: BillingAccountSummary[];
}

export interface PaymentHistoryItem {
  id: string;
  amount: number;
  currency: string;
  paidOn: string;
  periodStartsOn: string;
  periodEndsOn: string;
  coverageMonths: number;
  method: PaymentMethod;
  reference: string | null;
  note: string | null;
  createdAt: string;
}

export interface RecordManualPaymentRequest {
  amount: number;
  currency: string;
  paidOn: string;
  coverageMonths: number;
  method: PaymentMethod;
  reference: string | null;
  note: string | null;
}
