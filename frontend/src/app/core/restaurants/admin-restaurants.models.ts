export type EstablishmentType = 'Restaurant' | 'Cafe' | 'Bar' | 'Club' | 'FastFood' | 'Other' | 'ShishaBar';
export type RestaurantStatus = 'Draft' | 'Active' | 'Suspended' | 'Cancelled' | 'Archived';
export type SubscriptionStatus = 'Trial' | 'Active' | 'Overdue' | 'Suspended' | 'Cancelled';

export interface AdminRestaurantSummary {
  id: string;
  name: string;
  slug: string;
  type: EstablishmentType;
  logoUrl: string | null;
  address: string | null;
  status: RestaurantStatus;
  plan: string;
  subscriptionStatus: SubscriptionStatus;
  expiresOn: string;
}

export interface AdminRestaurantDetails {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  coverImageUrl: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  websiteUrl: string | null;
  instagramUrl: string | null;
  currency: string;
  defaultLanguage: string;
  enabledLanguages: string;
  type: EstablishmentType;
  status: RestaurantStatus;
  themeKey: string;
  ownerEmail: string | null;
  subscription: {
    status: SubscriptionStatus;
    plan: string;
    monthlyPrice: number;
    startsOn: string;
    expiresOn: string;
    gracePeriodEndsOn: string | null;
  };
}

export interface AdminDashboardSummary {
  totalRestaurants: number;
  activeRestaurants: number;
  activeLicenses: number;
  trialLicenses: number;
  expiringSoon: number;
  newSupportRequests: number;
  newLeads: number;
  restaurantsMissingProducts: number;
  restaurantsMissingImages: number;
  growth: { month: string; restaurants: number }[];
  subscriptionBreakdown: { status: string; count: number }[];
  recentRestaurants: { id: string; name: string; status: RestaurantStatus; plan: string; updatedAt: string }[];
  themeUsage: { themeKey: string; count: number }[];
}

export interface AdminReadinessIssue {
  key: string;
  label: string;
  ready: boolean;
}

export interface AdminRestaurantReadiness {
  restaurantId: string;
  restaurantName: string;
  slug: string;
  isMenuReady: boolean;
  qrDownloadedAt: string | null;
  items: AdminReadinessIssue[];
}

export interface AuditLogSummary {
  id: string;
  action: string;
  entityType: string;
  entityId: string | null;
  restaurantId: string | null;
  restaurantName: string | null;
  summary: string | null;
  actorEmail: string | null;
  actorRole: string | null;
  ipAddress: string | null;
  createdAt: string;
}

export interface CreateRestaurantRequest {
  name: string;
  slug: string;
  type: EstablishmentType;
  status: RestaurantStatus;
  ownerEmail: string;
  ownerPassword: string;
  trialDays: number;
  description: string | null;
  logoUrl: string | null;
  coverImageUrl: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  websiteUrl: string | null;
  instagramUrl: string | null;
  currency: string;
  defaultLanguage: string;
  enabledLanguages: string;
  plan: string;
  monthlyPrice: number;
  themeKey: string;
}

export interface UpdateRestaurantRequest {
  name: string;
  slug?: string | null;
  description: string | null;
  logoUrl: string | null;
  coverImageUrl: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  websiteUrl: string | null;
  instagramUrl: string | null;
  currency: string;
  defaultLanguage: string;
  enabledLanguages: string;
  type: EstablishmentType;
  themeKey: string;
}

export interface SetSubscriptionRequest {
  status: SubscriptionStatus;
  plan: string;
  monthlyPrice: number;
  startsOn: string;
  expiresOn: string;
  gracePeriodEndsOn: string | null;
}

export interface UpdateOwnerAccessRequest {
  email: string;
  newPassword: string | null;
}
