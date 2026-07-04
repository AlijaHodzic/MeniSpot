import { EstablishmentType, RestaurantStatus } from '../restaurants/admin-restaurants.models';

export type SpecialOfferKind = 'Promotion' | 'DailyMenu';
export type MenuCategoryType = 'Food' | 'Drink';
export type SupportTicketType = 'MenuChange' | 'Image' | 'Theme' | 'TechnicalProblem' | 'Other';
export type SupportTicketPriority = 'Normal' | 'Urgent';
export type SupportTicketStatus = 'New' | 'InProgress' | 'Resolved' | 'Closed';

export interface OwnerMenuItem {
  id: string;
  categoryId: string;
  globalDrinkId: string | null;
  name: string;
  description: string | null;
  nameEn: string | null;
  descriptionEn: string | null;
  nameDe: string | null;
  descriptionDe: string | null;
  price: number;
  servingSize: string | null;
  imageUrl: string | null;
  allergens: string | null;
  ingredients: string | null;
  calories: number | null;
  protein: number | null;
  carbs: number | null;
  fat: number | null;
  sugar: number | null;
  salt: number | null;
  sortOrder: number;
  isVisible: boolean;
  isAvailable: boolean;
  isVegetarian: boolean;
  isSpicy: boolean;
  isFeatured: boolean;
}

export interface OwnerMenuCategory {
  id: string;
  name: string;
  description: string | null;
  nameEn: string | null;
  descriptionEn: string | null;
  nameDe: string | null;
  descriptionDe: string | null;
  type: MenuCategoryType;
  sortOrder: number;
  isVisible: boolean;
  items: OwnerMenuItem[];
}

export interface OwnerSpecialOffer {
  id: string;
  title: string;
  description: string | null;
  titleEn: string | null;
  descriptionEn: string | null;
  itemsEn: string | null;
  titleDe: string | null;
  descriptionDe: string | null;
  itemsDe: string | null;
  price: number | null;
  originalPrice: number | null;
  imageUrl: string | null;
  startsAt: string | null;
  endsAt: string | null;
  isVisible: boolean;
  kind: SpecialOfferKind;
  items: string | null;
}

export interface OwnerBusinessHour {
  dayOfWeek: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
  opensAt: string | null;
  closesAt: string | null;
  isClosed: boolean;
}

export interface OwnerMenuViewPoint {
  date: string;
  label: string;
  views: number;
}

export interface OwnerMenuAnalytics {
  totalViews: number;
  last7DaysViews: number;
  last30DaysViews: number;
  weeklyViews: OwnerMenuViewPoint[];
  topItems: { itemId: string; name: string; views: number }[];
}

export interface OwnerRestaurant {
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
  plan: string;
  theme: { themeKey: string; primaryColor: string; accentColor: string; backgroundImageUrl: string | null; fontFamily: string };
  businessHours: OwnerBusinessHour[];
  categories: OwnerMenuCategory[];
  offers: OwnerSpecialOffer[];
  analytics: OwnerMenuAnalytics;
}

export interface CategoryRequest { name: string; description: string | null; nameEn: string | null; descriptionEn: string | null; nameDe: string | null; descriptionDe: string | null; type: MenuCategoryType; sortOrder: number; isVisible: boolean }
export interface MenuItemRequest extends Omit<OwnerMenuItem, 'id' | 'globalDrinkId'> {}
export interface SpecialOfferRequest extends Omit<OwnerSpecialOffer, 'id'> {}
export interface GlobalDrinkSummary { id: string; name: string; category: string; description: string | null; imageUrl: string | null; servingOptions: string | null; sortOrder: number }
export interface LibraryDrinkSelection { drinkId: string; servingSize: string | null; price: number; isVisible: boolean; isAvailable: boolean }
export interface AddLibraryDrinksRequest { categoryId: string | null; drinks: LibraryDrinkSelection[] }

export interface SupportTicket {
  id: string;
  restaurantId: string;
  restaurantName: string;
  restaurantSlug: string;
  restaurantPlan: string;
  title: string;
  type: SupportTicketType;
  priority: SupportTicketPriority;
  status: SupportTicketStatus;
  message: string;
  attachmentUrl: string | null;
  adminNote: string | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
}

export interface CreateSupportTicketRequest {
  title: string;
  type: SupportTicketType;
  priority: SupportTicketPriority;
  message: string;
  attachmentUrl: string | null;
}

export interface UpdateSupportTicketRequest {
  status: SupportTicketStatus;
  adminNote: string | null;
}
