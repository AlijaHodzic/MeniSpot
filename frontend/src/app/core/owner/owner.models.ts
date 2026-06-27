import { EstablishmentType, RestaurantStatus } from '../restaurants/admin-restaurants.models';

export type SpecialOfferKind = 'Promotion' | 'DailyMenu';
export type MenuCategoryType = 'Food' | 'Drink';

export interface OwnerMenuItem {
  id: string;
  categoryId: string;
  globalDrinkId: string | null;
  name: string;
  description: string | null;
  price: number;
  servingSize: string | null;
  imageUrl: string | null;
  allergens: string | null;
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
  type: MenuCategoryType;
  sortOrder: number;
  isVisible: boolean;
  items: OwnerMenuItem[];
}

export interface OwnerSpecialOffer {
  id: string;
  title: string;
  description: string | null;
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
  type: EstablishmentType;
  status: RestaurantStatus;
  theme: { themeKey: string; primaryColor: string; accentColor: string; backgroundImageUrl: string | null; fontFamily: string };
  businessHours: OwnerBusinessHour[];
  categories: OwnerMenuCategory[];
  offers: OwnerSpecialOffer[];
}

export interface CategoryRequest { name: string; description: string | null; type: MenuCategoryType; sortOrder: number; isVisible: boolean }
export interface MenuItemRequest extends Omit<OwnerMenuItem, 'id' | 'globalDrinkId'> {}
export interface SpecialOfferRequest extends Omit<OwnerSpecialOffer, 'id'> {}
export interface GlobalDrinkSummary { id: string; name: string; category: string; description: string | null; imageUrl: string | null; servingOptions: string | null; sortOrder: number }
export interface LibraryDrinkSelection { drinkId: string; servingSize: string | null; price: number; isVisible: boolean; isAvailable: boolean }
export interface AddLibraryDrinksRequest { categoryId: string | null; drinks: LibraryDrinkSelection[] }
