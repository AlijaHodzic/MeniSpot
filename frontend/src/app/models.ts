export type AppView = 'login' | 'auth-login' | 'super-admin' | 'restaurant-owner' | 'public-menu';
export type AdminTab = 'dashboard' | 'restaurants' | 'themes' | 'qr-codes';
export type OwnerTab = 'dashboard' | 'categories' | 'products' | 'daily-menu' | 'offers' | 'settings' | 'qr';
export type ThemeType = 'modern-dark' | 'classic-light' | 'premium-gold' | 'natural-green';
export type BadgeType = 'new' | 'popular' | 'spicy' | 'vegetarian' | 'chefs-choice';

export interface BusinessHour { day: string; open: string; close: string; closed: boolean; }
export interface Category { id: string; name: string; icon: string; order: number; active: boolean; }
export interface Product {
  id: string; categoryId: string; name: string; description: string; price: number;
  image: string; available: boolean; badges: BadgeType[]; allergens: string[];
}
export interface DailyMenu { id: string; name: string; items: string[]; price: number; date: string; active: boolean; }
export interface Offer { id: string; name: string; description: string; originalPrice: number; offerPrice: number; image: string; validUntil: string; active: boolean; }
export interface Restaurant {
  id: string; name: string; address: string; phone: string; website: string; instagram: string;
  cover: string; logo: string; status: 'active' | 'paused'; subscription: 'basic' | 'premium' | 'enterprise';
  theme: ThemeType; themeColor: string; rating: number; views: number; categories: Category[];
  businessHours: BusinessHour[]; dailyMenu: DailyMenu[]; offers: Offer[];
}
