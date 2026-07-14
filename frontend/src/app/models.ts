export type AppView = 'login' | 'auth-login' | 'super-admin' | 'restaurant-owner' | 'public-menu';
export type AdminTab = 'dashboard' | 'restaurants' | 'archived-restaurants' | 'billing' | 'drink-library' | 'themes' | 'qr-codes' | 'support' | 'audit-logs';
export type OwnerTab = 'dashboard' | 'categories' | 'products' | 'daily-menu' | 'offers' | 'settings' | 'qr' | 'support';
export type ThemeType =
  | 'classic-light'
  | 'classic-dark'
  | 'premium-gold'
  | 'burgundy-dining'
  | 'mediterranean-blue'
  | 'olive-linen'
  | 'ocean-slate'
  | 'coffee-cream'
  | 'urban-espresso'
  | 'soft-pastel'
  | 'natural-green'
  | 'rose-latte'
  | 'cocoa-mint'
  | 'neon-night'
  | 'royal-violet'
  | 'warm-orange'
  | 'street-red'
  | 'yellow-pop'
  | 'burger-black'
  | 'lime-street'
  | 'modern-dark';
export type BadgeType = 'new' | 'popular' | 'spicy' | 'vegetarian' | 'chefs-choice';

export interface BusinessHour { day: string; open: string; close: string; closed: boolean; }
export interface Category { id: string; name: string; icon: string; order: number; active: boolean; type?: 'Food' | 'Drink'; }
export interface Product {
  id: string; categoryId: string; name: string; description: string; price: number;
  servingSize?: string | null; image: string; available: boolean; badges: BadgeType[]; allergens: string[];
  ingredients?: string | null; calories?: number | null; protein?: number | null; carbs?: number | null; fat?: number | null; sugar?: number | null; salt?: number | null;
}
export interface DailyMenu { id: string; name: string; items: string[]; price: number; date: string; active: boolean; }
export interface Offer { id: string; name: string; description: string; originalPrice: number; offerPrice: number; image: string; validUntil: string; active: boolean; }
export interface Restaurant {
  id: string; slug?: string; name: string; address: string; phone: string; website: string; instagram: string;
  cover: string; logo: string; status: 'active' | 'paused'; subscription: 'basic' | 'premium' | 'enterprise';
  theme: ThemeType; themeColor: string; categories: Category[];
  businessHours: BusinessHour[]; dailyMenu: DailyMenu[]; offers: Offer[];
}
