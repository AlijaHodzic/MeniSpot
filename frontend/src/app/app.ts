import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { concatMap, filter, finalize, forkJoin, Observable } from 'rxjs';
import {
  LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat, LucideClock, LucideEdit2,
  LucideDownload, LucideEye, LucideEyeOff, LucideFlame, LucideGlobe, LucideLeaf, LucideLock, LucideLogIn, LucideLogOut, LucideMail,
  LucideMapPin, LucideMenu, LucidePercent, LucidePhone, LucidePlus,
  LucidePower, LucideQrCode, LucideSearch, LucideShield, LucideSparkles,
  LucideStore, LucideTrash2, LucideTrendingUp, LucideUtensilsCrossed, LucideX,
} from '@lucide/angular';
import { AdminTab, AppView, BadgeType, Category, OwnerTab, Product, Restaurant, ThemeType } from './models';
import { AuthService } from './core/auth/auth.service';
import {
  AdminRestaurantDetails,
  AdminRestaurantSummary,
  AdminDashboardSummary,
  EstablishmentType,
  RestaurantStatus,
  SubscriptionStatus,
} from './core/restaurants/admin-restaurants.models';
import { AdminRestaurantsService } from './core/restaurants/admin-restaurants.service';
import { QrCodeService } from './core/qr-code.service';
import { BillingAccountSummary, BillingOverview, PaymentHistoryItem, PaymentMethod } from './core/billing/billing.models';
import { BillingService } from './core/billing/billing.service';
import { AdminGlobalDrink } from './core/drinks/admin-drinks.models';
import { AdminDrinksService } from './core/drinks/admin-drinks.service';
import { GlobalDrinkSummary, MenuCategoryType, OwnerMenuCategory, OwnerMenuItem, OwnerRestaurant, OwnerSpecialOffer, SpecialOfferKind, SpecialOfferRequest } from './core/owner/owner.models';
import { OwnerService } from './core/owner/owner.service';

interface RestaurantForm {
  id: string | null;
  name: string;
  slug: string;
  type: EstablishmentType;
  status: RestaurantStatus;
  ownerEmail: string;
  ownerPassword: string;
  trialDays: number;
  description: string;
  logoUrl: string;
  coverImageUrl: string;
  address: string;
  phone: string;
  email: string;
  websiteUrl: string;
  instagramUrl: string;
  currency: string;
  defaultLanguage: string;
  themeKey: string;
  plan: string;
  monthlyPrice: number;
  subscriptionStatus: SubscriptionStatus;
  startsOn: string;
  expiresOn: string;
  gracePeriodEndsOn: string;
}

interface CategoryForm { id: string | null; name: string; description: string; type: MenuCategoryType; sortOrder: number; isVisible: boolean }
interface ProductForm { id: string | null; categoryId: string; globalDrinkId: string | null; name: string; description: string; price: number; servingSize: string; imageUrl: string; allergens: string; sortOrder: number; isVisible: boolean; isAvailable: boolean; isVegetarian: boolean; isSpicy: boolean; isFeatured: boolean }
interface OfferForm { id: string | null; kind: SpecialOfferKind; title: string; description: string; price: number; originalPrice: number; imageUrl: string; startsAt: string; endsAt: string; isVisible: boolean; items: string }
interface AdminDrinkForm { id: string | null; name: string; slug: string; category: string; description: string; imageUrl: string; servingOptions: string; isByGlass: boolean; sortOrder: number; isActive: boolean }
interface DrinkLibraryVariant { key: string; drink: GlobalDrinkSummary; servingSize: string | null }
interface PasswordForm { currentPassword: string; newPassword: string; confirmPassword: string }
interface ReadinessItem { label: string; ready: boolean; tab: OwnerTab }

const themeOptions: { id: ThemeType; name: string; description: string; colors: string[] }[] = [
  { id: 'classic-light', name: 'Classic Light', description: 'Svijetla tema za restorane i porodične objekte.', colors: ['#f8fafc', '#ffffff', '#84cc16'] },
  { id: 'modern-dark', name: 'Modern Dark', description: 'Tamna tema za barove, klubove i premium večernji ambijent.', colors: ['#111827', '#1f2937', '#84cc16'] },
  { id: 'premium-gold', name: 'Premium Gold', description: 'Elegantna tema za restorane s ozbiljnijim vizuelnim identitetom.', colors: ['#111827', '#27272a', '#c8a96e'] },
  { id: 'natural-green', name: 'Natural Green', description: 'Svježa tema za kafiće, zdrave menije i dnevne ponude.', colors: ['#f0fdf4', '#ffffff', '#65a30d'] },
];

const drinkCategories = [
  'Vode',
  'Gazirana pića',
  'Negazirana pića',
  'Cijeđeni sokovi',
  'Energetska pića',
  'Topli napici',
  'Točeno pivo',
  'Pivo',
  'Alkoholni napici',
  'Rakije',
  'Likeri i aperitivi',
  'Crna vina',
  'Bijela vina',
  'Rosé vina',
];

@Component({
  selector: 'app-root',
  imports: [
    CommonModule, FormsModule, LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat,
    LucideClock, LucideDownload, LucideEdit2, LucideEye, LucideFlame, LucideGlobe,
    LucideLeaf, LucideLock, LucideLogIn, LucideLogOut, LucideMail, LucideMapPin, LucideMenu, LucidePercent,
    LucidePhone, LucidePlus, LucidePower, LucideQrCode, LucideSearch,
    LucideShield, LucideSparkles, LucideStore, LucideTrash2, LucideTrendingUp,
    LucideUtensilsCrossed, LucideX, LucideEyeOff,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly adminRestaurantsService = inject(AdminRestaurantsService);
  private readonly adminDrinksService = inject(AdminDrinksService);
  private readonly qrCodeService = inject(QrCodeService);
  private readonly billingService = inject(BillingService);
  private readonly ownerService = inject(OwnerService);
  readonly auth = inject(AuthService);
  readonly themes = themeOptions;
  readonly drinkCategories = drinkCategories;
  readonly categoryTypes: { value: MenuCategoryType; label: string }[] = [
    { value: 'Food', label: 'Hrana' },
    { value: 'Drink', label: 'Piće' },
  ];
  readonly adminTabs: { id: AdminTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'restaurants', label: 'Restorani' },
    { id: 'billing', label: 'Pretplate' }, { id: 'drink-library', label: 'Biblioteka pića' },
    { id: 'themes', label: 'Teme' }, { id: 'qr-codes', label: 'QR kodovi' },
  ];
  readonly ownerTabs: { id: OwnerTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'categories', label: 'Kategorije' },
    { id: 'products', label: 'Proizvodi' }, { id: 'daily-menu', label: 'Dnevni meni' },
    { id: 'offers', label: 'Ponude' }, { id: 'settings', label: 'Postavke' }, { id: 'qr', label: 'QR kod' },
  ];
  readonly establishmentTypes: { value: EstablishmentType; label: string }[] = [
    { value: 'Restaurant', label: 'Restoran' }, { value: 'Cafe', label: 'Kafić' },
    { value: 'Bar', label: 'Bar' }, { value: 'ShishaBar', label: 'Shisha bar' }, { value: 'Club', label: 'Klub' },
    { value: 'FastFood', label: 'Fast food' }, { value: 'Other', label: 'Ostalo' },
  ];
  readonly subscriptionStatuses: { value: SubscriptionStatus; label: string }[] = [
    { value: 'Trial', label: 'Probni period' }, { value: 'Active', label: 'Aktivna' },
    { value: 'Overdue', label: 'Dospjela' }, { value: 'Suspended', label: 'Pauzirana' },
    { value: 'Cancelled', label: 'Otkazana' },
  ];
  readonly restaurantStatuses: { value: RestaurantStatus; label: string }[] = [
    { value: 'Draft', label: 'Priprema' }, { value: 'Active', label: 'Aktivan' },
    { value: 'Suspended', label: 'Pauziran' }, { value: 'Cancelled', label: 'Otkazan' },
  ];
  readonly paymentMethods: { value: PaymentMethod; label: string }[] = [
    { value: 'BankTransfer', label: 'Bankovna uplata' }, { value: 'Cash', label: 'Gotovina' },
    { value: 'Card', label: 'Kartica' }, { value: 'Other', label: 'Ostalo' },
  ];

  view: AppView = 'login';
  adminTab: AdminTab = 'dashboard';
  ownerTab: OwnerTab = 'dashboard';
  selectedCategory = 'all';
  publicMenuSection: 'food' | 'drink' = 'food';
  search = '';
  mobileNav = false;
  sidebarCollapsed = false;
  showHours = false;
  showProductModal = false;
  showDrinkLibraryModal = false;
  showCategoryModal = false;
  showOfferModal = false;
  showChangePasswordModal = false;
  showPassword = false;
  loginEmail = '';
  loginPassword = '';
  loginLoading = false;
  loginError = '';
  passwordForm: PasswordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
  passwordSaving = false;
  passwordError = '';
  passwordSuccess = '';
  leadForm = { businessName: '', email: '', phone: '', type: '', message: '' };
  leadLoading = false;
  leadSuccess = '';
  leadError = '';
  adminRestaurants: AdminRestaurantSummary[] = [];
  adminDashboard: AdminDashboardSummary | null = null;
  adminRestaurantsLoading = false;
  adminRestaurantsLoaded = false;
  adminRestaurantsError = '';
  adminRestaurantSearch = '';
  showRestaurantModal = false;
  restaurantModalLoading = false;
  restaurantSaving = false;
  restaurantFormError = '';
  restaurantFormSuccess = '';
  restaurantStatusUpdating = new Set<string>();
  restaurantImpersonating = new Set<string>();
  adminQrCodes: Record<string, string> = {};
  billingOverview: BillingOverview | null = null;
  billingLoading = false;
  billingLoaded = false;
  billingError = '';
  billingSearch = '';
  billingStatusFilter = 'all';
  adminDrinks: AdminGlobalDrink[] = [];
  adminDrinksLoading = false;
  adminDrinksLoaded = false;
  adminDrinksError = '';
  adminDrinkSearch = '';
  showAdminDrinkModal = false;
  adminDrinkSaving = false;
  adminDrinkUploading = false;
  adminDrinkInlineSaving = new Set<string>();
  adminDrinkFormError = '';
  adminDrinkForm = this.emptyAdminDrinkForm();
  showPaymentModal = false;
  selectedBillingAccount: BillingAccountSummary | null = null;
  paymentHistory: PaymentHistoryItem[] = [];
  paymentHistoryLoading = false;
  paymentSaving = false;
  paymentError = '';
  paymentForm = this.emptyPaymentForm();
  restaurantForm = this.emptyRestaurantForm();
  productMap: Record<string, Product[]> = {};
  ownerRestaurant: OwnerRestaurant | null = null;
  ownerViewRestaurant: Restaurant | null = null;
  ownerLoading = false;
  ownerError = '';
  ownerSaving = false;
  ownerQrCode = '';
  drinkLibrary: GlobalDrinkSummary[] = [];
  drinkLibraryLoading = false;
  drinkLibraryError = '';
  drinkLibrarySearch = '';
  drinkLibraryCategoryFilter = 'all';
  drinkLibraryCategoryId = '';
  drinkSelections: Record<string, { drinkId: string; servingSize: string | null; selected: boolean; price: number }> = {};
  categoryForm: CategoryForm = this.emptyCategoryForm();
  productForm: ProductForm = this.emptyProductForm();
  offerForm: OfferForm = this.emptyOfferForm('Promotion');
  private readonly allowedImageTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);
  private readonly maxImageUploadSize = 5 * 1024 * 1024;

  constructor() {
    this.syncRoute(this.router.url);
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.syncRoute(event.urlAfterRedirects));
  }

  get restaurant(): Restaurant {
    return this.ownerViewRestaurant ?? this.emptyRestaurantView();
  }

  get restaurantProducts(): Product[] { return this.productMap[this.restaurant.id] ?? []; }
  get ownerCategories(): OwnerMenuCategory[] { return this.ownerRestaurant?.categories ?? []; }
  get ownerItems(): OwnerMenuItem[] { return this.ownerCategories.flatMap((category) => category.items); }
  get dailyOffers(): OwnerSpecialOffer[] { return (this.ownerRestaurant?.offers ?? []).filter((offer) => offer.kind === 'DailyMenu'); }
  get promotions(): OwnerSpecialOffer[] { return (this.ownerRestaurant?.offers ?? []).filter((offer) => offer.kind === 'Promotion'); }
  get publicFoodCategories(): Category[] { return this.restaurant.categories.filter((category) => category.active && this.categoryType(category) === 'Food' && this.restaurantProducts.some((product) => product.categoryId === category.id && product.available)); }
  get publicDrinkCategories(): Category[] { return this.restaurant.categories.filter((category) => category.active && this.categoryType(category) === 'Drink' && this.restaurantProducts.some((product) => product.categoryId === category.id && product.available)); }
  get publicCategories(): Category[] { return this.publicMenuSection === 'drink' ? this.publicDrinkCategories : this.publicFoodCategories; }
  get publicProductCount(): number { return this.publicCategories.reduce((total, category) => total + this.publicProductsFor(category.id).length, 0); }
  get hasPublicMenuSections(): boolean { return this.publicFoodCategories.length > 0 && this.publicDrinkCategories.length > 0; }
  get showFoodHighlights(): boolean { return this.publicMenuSection === 'food' || !this.publicDrinkCategories.length; }
  get ownerWeeklyViews(): { label: string; views: number }[] { return this.ownerRestaurant?.analytics.weeklyViews ?? []; }
  get ownerTotalViews(): number { return this.ownerRestaurant?.analytics.totalViews ?? 0; }
  get ownerLast7DaysViews(): number { return this.ownerRestaurant?.analytics.last7DaysViews ?? 0; }
  get ownerViewsMaximum(): number {
    const peak = Math.max(0, ...this.ownerWeeklyViews.map((point) => point.views));
    return Math.max(10, Math.ceil(peak / 10) * 10);
  }
  get ownerViewsTicks(): number[] {
    const maximum = this.ownerViewsMaximum;
    return [maximum, Math.round(maximum * .75), Math.round(maximum * .5), Math.round(maximum * .25), 0];
  }
  get ownerReadyPublicProducts(): Product[] {
    const visibleCategoryIds = new Set(this.ownerCategories.filter((category) => category.isVisible).map((category) => category.id));
    return this.restaurantProducts.filter((product) => product.available && visibleCategoryIds.has(product.categoryId));
  }
  get hasReadyPublicCategory(): boolean {
    const readyProductCategoryIds = new Set(this.ownerReadyPublicProducts.map((product) => product.categoryId));
    return this.ownerCategories.some((category) => category.isVisible && readyProductCategoryIds.has(category.id));
  }
  get hasOwnerContactInfo(): boolean {
    return Boolean(this.restaurant.phone || this.restaurant.address || this.restaurant.website || this.restaurant.instagram);
  }
  get hasOwnerBusinessHours(): boolean {
    return this.restaurant.businessHours.some((hour) => !hour.closed && hour.open && hour.close);
  }
  get ownerReadinessItems(): ReadinessItem[] {
    return [
      { label: 'Meni je aktivan', ready: this.restaurant.status === 'active', tab: 'dashboard' },
      { label: 'Javni proizvodi dodani', ready: this.ownerReadyPublicProducts.length > 0, tab: 'products' },
      { label: 'Kategorije imaju proizvode', ready: this.hasReadyPublicCategory, tab: 'categories' },
      { label: 'Kontakt podaci uneseni', ready: this.hasOwnerContactInfo, tab: 'settings' },
      { label: 'Radno vrijeme uneseno', ready: this.hasOwnerBusinessHours, tab: 'settings' },
      { label: 'QR kod generisan', ready: Boolean(this.ownerQrCode), tab: 'qr' },
    ];
  }
  get adminTitle(): string { return this.adminTabs.find((item) => item.id === this.adminTab)?.label ?? ''; }
  get ownerTitle(): string { return this.ownerTabs.find((item) => item.id === this.ownerTab)?.label ?? ''; }
  get ownerThemeLabel(): string { return this.themeLabel(this.restaurant.theme); }
  get platformGrowth(): { day: string; views: number }[] {
    return (this.adminDashboard?.growth ?? []).map((point) => ({
      day: new Intl.DateTimeFormat('bs-BA', { month: 'short' }).format(new Date(`${point.month}-01T00:00:00`)),
      views: point.restaurants,
    }));
  }
  get platformChartMaximum(): number { return Math.max(4, ...this.platformGrowth.map((point) => point.views)); }
  get platformChartTicks(): number[] {
    const maximum = this.platformChartMaximum;
    return [maximum, Math.round(maximum * .75), Math.round(maximum * .5), Math.round(maximum * .25), 0];
  }
  get filteredAdminRestaurants(): AdminRestaurantSummary[] {
    const term = this.adminRestaurantSearch.trim().toLocaleLowerCase();
    return this.adminRestaurants.filter((item) => !term ||
      item.name.toLocaleLowerCase().includes(term) ||
      item.slug.toLocaleLowerCase().includes(term) ||
      (item.address ?? '').toLocaleLowerCase().includes(term));
  }
  get filteredBillingAccounts(): BillingAccountSummary[] {
    const term = this.billingSearch.trim().toLocaleLowerCase();
    return (this.billingOverview?.accounts ?? []).filter((item) =>
      (this.billingStatusFilter === 'all' || item.status === this.billingStatusFilter) &&
      (!term || item.restaurantName.toLocaleLowerCase().includes(term) || item.slug.toLocaleLowerCase().includes(term)));
  }
  get filteredAdminDrinks(): AdminGlobalDrink[] {
    const term = this.adminDrinkSearch.trim().toLocaleLowerCase();
    return this.adminDrinks.filter((item) =>
      !term ||
      item.name.toLocaleLowerCase().includes(term) ||
      item.slug.toLocaleLowerCase().includes(term) ||
      item.category.toLocaleLowerCase().includes(term) ||
      (item.description ?? '').toLocaleLowerCase().includes(term));
  }
  get adminDrinkCategoryStats(): { name: string; count: number }[] {
    return this.drinkCategories.map((name) => ({ name, count: this.adminDrinks.filter((item) => item.category === name).length }));
  }

  get filteredProducts(): Product[] {
    const term = this.search.trim().toLocaleLowerCase();
    return this.restaurantProducts.filter((product) =>
      (this.selectedCategory === 'all' || product.categoryId === this.selectedCategory) &&
      (!term || product.name.toLocaleLowerCase().includes(term) || product.description.toLocaleLowerCase().includes(term)),
    );
  }
  get filteredDrinkLibrary(): GlobalDrinkSummary[] {
    const term = this.drinkLibrarySearch.trim().toLocaleLowerCase();
    return this.drinkLibrary.filter((drink) =>
      (this.drinkLibraryCategoryFilter === 'all' || drink.category === this.drinkLibraryCategoryFilter) &&
      (!term ||
        drink.name.toLocaleLowerCase().includes(term) ||
        drink.category.toLocaleLowerCase().includes(term) ||
        (drink.description ?? '').toLocaleLowerCase().includes(term)));
  }
  get filteredDrinkVariants(): DrinkLibraryVariant[] {
    return this.filteredDrinkLibrary.flatMap((drink) => this.servingOptionsFor(drink).map((servingSize) => ({
      key: this.drinkVariantKey(drink.id, servingSize),
      drink,
      servingSize,
    })));
  }
  get selectedLibraryDrinkCount(): number {
    return Object.values(this.drinkSelections).filter((item) => item.selected).length;
  }
  get drinkLibraryCategories(): { name: string; count: number }[] {
    const categories = [...new Set(this.drinkLibrary.map((drink) => drink.category))].sort((a, b) => a.localeCompare(b, 'bs-BA'));
    return categories.map((name) => ({ name, count: this.drinkLibrary.filter((drink) => drink.category === name).length }));
  }

  enter(view: AppView, restaurantId?: string): void {
    const id = restaurantId ?? this.auth.session()?.restaurantId ?? this.restaurant.id;
    const commands: string[] = view === 'auth-login'
      ? ['/auth/login']
      : view === 'super-admin'
      ? ['/admin', 'dashboard']
      : view === 'restaurant-owner'
        ? ['/restaurant', id, 'dashboard']
        : view === 'public-menu'
          ? ['/menu', this.restaurant.slug ?? id]
          : ['/'];
    void this.router.navigate(commands);
  }

  openLogin(): void {
    this.loginError = '';
    void this.router.navigate(['/auth/login']);
  }

  openDemoMenu(): void {
    globalThis.open('/menu/demo-meni', '_blank', 'noopener,noreferrer');
  }

  toggleSidebar(): void {
    if (globalThis.innerWidth <= 900) {
      this.mobileNav = !this.mobileNav;
      return;
    }

    this.sidebarCollapsed = !this.sidebarCollapsed;
  }

  submitLeadForm(): void {
    const businessName = this.leadForm.businessName.trim();
    const email = this.leadForm.email.trim();
    const type = this.leadForm.type.trim();
    if (businessName.length < 2) {
      this.leadError = 'Naziv objekta je obavezan.';
      return;
    }
    if (!email) {
      this.leadError = 'Email adresa je obavezna kako bismo vas mogli kontaktirati.';
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.leadError = 'Unesite ispravnu email adresu, npr. ime@primjer.ba.';
      return;
    }
    if (!type) {
      this.leadError = 'Morate izabrati tip objekta.';
      return;
    }

    this.leadLoading = true;
    this.leadError = '';
    this.leadSuccess = '';
    const payload = new FormData();
    payload.append('_subject', 'Novi MeniSpot upit');
    payload.append('Naziv objekta', businessName);
    payload.append('Email', email);
    payload.append('_replyto', email);
    payload.append('email', email);
    payload.append('Telefon', this.leadForm.phone.trim());
    payload.append('Tip objekta', type);
    payload.append('Poruka', this.leadForm.message.trim());

    void fetch('https://formspree.io/f/xojojzoe', {
      method: 'POST',
      body: payload,
      headers: { Accept: 'application/json' },
    })
      .then((response) => {
        if (!response.ok) throw new Error('Formspree request failed.');
        this.leadSuccess = 'Upit je poslan. Javit ćemo vam se uskoro na email.';
        this.leadForm = { businessName: '', email: '', phone: '', type: '', message: '' };
      })
      .catch(() => {
        this.leadError = 'Upit trenutno nije moguće poslati. Pokušajte ponovo malo kasnije.';
      })
      .finally(() => this.leadLoading = false);
  }

  submitLogin(): void {
    if (!this.loginEmail.trim() || !this.loginPassword) {
      this.loginError = 'Unesi email adresu i lozinku.';
      return;
    }

    this.loginLoading = true;
    this.loginError = '';
    this.auth.login({ email: this.loginEmail.trim(), password: this.loginPassword })
      .pipe(finalize(() => this.loginLoading = false))
      .subscribe({
        next: (session) => void this.router.navigate(this.auth.dashboardUrl(session)),
        error: (error: HttpErrorResponse) => {
          this.loginError = error.status === 401
            ? 'Email ili lozinka nisu ispravni.'
            : 'Prijava trenutno nije dostupna. Provjeri da li je backend pokrenut.';
        },
      });
  }

  logout(): void {
    this.ownerRestaurant = null;
    this.ownerViewRestaurant = null;
    this.productMap = {};
    this.auth.logout();
  }

  openChangePassword(): void {
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.passwordError = '';
    this.passwordSuccess = '';
    this.showChangePasswordModal = true;
  }

  closeChangePassword(): void {
    if (this.passwordSaving) return;
    this.showChangePasswordModal = false;
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.passwordError = '';
  }

  submitPasswordChange(): void {
    const currentPassword = this.passwordForm.currentPassword;
    const newPassword = this.passwordForm.newPassword;
    const confirmPassword = this.passwordForm.confirmPassword;

    if (!currentPassword || !newPassword || !confirmPassword) {
      this.passwordError = 'Unesi trenutnu lozinku, novu lozinku i potvrdu.';
      return;
    }
    if (newPassword.length < 8) {
      this.passwordError = 'Nova lozinka mora imati najmanje 8 znakova.';
      return;
    }
    if (newPassword !== confirmPassword) {
      this.passwordError = 'Nova lozinka i potvrda se ne poklapaju.';
      return;
    }
    if (currentPassword === newPassword) {
      this.passwordError = 'Nova lozinka mora biti drugačija od trenutne.';
      return;
    }

    this.passwordSaving = true;
    this.passwordError = '';
    this.passwordSuccess = '';
    this.auth.changePassword({ currentPassword, newPassword })
      .pipe(finalize(() => this.passwordSaving = false))
      .subscribe({
        next: () => {
          this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
          this.passwordSuccess = 'Nova lozinka je uspjesno postavljena.';
        },
        error: (error: HttpErrorResponse) => {
          const errors = error.error?.errors;
          this.passwordError = Array.isArray(errors) && errors.length
            ? errors.join(' ')
            : 'Lozinka nije promijenjena. Provjeri trenutnu lozinku i pravila za novu lozinku.';
        },
      });
  }

  loadAdminRestaurants(force = false): void {
    if (this.adminRestaurantsLoading || this.adminRestaurantsLoaded && !force) return;
    this.adminRestaurantsLoading = true;
    this.adminRestaurantsError = '';
    forkJoin({ restaurants: this.adminRestaurantsService.getAll(), dashboard: this.adminRestaurantsService.getDashboard() })
      .pipe(finalize(() => this.adminRestaurantsLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ restaurants, dashboard }) => {
          this.adminRestaurants = restaurants;
          this.adminDashboard = dashboard;
          this.adminRestaurantsLoaded = true;
          void this.generateAdminQrCodes(restaurants);
        },
        error: () => this.adminRestaurantsError = 'Restorani se trenutno ne mogu učitati. Provjeri backend i pokušaj ponovo.',
      });
  }

  loadBilling(force = false): void {
    if (this.billingLoading || this.billingLoaded && !force) return;
    this.billingLoading = true;
    this.billingError = '';
    this.billingService.getOverview()
      .pipe(finalize(() => this.billingLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (overview) => {
          this.billingOverview = overview;
          this.billingLoaded = true;
        },
        error: () => this.billingError = 'Pretplate se trenutno ne mogu učitati. Provjeri backend i pokušaj ponovo.',
      });
  }

  loadAdminDrinks(force = false): void {
    if (this.adminDrinksLoading || this.adminDrinksLoaded && !force) return;
    this.adminDrinksLoading = true;
    this.adminDrinksError = '';
    this.adminDrinksService.getAll()
      .pipe(finalize(() => this.adminDrinksLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.adminDrinks = items;
          this.adminDrinksLoaded = true;
        },
        error: () => this.adminDrinksError = 'Biblioteka pića se trenutno ne može učitati.',
      });
  }

  openAdminDrink(item?: AdminGlobalDrink): void {
    this.adminDrinkForm = item ? {
      id: item.id,
      name: item.name,
      slug: item.slug,
      category: item.category,
      description: item.description ?? '',
      imageUrl: item.imageUrl ?? '',
      servingOptions: item.servingOptions ?? '',
      isByGlass: this.isGlassDescription(item.description),
      sortOrder: item.sortOrder,
      isActive: item.isActive,
    } : this.emptyAdminDrinkForm();
    this.adminDrinkFormError = '';
    this.showAdminDrinkModal = true;
  }

  closeAdminDrinkModal(): void {
    if (this.adminDrinkSaving || this.adminDrinkUploading) return;
    this.showAdminDrinkModal = false;
    this.adminDrinkFormError = '';
  }

  syncAdminDrinkSlug(): void {
    if (this.adminDrinkForm.id) return;
    this.adminDrinkForm.slug = this.adminDrinkForm.name
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  }

  saveAdminDrink(): void {
    const form = this.adminDrinkForm;
    if (!form.name.trim() || !form.category.trim()) {
      this.adminDrinkFormError = 'Naziv i kategorija su obavezni.';
      return;
    }
    this.adminDrinkSaving = true;
    this.adminDrinkFormError = '';
    const request = {
      name: form.name.trim(),
      slug: this.nullIfEmpty(form.slug),
      category: form.category.trim(),
      description: this.adminDrinkDescription(form),
      imageUrl: this.nullIfEmpty(form.imageUrl),
      servingOptions: this.nullIfEmpty(form.servingOptions),
      sortOrder: Number(form.sortOrder) || 0,
      isActive: form.isActive,
    };
    const action = form.id ? this.adminDrinksService.update(form.id, request) : this.adminDrinksService.create(request);
    action.pipe(finalize(() => this.adminDrinkSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showAdminDrinkModal = false;
        this.loadAdminDrinks(true);
        this.drinkLibrary = [];
      },
      error: (error: HttpErrorResponse) => this.adminDrinkFormError = error.error?.title ?? 'Piće nije sačuvano. Provjeri unesene podatke.',
    });
  }

  toggleAdminDrink(item: AdminGlobalDrink): void {
    this.adminDrinksService.update(item.id, this.adminDrinkRequest(item, { isActive: !item.isActive })).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => {
        this.adminDrinks = this.adminDrinks.map((drink) => drink.id === updated.id ? updated : drink);
        this.drinkLibrary = [];
      },
      error: () => this.adminDrinksError = 'Status pića nije promijenjen.',
    });
  }

  quickUpdateAdminDrink(item: AdminGlobalDrink, changes: Partial<Pick<AdminGlobalDrink, 'category' | 'servingOptions' | 'sortOrder' | 'isActive'>>): void {
    if (this.adminDrinkInlineSaving.has(item.id)) return;
    this.adminDrinkInlineSaving.add(item.id);
    this.adminDrinksError = '';
    this.adminDrinksService.update(item.id, this.adminDrinkRequest(item, changes))
      .pipe(finalize(() => this.adminDrinkInlineSaving.delete(item.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.adminDrinks = this.adminDrinks.map((drink) => drink.id === updated.id ? updated : drink);
          this.drinkLibrary = [];
        },
        error: () => this.adminDrinksError = 'Brza izmjena pića nije sačuvana.',
      });
  }

  exportAdminRestaurantsCsv(): void {
    this.downloadCsv('menispot-restaurants.csv',
      ['Name', 'Slug', 'Type', 'Status', 'Plan', 'Subscription', 'Expires on', 'Address'],
      this.filteredAdminRestaurants.map((item) => [item.name, item.slug, this.establishmentLabel(item.type), this.restaurantStatusLabel(item.status), item.plan, this.subscriptionStatusLabel(item.subscriptionStatus), item.expiresOn, item.address ?? '']));
  }

  exportAdminDrinksCsv(): void {
    this.downloadCsv('menispot-drink-library.csv',
      ['Name', 'Slug', 'Category', 'Serving options', 'Description', 'Image URL', 'Sort order', 'Active'],
      this.filteredAdminDrinks.map((item) => [item.name, item.slug, item.category, item.servingOptions ?? '', item.description ?? '', item.imageUrl ?? '', item.sortOrder, item.isActive ? 'Yes' : 'No']));
  }

  exportOwnerProductsCsv(): void {
    this.downloadCsv(`${this.restaurant.slug || 'restaurant'}-products.csv`,
      ['Name', 'Category', 'Price', 'Serving size', 'Available', 'Visible', 'Image', 'Description'],
      this.ownerItems.map((item) => [item.name, this.categoryName(item.categoryId), item.price, item.servingSize ?? '', item.isAvailable ? 'Yes' : 'No', item.isVisible ? 'Yes' : 'No', item.imageUrl ?? '', item.description ?? '']));
  }

  productWarnings(product: Product): string[] {
    const warnings: string[] = [];
    if (!product.price || product.price <= 0) warnings.push('Cijena 0');
    if (!this.ownerCategories.some((category) => category.id === product.categoryId)) warnings.push('Bez kategorije');
    if (!product.image || product.image.includes('menispot-mark.png')) warnings.push('Nema slike');
    return warnings;
  }

  numberFromInput(value: string): number {
    return Number(value) || 0;
  }

  clearAdminDrinkImage(): void { this.adminDrinkForm.imageUrl = ''; }
  clearProductImage(): void { this.productForm.imageUrl = ''; }
  clearOfferImage(): void { this.offerForm.imageUrl = ''; }

  private adminDrinkRequest(item: AdminGlobalDrink, changes: Partial<Pick<AdminGlobalDrink, 'category' | 'servingOptions' | 'sortOrder' | 'isActive'>>) {
    return {
      name: item.name,
      slug: item.slug,
      category: changes.category ?? item.category,
      description: item.description,
      imageUrl: item.imageUrl,
      servingOptions: changes.servingOptions ?? item.servingOptions,
      sortOrder: changes.sortOrder ?? item.sortOrder,
      isActive: changes.isActive ?? item.isActive,
    };
  }

  deleteAdminDrink(item: AdminGlobalDrink): void {
    if (!confirm(`Trajno obrisati piće "${item.name}" iz biblioteke? Pića koja su vlasnici već dodali ostaju u njihovim menijima.`)) return;
    this.adminDrinksService.delete(item.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.adminDrinks = this.adminDrinks.filter((drink) => drink.id !== item.id);
        this.drinkLibrary = [];
      },
      error: () => this.adminDrinksError = 'Piće nije obrisano.',
    });
  }

  selectAdminDrinkImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.adminDrinkFormError = validationError;
      input.value = '';
      return;
    }
    this.adminDrinkUploading = true;
    this.adminDrinkFormError = '';
    this.adminDrinksService.uploadImage(file)
      .pipe(finalize(() => { this.adminDrinkUploading = false; input.value = ''; }), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ url }) => this.adminDrinkForm.imageUrl = url,
        error: (error: HttpErrorResponse) => this.adminDrinkFormError = error.error?.title ?? 'Fotografija nije učitana. Maksimalna veličina je 5 MB.',
      });
  }

  openPayment(account: BillingAccountSummary): void {
    this.selectedBillingAccount = account;
    this.paymentForm = this.emptyPaymentForm(account);
    this.paymentHistory = [];
    this.paymentError = '';
    this.showPaymentModal = true;
    this.paymentHistoryLoading = true;
    this.billingService.getHistory(account.restaurantId)
      .pipe(finalize(() => this.paymentHistoryLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => this.paymentHistory = items,
        error: () => this.paymentError = 'Historija uplata se ne može učitati.',
      });
  }

  closePaymentModal(): void {
    if (this.paymentSaving) return;
    this.showPaymentModal = false;
    this.selectedBillingAccount = null;
    this.paymentError = '';
  }

  recordPayment(): void {
    const account = this.selectedBillingAccount;
    if (!account) return;
    if (this.paymentForm.amount <= 0) { this.paymentError = 'Iznos uplate mora biti veći od nule.'; return; }
    this.paymentSaving = true;
    this.paymentError = '';
    this.billingService.recordPayment(account.restaurantId, {
      amount: this.paymentForm.amount,
      currency: this.paymentForm.currency,
      paidOn: this.paymentForm.paidOn,
      coverageMonths: this.paymentForm.coverageMonths,
      method: this.paymentForm.method,
      reference: this.nullIfEmpty(this.paymentForm.reference),
      note: this.nullIfEmpty(this.paymentForm.note),
    }).pipe(finalize(() => this.paymentSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (payment) => {
        this.paymentHistory = [payment, ...this.paymentHistory];
        this.paymentForm = this.emptyPaymentForm(account);
        this.loadBilling(true);
        this.loadAdminRestaurants(true);
      },
      error: (error: HttpErrorResponse) => this.paymentError = error.error?.title ?? 'Uplata nije evidentirana.',
    });
  }

  openCreateRestaurant(): void {
    this.restaurantForm = this.emptyRestaurantForm();
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    this.showRestaurantModal = true;
  }

  openEditRestaurant(item: AdminRestaurantSummary): void {
    this.restaurantForm = { ...this.emptyRestaurantForm(), id: item.id, name: item.name, slug: item.slug, type: item.type };
    this.showRestaurantModal = true;
    this.restaurantModalLoading = true;
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    this.adminRestaurantsService.get(item.id)
      .pipe(finalize(() => this.restaurantModalLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (details) => this.restaurantForm = this.formFromDetails(details),
        error: () => this.restaurantFormError = 'Podaci restorana se ne mogu učitati.',
      });
  }

  closeRestaurantModal(): void {
    if (this.restaurantSaving) return;
    this.showRestaurantModal = false;
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
  }

  syncRestaurantSlug(): void {
    if (this.restaurantForm.id) return;
    this.restaurantForm.slug = this.restaurantForm.name
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  }

  saveRestaurant(): void {
    const form = this.restaurantForm;
    if (!form.name.trim()) { this.restaurantFormError = 'Naziv restorana je obavezan.'; return; }
    if (!form.id && (!form.slug.trim() || !form.ownerEmail.trim() || form.ownerPassword.length < 5)) {
      this.restaurantFormError = 'Slug, email vlasnika i lozinka od najmanje 5 znakova su obavezni.';
      return;
    }

    this.restaurantSaving = true;
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    const passwordChanged = !!form.id && !!form.ownerPassword.trim();
    const request: Observable<unknown> = form.id
      ? forkJoin([
          this.adminRestaurantsService.update(form.id, {
            name: form.name.trim(), slug: this.nullIfEmpty(form.slug), description: this.nullIfEmpty(form.description),
            logoUrl: this.nullIfEmpty(form.logoUrl), coverImageUrl: this.nullIfEmpty(form.coverImageUrl),
            address: this.nullIfEmpty(form.address), phone: this.nullIfEmpty(form.phone), email: this.nullIfEmpty(form.email),
            websiteUrl: this.nullIfEmpty(form.websiteUrl), instagramUrl: this.nullIfEmpty(form.instagramUrl),
            currency: form.currency.trim() || 'BAM', defaultLanguage: form.defaultLanguage.trim() || 'bs', type: form.type,
            themeKey: form.themeKey,
          }),
          this.adminRestaurantsService.updateOwnerAccess(form.id, {
            email: form.ownerEmail.trim(), newPassword: this.nullIfEmpty(form.ownerPassword),
          }),
        ]).pipe(
          concatMap(() => this.adminRestaurantsService.setStatus(form.id!, form.status)),
          concatMap(() => this.adminRestaurantsService.setSubscription(form.id!, {
            status: form.status === 'Suspended' || form.status === 'Cancelled'
              ? form.status
              : form.subscriptionStatus,
            plan: form.plan.trim() || 'Basic', monthlyPrice: form.monthlyPrice, startsOn: form.startsOn,
            expiresOn: form.expiresOn, gracePeriodEndsOn: this.nullIfEmpty(form.gracePeriodEndsOn),
          })),
        )
      : this.adminRestaurantsService.create({
          name: form.name.trim(), slug: form.slug.trim(), type: form.type, status: form.status,
          ownerEmail: form.ownerEmail.trim(), ownerPassword: form.ownerPassword, trialDays: form.trialDays,
          description: this.nullIfEmpty(form.description), logoUrl: this.nullIfEmpty(form.logoUrl),
          coverImageUrl: this.nullIfEmpty(form.coverImageUrl), address: this.nullIfEmpty(form.address),
          phone: this.nullIfEmpty(form.phone), email: this.nullIfEmpty(form.email),
          websiteUrl: this.nullIfEmpty(form.websiteUrl), instagramUrl: this.nullIfEmpty(form.instagramUrl),
          currency: form.currency.trim() || 'BAM', defaultLanguage: form.defaultLanguage.trim() || 'bs',
          themeKey: form.themeKey,
        });

    request.pipe(finalize(() => this.restaurantSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (passwordChanged) {
          this.restaurantForm.ownerPassword = '';
          this.restaurantFormSuccess = 'Nova šifra je uspješno postavljena.';
        } else {
          this.showRestaurantModal = false;
        }
        this.loadAdminRestaurants(true);
      },
      error: (error: HttpErrorResponse) => {
        this.restaurantFormError = error.error?.title ?? 'Promjene nisu sačuvane. Provjeri unesene podatke.';
      },
    });
  }

  toggleAdminRestaurant(item: AdminRestaurantSummary): void {
    if (this.restaurantStatusUpdating.has(item.id)) return;
    const status: RestaurantStatus = item.status === 'Active' ? 'Suspended' : 'Active';
    this.restaurantStatusUpdating.add(item.id);
    this.adminRestaurantsService.setStatus(item.id, status)
      .pipe(finalize(() => this.restaurantStatusUpdating.delete(item.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          item.status = status;
          this.loadAdminRestaurants(true);
        },
        error: () => this.adminRestaurantsError = 'Status restorana nije promijenjen. Pokušaj ponovo.',
      });
  }

  impersonateRestaurant(item: AdminRestaurantSummary): void {
    if (this.restaurantImpersonating.has(item.id)) return;
    this.restaurantImpersonating.add(item.id);
    this.adminRestaurantsService.impersonate(item.id)
      .pipe(finalize(() => this.restaurantImpersonating.delete(item.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.ownerRestaurant = null;
          this.ownerViewRestaurant = null;
          this.auth.startImpersonation(session);
          void this.router.navigate(this.auth.dashboardUrl(session));
        },
        error: () => this.adminRestaurantsError = 'Nije moguće otvoriti vlasnički panel za ovaj restoran.',
      });
  }

  returnToAdmin(): void {
    this.ownerRestaurant = null;
    this.ownerViewRestaurant = null;
    this.auth.stopImpersonation();
  }

  selectAdminTab(tab: AdminTab): void { void this.router.navigate(['/admin', tab]); }
  selectOwnerTab(tab: OwnerTab): void { void this.router.navigate(['/restaurant', this.restaurant.id, tab]); }

  openCategory(category?: OwnerMenuCategory): void {
    this.categoryForm = category
      ? { id: category.id, name: category.name, description: category.description ?? '', type: category.type, sortOrder: category.sortOrder, isVisible: category.isVisible }
      : this.emptyCategoryForm();
    this.showCategoryModal = true;
  }

  saveCategory(): void {
    if (!this.categoryForm.name.trim()) { this.ownerError = 'Naziv kategorije je obavezan.'; return; }
    this.ownerSaving = true;
    this.ownerService.saveCategory(this.categoryForm.id, {
      name: this.categoryForm.name.trim(), description: this.nullIfEmpty(this.categoryForm.description),
      type: this.categoryForm.type, sortOrder: this.categoryForm.sortOrder, isVisible: this.categoryForm.isVisible,
    }).pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.showCategoryModal = false; this.loadOwnerRestaurant(true); },
      error: () => this.ownerError = 'Kategorija nije sačuvana.',
    });
  }

  toggleCategory(category: OwnerMenuCategory): void {
    this.ownerService.saveCategory(category.id, { name: category.name, description: category.description, type: category.type, sortOrder: category.sortOrder, isVisible: !category.isVisible })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Vidljivost kategorije nije promijenjena.' });
  }

  deleteCategory(category: OwnerMenuCategory): void {
    if (!confirm(`Obrisati kategoriju "${category.name}" i sve njene proizvode?`)) return;
    this.ownerService.deleteCategory(category.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Kategorija nije obrisana.' });
  }

  openProduct(product?: OwnerMenuItem): void {
    this.productForm = product ? {
      id: product.id, categoryId: product.categoryId, globalDrinkId: product.globalDrinkId, name: product.name, description: product.description ?? '', price: product.price, servingSize: product.servingSize ?? '',
      imageUrl: product.globalDrinkId ? '' : product.imageUrl ?? '', allergens: product.allergens ?? '', sortOrder: product.sortOrder, isVisible: product.isVisible,
      isAvailable: product.isAvailable, isVegetarian: product.isVegetarian, isSpicy: product.isSpicy, isFeatured: product.isFeatured,
    } : this.emptyProductForm();
    this.showProductModal = true;
  }

  saveProduct(): void {
    if (!this.productForm.name.trim() || !this.productForm.categoryId) { this.ownerError = 'Naziv i kategorija proizvoda su obavezni.'; return; }
    this.ownerSaving = true;
    this.ownerService.saveItem(this.productForm.id, {
      categoryId: this.productForm.categoryId, name: this.productForm.name.trim(), description: this.nullIfEmpty(this.productForm.description),
      price: this.productForm.price, servingSize: this.nullIfEmpty(this.productForm.servingSize), imageUrl: this.nullIfEmpty(this.productForm.imageUrl), allergens: this.nullIfEmpty(this.productForm.allergens),
      sortOrder: this.productForm.sortOrder, isVisible: this.productForm.isVisible, isAvailable: this.productForm.isAvailable,
      isVegetarian: this.productForm.isVegetarian, isSpicy: this.productForm.isSpicy, isFeatured: this.productForm.isFeatured,
    }).pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.showProductModal = false; this.loadOwnerRestaurant(true); }, error: () => this.ownerError = 'Proizvod nije sačuvan.',
    });
  }

  openDrinkLibrary(): void {
    this.drinkLibraryError = '';
    this.drinkLibrarySearch = '';
    this.drinkLibraryCategoryFilter = 'all';
    this.drinkLibraryCategoryId = '';
    this.showDrinkLibraryModal = true;
    if (this.drinkLibrary.length) return;
    this.drinkLibraryLoading = true;
    this.ownerService.getDrinkLibrary().pipe(finalize(() => this.drinkLibraryLoading = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (items) => {
        this.drinkLibrary = items;
        this.drinkSelections = this.emptyDrinkSelections(items);
      },
      error: () => this.drinkLibraryError = 'Biblioteka pića nije učitana.',
    });
  }

  libraryDrinkAdded(drinkId: string, servingSize?: string | null): boolean {
    return this.ownerItems.some((item) => item.globalDrinkId === drinkId && this.normalizeServing(item.servingSize) === this.normalizeServing(servingSize));
  }

  toggleLibraryDrink(variant: DrinkLibraryVariant): void {
    if (this.libraryDrinkAdded(variant.drink.id, variant.servingSize)) return;
    const current = this.drinkSelections[variant.key] ?? { drinkId: variant.drink.id, servingSize: variant.servingSize, selected: false, price: 0 };
    this.drinkSelections = { ...this.drinkSelections, [variant.key]: { ...current, selected: !current.selected } };
  }

  addSelectedLibraryDrinks(): void {
    const drinks = Object.entries(this.drinkSelections)
      .filter(([, selection]) => selection.selected && !this.libraryDrinkAdded(selection.drinkId, selection.servingSize))
      .map(([, selection]) => ({ drinkId: selection.drinkId, servingSize: selection.servingSize, price: Number(selection.price) || 0, isVisible: true, isAvailable: true }));
    if (!drinks.length) { this.drinkLibraryError = 'Odaberi barem jedno piće iz biblioteke.'; return; }
    this.ownerSaving = true;
    this.drinkLibraryError = '';
    this.ownerService.addLibraryDrinks({ categoryId: this.drinkLibraryCategoryId || null, drinks })
      .pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.showDrinkLibraryModal = false;
          this.drinkSelections = this.emptyDrinkSelections(this.drinkLibrary);
          this.loadOwnerRestaurant(true);
        },
        error: () => this.drinkLibraryError = 'Pića nisu dodana u meni.',
      });
  }

  toggleProduct(product: Product): void {
    const source = this.ownerRestaurant?.categories.flatMap((category) => category.items).find((item) => item.id === product.id);
    if (!source) { product.available = !product.available; return; }
    this.ownerService.saveItem(source.id, {
      categoryId: source.categoryId, name: source.name, description: source.description, price: source.price,
      servingSize: source.servingSize, imageUrl: source.globalDrinkId ? null : source.imageUrl, allergens: source.allergens, sortOrder: source.sortOrder,
      isVisible: source.isVisible, isAvailable: !source.isAvailable, isVegetarian: source.isVegetarian, isSpicy: source.isSpicy, isFeatured: source.isFeatured,
    })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Dostupnost proizvoda nije promijenjena.' });
  }

  deleteProduct(product: Product): void {
    if (!confirm(`Obrisati proizvod "${product.name}"?`)) return;
    this.ownerService.deleteItem(product.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Proizvod nije obrisan.' });
  }

  openOffer(kind: SpecialOfferKind, offer?: OwnerSpecialOffer): void {
    this.offerForm = offer ? {
      id: offer.id, kind: offer.kind, title: offer.title, description: offer.description ?? '', price: offer.price ?? 0,
      originalPrice: offer.originalPrice ?? 0, imageUrl: offer.imageUrl ?? '', startsAt: this.dateTimeInputValue(offer.startsAt),
      endsAt: this.dateTimeInputValue(offer.endsAt), isVisible: offer.isVisible, items: offer.items ?? '',
    } : this.emptyOfferForm(kind);
    this.showOfferModal = true;
  }

  saveOffer(): void {
    if (!this.offerForm.title.trim()) { this.ownerError = 'Naziv ponude je obavezan.'; return; }
    this.ownerSaving = true;
    this.ownerService.saveOffer(this.offerForm.id, {
      title: this.offerForm.title.trim(), description: this.nullIfEmpty(this.offerForm.description), price: this.offerForm.price || null,
      originalPrice: this.offerForm.originalPrice || null, imageUrl: this.nullIfEmpty(this.offerForm.imageUrl),
      startsAt: this.isoDateTime(this.offerForm.startsAt), endsAt: this.isoDateTime(this.offerForm.endsAt), isVisible: this.offerForm.isVisible,
      kind: this.offerForm.kind, items: this.nullIfEmpty(this.offerForm.items),
    }).pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.showOfferModal = false; this.loadOwnerRestaurant(true); }, error: () => this.ownerError = 'Ponuda nije sačuvana.',
    });
  }

  toggleOffer(offer: OwnerSpecialOffer): void {
    this.ownerService.saveOffer(offer.id, this.offerRequestFromOffer(offer, !offer.isVisible))
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Status ponude nije promijenjen.' });
  }

  deleteOffer(offer: OwnerSpecialOffer): void {
    if (!confirm(`Obrisati ponudu "${offer.title}"?`)) return;
    this.ownerService.deleteOffer(offer.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Ponuda nije obrisana.' });
  }

  changeTheme(theme: ThemeType): void {
    if (!this.ownerRestaurant) { this.restaurant.theme = theme; return; }
    const selected = this.themes.find((item) => item.id === theme)!;
    const request = { ...this.ownerRestaurant.theme, themeKey: theme, primaryColor: selected.colors[1], accentColor: selected.colors[2] };
    this.ownerService.setTheme(request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Tema nije sačuvana.' });
  }

  saveOwnerSettings(): void {
    if (!this.ownerRestaurant) return;
    this.ownerSaving = true;
    this.ownerService.updateRestaurant({
      name: this.restaurant.name, description: this.ownerRestaurant.description, logoUrl: this.restaurant.logo || null,
      coverImageUrl: this.restaurant.cover || null, address: this.restaurant.address || null, phone: this.restaurant.phone || null,
      email: this.ownerRestaurant.email, websiteUrl: this.restaurant.website || null, instagramUrl: this.restaurant.instagram || null,
      currency: this.ownerRestaurant.currency, defaultLanguage: this.ownerRestaurant.defaultLanguage, type: this.ownerRestaurant.type,
      themeKey: this.restaurant.theme,
    }).pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Postavke nisu sačuvane.',
    });
  }

  saveBusinessHours(): void {
    if (!this.ownerRestaurant) return;
    this.ownerService.setBusinessHours(this.ownerRestaurant.businessHours)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.loadOwnerRestaurant(true), error: () => this.ownerError = 'Radno vrijeme nije sačuvano.' });
  }

  downloadOwnerQr(): void {
    if (!this.ownerQrCode || !this.ownerRestaurant) return;
    const link = document.createElement('a'); link.href = this.ownerQrCode; link.download = `${this.ownerRestaurant.slug}-qr.png`; link.click();
  }

  selectImage(event: Event, target: 'product' | 'offer' | 'logo' | 'cover'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.ownerError = validationError;
      input.value = '';
      return;
    }
    this.ownerSaving = true;
    this.ownerService.uploadImage(file).pipe(finalize(() => { this.ownerSaving = false; input.value = ''; }), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ url }) => {
        if (target === 'product') this.productForm.imageUrl = url;
        else if (target === 'offer') this.offerForm.imageUrl = url;
        else if (target === 'logo') this.restaurant.logo = url;
        else this.restaurant.cover = url;
      },
      error: (error: HttpErrorResponse) => this.ownerError = error.error?.title ?? 'Fotografija nije učitana. Maksimalna veličina je 5 MB.',
    });
  }

  productsFor(categoryId: string): Product[] {
    return this.filteredProducts.filter((product) => product.categoryId === categoryId);
  }
  publicProductsFor(categoryId: string): Product[] {
    const term = this.search.trim().toLocaleLowerCase();
    return this.restaurantProducts.filter((product) =>
      product.categoryId === categoryId &&
      product.available &&
      (this.selectedCategory === 'all' || product.categoryId === this.selectedCategory) &&
      (!term || product.name.toLocaleLowerCase().includes(term) || product.description.toLocaleLowerCase().includes(term)),
    );
  }
  selectPublicMenuSection(section: 'food' | 'drink'): void {
    this.publicMenuSection = section;
    this.selectedCategory = 'all';
  }

  categoryName(categoryId: string): string {
    return this.restaurant.categories.find((item) => item.id === categoryId)?.name ?? '';
  }
  categoryType(category: Category): MenuCategoryType {
    return category.type ?? this.ownerRestaurant?.categories.find((item) => item.id === category.id)?.type ?? 'Food';
  }

  ownerItem(id: string): OwnerMenuItem | undefined { return this.ownerItems.find((item) => item.id === id); }

  loadOwnerAgain(): void { this.loadOwnerRestaurant(true); }

  dayName(day: string): string { return this.dayLabel(day); }

  badgeLabel(badge: BadgeType): string {
    return ({ new: 'Novo', popular: 'Popularno', spicy: 'Ljuto', vegetarian: 'Veg', 'chefs-choice': 'Chef preporučuje' })[badge];
  }

  formatPrice(price: number): string {
    return new Intl.NumberFormat('bs-BA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(price) + ' KM';
  }

  establishmentLabel(type: EstablishmentType): string {
    return this.establishmentTypes.find((item) => item.value === type)?.label ?? type;
  }

  themeLabel(theme: string): string {
    return this.themes.find((item) => item.id === theme)?.name ?? theme;
  }

  restaurantStatusLabel(status: RestaurantStatus): string {
    return ({ Draft: 'Priprema', Active: 'Aktivan', Suspended: 'Pauziran', Cancelled: 'Otkazan' })[status];
  }

  subscriptionStatusLabel(status: SubscriptionStatus): string {
    return this.subscriptionStatuses.find((item) => item.value === status)?.label ?? status;
  }

  paymentMethodLabel(method: PaymentMethod): string {
    return this.paymentMethods.find((item) => item.value === method)?.label ?? method;
  }

  formatMoney(amount: number, currency = 'BAM'): string {
    return new Intl.NumberFormat('bs-BA', { style: 'currency', currency }).format(amount);
  }

  formatMoneyTotals(totals: { currency: string; amount: number }[] | undefined): string {
    return totals?.length ? totals.map((item) => this.formatMoney(item.amount, item.currency)).join(' · ') : this.formatMoney(0);
  }

  syncPaymentAmount(): void {
    if (!this.selectedBillingAccount) return;
    this.paymentForm.amount = Math.round(this.selectedBillingAccount.monthlyPrice * this.paymentForm.coverageMonths * 100) / 100;
  }

  syncPlanPrice(): void {
    const prices: Record<string, number> = { Basic: 39.90, Standard: 59.90, Premium: 99.90, Enterprise: 149.90 };
    this.restaurantForm.monthlyPrice = prices[this.restaurantForm.plan] ?? this.restaurantForm.monthlyPrice;
  }

  themeUsageCount(themeKey: string): number {
    return this.adminDashboard?.themeUsage.find((item) => item.themeKey === themeKey)?.count ?? 0;
  }

  publicMenuUrl(item: AdminRestaurantSummary): string {
    return `${globalThis.location.origin}/menu/${item.slug}?source=qr`;
  }

  downloadRestaurantQr(item: AdminRestaurantSummary): void {
    const dataUrl = this.adminQrCodes[item.id];
    if (!dataUrl) return;
    const link = document.createElement('a');
    link.href = dataUrl;
    link.download = `${item.slug}-qr.png`;
    link.click();
  }

  chartHeight(value: number, maximum: number): number {
    return Math.max(4, Math.round((value / maximum) * 100));
  }

  qrCells(seed: string): number[] {
    const values: number[] = [];
    for (let i = 0; i < 225; i++) values.push((seed.charCodeAt(i % seed.length) + i * 7) % 3 ? 1 : 0);
    return values;
  }

  private syncRoute(url: string): void {
    const segments = url.split('?')[0].split('/').filter(Boolean).map(decodeURIComponent);
    if (segments[0] === 'auth' && segments[1] === 'login') {
      this.view = 'auth-login';
    } else if (segments[0] === 'admin') {
      this.view = 'super-admin';
      this.adminTab = this.isAdminTab(segments[1]) ? segments[1] : 'dashboard';
      this.loadAdminRestaurants();
      if (this.adminTab === 'billing') this.loadBilling();
      if (this.adminTab === 'drink-library') this.loadAdminDrinks();
    } else if (segments[0] === 'restaurant') {
      this.view = 'restaurant-owner';
      this.ownerTab = this.isOwnerTab(segments[2]) ? segments[2] : 'dashboard';
      this.loadOwnerRestaurant(true);
    } else if (segments[0] === 'menu') {
      this.view = 'public-menu';
      this.loadPublicMenu(segments[1] ?? '');
    } else {
      this.view = 'login';
    }

    this.mobileNav = false;
    this.search = '';
    this.selectedCategory = 'all';
  }

  private isAdminTab(value?: string): value is AdminTab {
    return this.adminTabs.some((tab) => tab.id === value);
  }

  private isOwnerTab(value?: string): value is OwnerTab {
    return this.ownerTabs.some((tab) => tab.id === value);
  }

  private emptyRestaurantForm(): RestaurantForm {
    const today = new Date();
    const expires = new Date(today);
    expires.setDate(expires.getDate() + 30);
    return {
      id: null, name: '', slug: '', type: 'Restaurant', status: 'Active', ownerEmail: '', ownerPassword: '', trialDays: 30,
      description: '', logoUrl: '', coverImageUrl: '', address: '', phone: '', email: '', websiteUrl: '', instagramUrl: '',
      currency: 'BAM', defaultLanguage: 'bs', themeKey: 'classic-light', plan: 'Basic', monthlyPrice: 39.90, subscriptionStatus: 'Trial',
      startsOn: this.dateInputValue(today), expiresOn: this.dateInputValue(expires), gracePeriodEndsOn: '',
    };
  }

  private formFromDetails(item: AdminRestaurantDetails): RestaurantForm {
    return {
      id: item.id, name: item.name, slug: item.slug, type: item.type, status: item.status,
      ownerEmail: item.ownerEmail ?? '', ownerPassword: '', trialDays: 30,
      description: item.description ?? '', logoUrl: item.logoUrl ?? '', coverImageUrl: item.coverImageUrl ?? '',
      address: item.address ?? '', phone: item.phone ?? '', email: item.email ?? '', websiteUrl: item.websiteUrl ?? '',
      instagramUrl: item.instagramUrl ?? '', currency: item.currency, defaultLanguage: item.defaultLanguage, themeKey: item.themeKey,
      plan: item.subscription.plan, monthlyPrice: item.subscription.monthlyPrice, subscriptionStatus: item.subscription.status, startsOn: item.subscription.startsOn,
      expiresOn: item.subscription.expiresOn, gracePeriodEndsOn: item.subscription.gracePeriodEndsOn ?? '',
    };
  }

  private dateInputValue(date: Date): string {
    return date.toISOString().slice(0, 10);
  }

  private nullIfEmpty(value: string): string | null {
    return value.trim() || null;
  }

  private servingOptionsFor(drink: GlobalDrinkSummary): (string | null)[] {
    const options = (drink.servingOptions ?? '').split(',').map((value) => value.trim()).filter(Boolean);
    return options.length ? options : [null];
  }

  private emptyDrinkSelections(items: GlobalDrinkSummary[]): Record<string, { drinkId: string; servingSize: string | null; selected: boolean; price: number }> {
    return Object.fromEntries(items.flatMap((item) => this.servingOptionsFor(item).map((servingSize) => {
      const key = this.drinkVariantKey(item.id, servingSize);
      return [key, { drinkId: item.id, servingSize, selected: false, price: this.drinkSelections[key]?.price ?? 0 }];
    })));
  }

  private drinkVariantKey(drinkId: string, servingSize?: string | null): string {
    return `${drinkId}:${this.normalizeServing(servingSize)}`;
  }

  private normalizeServing(value?: string | null): string {
    return (value ?? '').trim().toLocaleLowerCase();
  }

  defaultServingOptions(category: string): string {
    const options: Record<string, string> = {
      'Vode': '0.25l, 0.33l, 0.50l, 0.75l, 1.00l',
      'Gazirana pića': '0.20l, 0.25l, 0.33l, 0.50l, 1.00l',
      'Negazirana pića': '0.20l, 0.25l, 0.33l, 0.50l, 1.00l',
      'Cijeđeni sokovi': '0.20l, 0.25l, 0.30l, 0.40l, 0.50l',
      'Energetska pića': '0.25l, 0.33l, 0.50l',
      'Topli napici': 'porcija',
      'Točeno pivo': '0.20l, 0.25l, 0.30l, 0.33l, 0.40l, 0.50l, 1.00l',
      'Pivo': '0.25l, 0.33l, 0.50l',
      'Alkoholni napici': '0.03l, 0.04l, 0.05l',
      'Rakije': '0.03l, 0.04l, 0.05l',
      'Likeri i aperitivi': '0.03l, 0.04l, 0.05l',
      'Crna vina': '0.10l, 0.15l, 0.187l, 0.75l, 1.00l',
      'Bijela vina': '0.10l, 0.15l, 0.187l, 0.75l, 1.00l',
      'Rosé vina': '0.10l, 0.15l, 0.187l, 0.75l, 1.00l',
    };
    return options[category] ?? 'porcija';
  }

  updateAdminDrinkGlassFlag(): void {
    if (this.adminDrinkForm.isByGlass) {
      this.adminDrinkForm.description = 'Vino na čašu';
    } else if (this.isGlassDescription(this.adminDrinkForm.description)) {
      this.adminDrinkForm.description = '';
    }
  }

  private adminDrinkDescription(form: AdminDrinkForm): string | null {
    return form.isByGlass ? 'Vino na čašu' : this.nullIfEmpty(form.description);
  }

  private isGlassDescription(value?: string | null): boolean {
    return (value ?? '').trim().toLocaleLowerCase('bs-BA') === 'vino na čašu';
  }

  private emptyPaymentForm(account?: BillingAccountSummary): { amount: number; currency: string; paidOn: string; coverageMonths: number; method: PaymentMethod; reference: string; note: string } {
    return {
      amount: account?.monthlyPrice ?? 0,
      currency: account?.currency ?? 'BAM',
      paidOn: this.dateInputValue(new Date()),
      coverageMonths: 1,
      method: 'BankTransfer',
      reference: '',
      note: '',
    };
  }

  private emptyAdminDrinkForm(): AdminDrinkForm {
    return {
      id: null,
      name: '',
      slug: '',
      category: this.drinkCategories[0],
      description: '',
      imageUrl: '',
      servingOptions: this.defaultServingOptions(this.drinkCategories[0]),
      isByGlass: false,
      sortOrder: this.adminDrinks.length + 1,
      isActive: true,
    };
  }

  private emptyRestaurantView(): Restaurant {
    return {
      id: '',
      name: 'MeniSpot',
      address: '',
      phone: '',
      website: '',
      instagram: '',
      cover: '/menispot-mark.png',
      logo: '/menispot-mark.png',
      status: 'paused',
      subscription: 'basic',
      theme: 'classic-light',
      themeColor: '#84cc16',
      categories: [],
      businessHours: [],
      dailyMenu: [],
      offers: [],
    };
  }

  private async generateAdminQrCodes(items: AdminRestaurantSummary[]): Promise<void> {
    const entries = await Promise.all(items.map(async (item) => [item.id, await this.qrCodeService.createDataUrl(this.publicMenuUrl(item))] as const));
    this.adminQrCodes = Object.fromEntries(entries);
  }

  private loadOwnerRestaurant(force = false): void {
    if (this.ownerLoading || this.view !== 'restaurant-owner' || this.ownerRestaurant && !force) return;
    this.ownerLoading = true;
    this.ownerError = '';
    this.ownerService.getRestaurant().pipe(finalize(() => this.ownerLoading = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (restaurant) => this.applyOwnerRestaurant(restaurant),
      error: () => this.ownerError = 'Podaci restorana nisu učitani. Provjeri da li je API pokrenut.',
    });
  }

  private loadPublicMenu(slug: string): void {
    if (!slug) return;
    this.ownerLoading = true;
    this.ownerError = '';
    this.ownerService.getPublicMenu(slug).pipe(finalize(() => this.ownerLoading = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ restaurant }) => this.applyOwnerRestaurant(restaurant),
      error: () => { this.ownerRestaurant = null; this.ownerViewRestaurant = null; this.ownerError = 'Ovaj meni trenutno nije dostupan.'; },
    });
  }

  private applyOwnerRestaurant(restaurant: OwnerRestaurant): void {
    this.ownerRestaurant = { ...restaurant, businessHours: this.completeBusinessHours(restaurant.businessHours) };
    const products: Product[] = restaurant.categories.flatMap((category) => category.items.map((item) => ({
      id: item.id, categoryId: item.categoryId, name: item.name, description: item.description ?? '', price: item.price,
      servingSize: item.servingSize,
      image: item.imageUrl || '/menispot-mark.png', available: item.isAvailable && item.isVisible,
      badges: [item.isVegetarian ? 'vegetarian' : null, item.isSpicy ? 'spicy' : null, item.isFeatured ? 'chefs-choice' : null].filter(Boolean) as BadgeType[],
      allergens: (item.allergens ?? '').split(',').map((value) => value.trim()).filter(Boolean),
    })));
    const fallbackCover = '/menispot-mark.png';
    this.ownerViewRestaurant = {
      id: restaurant.id, slug: restaurant.slug, name: restaurant.name, address: restaurant.address ?? '', phone: restaurant.phone ?? '',
      website: restaurant.websiteUrl ?? '', instagram: restaurant.instagramUrl ?? '', cover: restaurant.coverImageUrl || fallbackCover,
      logo: restaurant.logoUrl || '/menispot-mark.png', status: restaurant.status === 'Active' ? 'active' : 'paused', subscription: 'basic',
      theme: restaurant.theme.themeKey as ThemeType, themeColor: restaurant.theme.accentColor,
      categories: restaurant.categories.map((category) => ({ id: category.id, name: category.name, icon: category.type === 'Drink' ? '🥤' : '🍽', order: category.sortOrder, active: category.isVisible, type: category.type })),
      businessHours: this.completeBusinessHours(restaurant.businessHours).map((hour) => ({ day: this.dayLabel(hour.dayOfWeek), open: hour.opensAt?.slice(0, 5) ?? '', close: hour.closesAt?.slice(0, 5) ?? '', closed: hour.isClosed })),
      dailyMenu: restaurant.offers.filter((offer) => offer.kind === 'DailyMenu' && offer.isVisible).map((offer) => ({ id: offer.id, name: offer.title, items: (offer.items ?? '').split('\n').map((item) => item.trim()).filter(Boolean), price: offer.price ?? 0, date: this.offerDateLabel(offer), active: offer.isVisible })),
      offers: restaurant.offers.filter((offer) => offer.kind === 'Promotion' && offer.isVisible).map((offer) => ({ id: offer.id, name: offer.title, description: offer.description ?? '', originalPrice: offer.originalPrice ?? offer.price ?? 0, offerPrice: offer.price ?? 0, image: offer.imageUrl || fallbackCover, validUntil: offer.endsAt ? new Intl.DateTimeFormat('bs-BA').format(new Date(offer.endsAt)) : 'Trajna ponuda', active: offer.isVisible })),
    };
    this.productMap[restaurant.id] = products;
    const drinkCategoryIds = new Set(restaurant.categories.filter((category) => category.type === 'Drink').map((category) => category.id));
    const foodCategoryIds = new Set(restaurant.categories.filter((category) => category.type === 'Food').map((category) => category.id));
    const hasDrinkProducts = products.some((product) => drinkCategoryIds.has(product.categoryId) && product.available);
    const hasFoodProducts = products.some((product) => foodCategoryIds.has(product.categoryId) && product.available);
    this.publicMenuSection = hasDrinkProducts && (!hasFoodProducts || restaurant.type !== 'Restaurant' && restaurant.type !== 'FastFood') ? 'drink' : 'food';
    this.selectedCategory = 'all';
    void this.qrCodeService.createDataUrl(`${globalThis.location.origin}/menu/${restaurant.slug}`).then((value) => this.ownerQrCode = value);
  }

  private emptyCategoryForm(): CategoryForm { return { id: null, name: '', description: '', type: 'Food', sortOrder: (this.ownerCategories.at(-1)?.sortOrder ?? 0) + 1, isVisible: true }; }
  private emptyProductForm(): ProductForm { return { id: null, categoryId: this.ownerCategories[0]?.id ?? '', globalDrinkId: null, name: '', description: '', price: 0, servingSize: '', imageUrl: '', allergens: '', sortOrder: this.ownerItems.length + 1, isVisible: true, isAvailable: true, isVegetarian: false, isSpicy: false, isFeatured: false }; }
  private emptyOfferForm(kind: SpecialOfferKind): OfferForm { return { id: null, kind, title: '', description: '', price: 0, originalPrice: 0, imageUrl: '', startsAt: '', endsAt: '', isVisible: true, items: '' }; }
  private dateTimeInputValue(value: string | null): string { return value ? new Date(value).toISOString().slice(0, 16) : ''; }
  private isoDateTime(value: string): string | null { return value ? new Date(value).toISOString() : null; }
  private offerRequestFromOffer(offer: OwnerSpecialOffer, isVisible = offer.isVisible): SpecialOfferRequest {
    return {
      title: offer.title,
      description: offer.description,
      price: offer.price,
      originalPrice: offer.originalPrice,
      imageUrl: offer.imageUrl,
      startsAt: offer.startsAt,
      endsAt: offer.endsAt,
      isVisible,
      kind: offer.kind,
      items: offer.items,
    };
  }
  private offerDateLabel(offer: OwnerSpecialOffer): string { return offer.startsAt ? new Intl.DateTimeFormat('bs-BA', { dateStyle: 'medium' }).format(new Date(offer.startsAt)) : 'Danas'; }
  private dayLabel(day: string): string { return ({ Monday: 'Ponedjeljak', Tuesday: 'Utorak', Wednesday: 'Srijeda', Thursday: 'Četvrtak', Friday: 'Petak', Saturday: 'Subota', Sunday: 'Nedjelja' } as Record<string, string>)[day] ?? day; }
  private validateImageFile(file: File): string | null {
    if (!this.allowedImageTypes.has(file.type)) return 'Dozvoljene su samo JPG, PNG ili WebP fotografije.';
    if (file.size > this.maxImageUploadSize) return 'Fotografija može biti maksimalno 5 MB.';
    return null;
  }
  private downloadCsv(fileName: string, headers: string[], rows: (string | number | boolean | null | undefined)[][]): void {
    const csv = [headers, ...rows]
      .map((row) => row.map((value) => this.csvCell(value)).join(','))
      .join('\r\n');
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
  private csvCell(value: string | number | boolean | null | undefined): string {
    const text = String(value ?? '');
    return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
  }
  private completeBusinessHours(hours: OwnerRestaurant['businessHours']): OwnerRestaurant['businessHours'] {
    const days: OwnerRestaurant['businessHours'][number]['dayOfWeek'][] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
    return days.map((day) => hours.find((hour) => hour.dayOfWeek === day) ?? { dayOfWeek: day, opensAt: '09:00:00', closesAt: '23:00:00', isClosed: false });
  }
}

