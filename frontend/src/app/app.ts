import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import {
  LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat, LucideClock, LucideEdit2,
  LucideEye, LucideFlame, LucideGlobe, LucideLeaf, LucideLogOut,
  LucideMapPin, LucideMenu, LucidePalette, LucidePercent, LucidePhone, LucidePlus,
  LucidePower, LucideQrCode, LucideSearch, LucideShield, LucideSparkles,
  LucideStar, LucideStore, LucideTrash2, LucideTrendingUp, LucideUtensilsCrossed, LucideX,
} from '@lucide/angular';
import { products as initialProducts, restaurants, themeOptions } from './demo-data';
import { AdminTab, AppView, BadgeType, OwnerTab, Product, Restaurant, ThemeType } from './models';

@Component({
  selector: 'app-root',
  imports: [
    CommonModule, FormsModule, LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat,
    LucideClock, LucideEdit2, LucideEye, LucideFlame, LucideGlobe,
    LucideLeaf, LucideLogOut, LucideMapPin, LucideMenu, LucidePalette, LucidePercent,
    LucidePhone, LucidePlus, LucidePower, LucideQrCode, LucideSearch,
    LucideShield, LucideSparkles, LucideStar, LucideStore, LucideTrash2, LucideTrendingUp,
    LucideUtensilsCrossed, LucideX,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly restaurants = restaurants;
  readonly themes = themeOptions;
  readonly adminTabs: { id: AdminTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'restaurants', label: 'Restorani' },
    { id: 'themes', label: 'Teme' }, { id: 'qr-codes', label: 'QR kodovi' },
  ];
  readonly ownerTabs: { id: OwnerTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'categories', label: 'Kategorije' },
    { id: 'products', label: 'Proizvodi' }, { id: 'daily-menu', label: 'Dnevni meni' },
    { id: 'offers', label: 'Ponude' }, { id: 'settings', label: 'Postavke' }, { id: 'qr', label: 'QR kod' },
  ];

  view: AppView = 'login';
  adminTab: AdminTab = 'dashboard';
  ownerTab: OwnerTab = 'dashboard';
  selectedRestaurantId = restaurants[0].id;
  selectedCategory = 'all';
  search = '';
  mobileNav = false;
  showHours = false;
  showProductModal = false;
  productMap: Record<string, Product[]> = structuredClone(initialProducts);

  constructor() {
    this.syncRoute(this.router.url);
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.syncRoute(event.urlAfterRedirects));
  }

  get restaurant(): Restaurant {
    return this.restaurants.find((item) => item.id === this.selectedRestaurantId) ?? this.restaurants[0];
  }

  get restaurantProducts(): Product[] { return this.productMap[this.restaurant.id] ?? []; }
  get adminTitle(): string { return this.adminTabs.find((item) => item.id === this.adminTab)?.label ?? ''; }
  get ownerTitle(): string { return this.ownerTabs.find((item) => item.id === this.ownerTab)?.label ?? ''; }

  get filteredProducts(): Product[] {
    const term = this.search.trim().toLocaleLowerCase();
    return this.restaurantProducts.filter((product) =>
      (this.selectedCategory === 'all' || product.categoryId === this.selectedCategory) &&
      (!term || product.name.toLocaleLowerCase().includes(term) || product.description.toLocaleLowerCase().includes(term)),
    );
  }

  enter(view: AppView, restaurantId?: string): void {
    const id = restaurantId ?? this.selectedRestaurantId;
    const commands: string[] = view === 'super-admin'
      ? ['/admin', 'dashboard']
      : view === 'restaurant-owner'
        ? ['/restaurant', id, 'dashboard']
        : view === 'public-menu'
          ? ['/menu', id]
          : ['/'];
    void this.router.navigate(commands);
  }

  selectAdminTab(tab: AdminTab): void { void this.router.navigate(['/admin', tab]); }
  selectOwnerTab(tab: OwnerTab): void { void this.router.navigate(['/restaurant', this.restaurant.id, tab]); }

  toggleRestaurant(restaurant: Restaurant): void {
    restaurant.status = restaurant.status === 'active' ? 'paused' : 'active';
  }

  toggleProduct(product: Product): void { product.available = !product.available; }

  deleteProduct(product: Product): void {
    this.productMap[this.restaurant.id] = this.restaurantProducts.filter((item) => item.id !== product.id);
  }

  changeTheme(theme: ThemeType): void { this.restaurant.theme = theme; }

  productsFor(categoryId: string): Product[] {
    return this.filteredProducts.filter((product) => product.categoryId === categoryId);
  }

  categoryName(categoryId: string): string {
    return this.restaurant.categories.find((item) => item.id === categoryId)?.name ?? '';
  }

  badgeLabel(badge: BadgeType): string {
    return ({ new: 'Novo', popular: 'Popularno', spicy: 'Ljuto', vegetarian: 'Veg', 'chefs-choice': 'Chef preporučuje' })[badge];
  }

  formatPrice(price: number): string {
    return new Intl.NumberFormat('bs-BA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(price) + ' KM';
  }

  qrCells(seed: string): number[] {
    const values: number[] = [];
    for (let i = 0; i < 225; i++) values.push((seed.charCodeAt(i % seed.length) + i * 7) % 3 ? 1 : 0);
    return values;
  }

  private syncRoute(url: string): void {
    const segments = url.split('?')[0].split('/').filter(Boolean).map(decodeURIComponent);
    if (segments[0] === 'admin') {
      this.view = 'super-admin';
      this.adminTab = this.isAdminTab(segments[1]) ? segments[1] : 'dashboard';
    } else if (segments[0] === 'restaurant') {
      this.view = 'restaurant-owner';
      this.selectedRestaurantId = this.validRestaurantId(segments[1]);
      this.ownerTab = this.isOwnerTab(segments[2]) ? segments[2] : 'dashboard';
    } else if (segments[0] === 'menu') {
      this.view = 'public-menu';
      this.selectedRestaurantId = this.validRestaurantId(segments[1]);
    } else {
      this.view = 'login';
    }

    this.mobileNav = false;
    this.search = '';
    this.selectedCategory = 'all';
  }

  private validRestaurantId(value?: string): string {
    return this.restaurants.some((restaurant) => restaurant.id === value) ? value! : this.restaurants[0].id;
  }

  private isAdminTab(value?: string): value is AdminTab {
    return this.adminTabs.some((tab) => tab.id === value);
  }

  private isOwnerTab(value?: string): value is OwnerTab {
    return this.ownerTabs.some((tab) => tab.id === value);
  }
}
