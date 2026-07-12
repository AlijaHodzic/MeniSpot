import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { concatMap, filter, finalize, forkJoin, Observable, of } from 'rxjs';
import {
  LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat, LucideClock, LucideEdit2,
  LucideDownload, LucideEye, LucideEyeOff, LucideFlame, LucideGlobe, LucideLeaf, LucideLock, LucideLogIn, LucideLogOut, LucideMail,
  LucideMapPin, LucideMenu, LucideMoon, LucidePercent, LucidePhone, LucidePlus,
  LucidePower, LucideQrCode, LucideSearch, LucideShield, LucideSparkles,
  LucideStore, LucideSun, LucideTrash2, LucideTrendingUp, LucideUtensilsCrossed, LucideX,
} from '@lucide/angular';
import { AdminTab, AppView, BadgeType, Category, DailyMenu, Offer, OwnerTab, Product, Restaurant, ThemeType } from './models';
import { AuthService } from './core/auth/auth.service';
import {
  AdminReadinessIssue,
  AdminRestaurantReadiness,
  AdminRestaurantDetails,
  AdminRestaurantSummary,
  AdminDashboardSummary,
  AuditLogSummary,
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
import { GlobalDrinkSummary, MenuCategoryType, OwnerMenuCategory, OwnerMenuItem, OwnerRestaurant, OwnerSpecialOffer, SpecialOfferKind, SpecialOfferRequest, SupportTicket, SupportTicketPriority, SupportTicketStatus, SupportTicketType } from './core/owner/owner.models';
import { OwnerService } from './core/owner/owner.service';
import { AppSelectComponent, AppSelectOption } from './shared/app-select.component';
import { API_URL } from './core/api.config';

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
  enabledLanguages: string;
  themeKey: string;
  plan: string;
  monthlyPrice: number;
  subscriptionStatus: SubscriptionStatus;
  startsOn: string;
  expiresOn: string;
  gracePeriodEndsOn: string;
}

interface CategoryForm { id: string | null; name: string; description: string; nameEn: string; descriptionEn: string; nameDe: string; descriptionDe: string; type: MenuCategoryType; sortOrder: number; isVisible: boolean }
interface ProductForm {
  id: string | null;
  categoryId: string;
  globalDrinkId: string | null;
  name: string;
  description: string;
  nameEn: string;
  descriptionEn: string;
  nameDe: string;
  descriptionDe: string;
  price: number;
  servingSize: string;
  imageUrl: string;
  allergens: string;
  ingredients: string;
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
interface OfferForm { id: string | null; kind: SpecialOfferKind; title: string; description: string; titleEn: string; descriptionEn: string; itemsEn: string; titleDe: string; descriptionDe: string; itemsDe: string; price: number; originalPrice: number; imageUrl: string; startsAt: string; endsAt: string; isVisible: boolean; items: string }
interface AdminDrinkForm { id: string | null; name: string; slug: string; category: string; description: string; imageUrl: string; servingOptions: string; isByGlass: boolean; sortOrder: number; isActive: boolean }
interface DrinkLibraryVariant { key: string; drink: GlobalDrinkSummary; servingSize: string | null }
interface PasswordForm { currentPassword: string; newPassword: string; confirmPassword: string }
interface ReadinessItem { label: string; ready: boolean; tab: OwnerTab }
type ToastType = 'success' | 'error' | 'info';
interface ToastMessage { id: number; type: ToastType; message: string }
interface ConfirmDialog { title: string; message: string; confirmText: string; tone?: 'danger' | 'warning'; onConfirm: () => void }
interface SupportForm { title: string; type: SupportTicketType; priority: SupportTicketPriority; message: string; attachmentUrl: string }
type MenuLanguage = 'bs' | 'en' | 'de';
type UploadTarget = 'product' | 'offer' | 'logo' | 'cover';

type ThemeGroupId = 'restaurant' | 'cafe' | 'bar' | 'fast-food';
interface ThemeOption { id: ThemeType; group: ThemeGroupId; name: string; description: string; colors: string[] }
type SubscriptionPlan = 'Start' | 'Pro' | 'Premium';

const subscriptionPlanPrices: Record<SubscriptionPlan, number> = { Start: 29, Pro: 49, Premium: 79 };
const subscriptionPlanFeatures: Record<SubscriptionPlan, string[]> = {
  Start: ['QR digitalni meni', 'Proizvodi i kategorije', 'Biblioteka pica za brzo dodavanje', 'Dnevni meni i posebne ponude'],
  Pro: ['Sve iz Start paketa', 'Pregledi menija i QR counter', 'Sedmicna i 30 dana statistika', 'Prioritetna pomoc kod izmjena'],
  Premium: ['Sve iz Pro paketa', 'Najgledaniji proizvodi', 'Nutritivne vrijednosti i sastojci', 'Brand tema po objektu'],
};

const themeGroupOptions: { id: ThemeGroupId; name: string; description: string }[] = [
  { id: 'restaurant', name: 'Restorani', description: 'Elegantnije i univerzalne palete za restorane.' },
  { id: 'cafe', name: 'Kafici', description: 'Toplije i mekse teme za kafe, brunch i slastice.' },
  { id: 'bar', name: 'Barovi', description: 'Tamne i premium palete za barove, klubove i shisha lokale.' },
  { id: 'fast-food', name: 'Fast food', description: 'Energeticne boje za pizzu, burgere, grill i brzu hranu.' },
];

const themeOptions: ThemeOption[] = [
  { id: 'classic-light', group: 'restaurant', name: 'Classic Light', description: 'Cista svijetla tema za restorane i porodicne objekte.', colors: ['#f8fafc', '#ffffff', '#84cc16'] },
  { id: 'premium-gold', group: 'restaurant', name: 'Premium Gold', description: 'Tamna elegantna tema sa zlatnim akcentom.', colors: ['#111827', '#27272a', '#c8a96e'] },
  { id: 'burgundy-dining', group: 'restaurant', name: 'Burgundy Dining', description: 'Bordo akcent za steakhouse, vino i tradicionalni restoran.', colors: ['#18181b', '#27272a', '#be123c'] },
  { id: 'mediterranean-blue', group: 'restaurant', name: 'Mediterranean Blue', description: 'Svjeza plava paleta za riblje i moderne restorane.', colors: ['#eff6ff', '#ffffff', '#2563eb'] },
  { id: 'olive-linen', group: 'restaurant', name: 'Olive Linen', description: 'Maslinasta i lanena kombinacija za miran restoran.', colors: ['#f7f5ed', '#ffffff', '#64748b'] },
  { id: 'ocean-slate', group: 'restaurant', name: 'Ocean Slate', description: 'Tamno plava premium tema za moderan restoran.', colors: ['#0f172a', '#1e293b', '#38bdf8'] },
  { id: 'coffee-cream', group: 'cafe', name: 'Coffee Cream', description: 'Krem i kafa tonovi za kafice i slasticarne.', colors: ['#faf7f2', '#ffffff', '#92400e'] },
  { id: 'urban-espresso', group: 'cafe', name: 'Urban Espresso', description: 'Tamna coffee shop tema sa toplim akcentom.', colors: ['#1c1917', '#292524', '#d97706'] },
  { id: 'soft-pastel', group: 'cafe', name: 'Soft Pastel', description: 'Njezna pastelna tema za brunch i mirnije lokale.', colors: ['#fff7fb', '#ffffff', '#db2777'] },
  { id: 'natural-green', group: 'cafe', name: 'Fresh Mint', description: 'Svjeza mint tema za dnevne kafice i zdrave napitke.', colors: ['#f0fdf4', '#ffffff', '#65a30d'] },
  { id: 'rose-latte', group: 'cafe', name: 'Rose Latte', description: 'Topla roza i latte paleta za brunch i slastice.', colors: ['#fff1f2', '#ffffff', '#e11d48'] },
  { id: 'cocoa-mint', group: 'cafe', name: 'Cocoa Mint', description: 'Tamna kakao tema sa mint akcentom.', colors: ['#211b17', '#2f2722', '#2dd4bf'] },
  { id: 'neon-night', group: 'bar', name: 'Neon Night', description: 'Tamna neon tema za barove i klubove.', colors: ['#09090b', '#18181b', '#22d3ee'] },
  { id: 'royal-violet', group: 'bar', name: 'Royal Violet', description: 'Ljubičasta premium tema za lounge i shisha bar.', colors: ['#17112a', '#241a3a', '#a855f7'] },
  { id: 'warm-orange', group: 'fast-food', name: 'Warm Orange', description: 'Topla narandzasta za pizzu, grill i brzu hranu.', colors: ['#fff7ed', '#ffffff', '#f97316'] },
  { id: 'street-red', group: 'fast-food', name: 'Street Red', description: 'Jaka crvena za direktan street-food izgled.', colors: ['#fff1f2', '#ffffff', '#dc2626'] },
  { id: 'yellow-pop', group: 'fast-food', name: 'Yellow Pop', description: 'Zuta akcentna tema za snack, chicken i casual ponudu.', colors: ['#fefce8', '#ffffff', '#eab308'] },
  { id: 'burger-black', group: 'fast-food', name: 'Burger Black', description: 'Crna i cheddar tema za burgere i grill.', colors: ['#111827', '#1f2937', '#fb923c'] },
  { id: 'lime-street', group: 'fast-food', name: 'Lime Street', description: 'Lime akcent za street food i svjeze brze menije.', colors: ['#f7fee7', '#ffffff', '#84cc16'] },
  { id: 'modern-dark', group: 'fast-food', name: 'Charcoal Flame', description: 'Tamna grill tema sa vatrenim akcentom.', colors: ['#111827', '#1f2937', '#f59e0b'] },
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
    LucideUtensilsCrossed, LucideX, LucideEyeOff, LucideMoon, LucideSun, AppSelectComponent,
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
  readonly leadTypeOptions: AppSelectOption[] = [
    { value: '', label: 'Izaberite tip objekta', disabled: true },
    { value: 'Restoran', label: 'Restoran' },
    { value: 'Kafić', label: 'Kafić' },
    { value: 'Bar', label: 'Bar' },
    { value: 'Shisha bar', label: 'Shisha bar' },
    { value: 'Klub', label: 'Klub' },
    { value: 'Fast food', label: 'Fast food' },
    { value: 'Ostalo', label: 'Ostalo' },
  ];
  readonly categoryTypes: { value: MenuCategoryType; label: string }[] = [
    { value: 'Food', label: 'Hrana' },
    { value: 'Drink', label: 'Piće' },
  ];
  readonly adminTabs: { id: AdminTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'restaurants', label: 'Restorani' }, { id: 'archived-restaurants', label: 'Arhiva' },
    { id: 'billing', label: 'Pretplate' }, { id: 'drink-library', label: 'Biblioteka pića' },
    { id: 'themes', label: 'Teme' }, { id: 'qr-codes', label: 'QR kodovi' }, { id: 'support', label: 'Podrska' }, { id: 'audit-logs', label: 'Logovi' },
  ];
  readonly ownerTabs: { id: OwnerTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'categories', label: 'Kategorije' },
    { id: 'products', label: 'Proizvodi' }, { id: 'daily-menu', label: 'Dnevni meni' },
    { id: 'offers', label: 'Ponude' }, { id: 'settings', label: 'Postavke' }, { id: 'qr', label: 'QR kod' }, { id: 'support', label: 'Podrska' },
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
  readonly planOptions: AppSelectOption[] = (Object.keys(subscriptionPlanPrices) as SubscriptionPlan[]).map((item) => ({
    value: item,
    label: `${item} - ${subscriptionPlanPrices[item]} KM / mjesec`,
  }));
  readonly paymentCoverageOptions: AppSelectOption<number>[] = [
    { value: 1, label: '1 mjesec' }, { value: 3, label: '3 mjeseca' }, { value: 6, label: '6 mjeseci' },
    { value: 12, label: '12 mjeseci' }, { value: 24, label: '24 mjeseca' },
  ];
  readonly supportTypeOptions: AppSelectOption[] = [
    { value: 'MenuChange', label: 'Izmjena menija' },
    { value: 'Image', label: 'Slike i fotografije' },
    { value: 'Theme', label: 'Izgled i tema' },
    { value: 'TechnicalProblem', label: 'Tehnicki problem' },
    { value: 'Other', label: 'Ostalo' },
  ];
  readonly supportPriorityOptions: AppSelectOption[] = [
    { value: 'Normal', label: 'Normalno' },
    { value: 'Urgent', label: 'Hitno' },
  ];
  readonly supportStatusOptions: AppSelectOption[] = [
    { value: 'New', label: 'Novo' },
    { value: 'InProgress', label: 'U radu' },
    { value: 'Resolved', label: 'Rijeseno' },
    { value: 'Closed', label: 'Zatvoreno' },
  ];

  view: AppView = 'login';
  adminTab: AdminTab = 'dashboard';
  ownerTab: OwnerTab = 'dashboard';
  selectedCategory = 'all';
  publicMenuSection: 'food' | 'drink' = 'food';
  search = '';
  mobileNav = false;
  sidebarCollapsed = false;
  ownerDarkMode = globalThis.localStorage?.getItem('menispot-owner-theme') === 'dark';
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
  leadForm = { businessName: '', email: '', phone: '', type: '', message: '', website: '' };
  leadLoading = false;
  leadSuccess = '';
  leadError = '';
  toasts: ToastMessage[] = [];
  confirmDialog: ConfirmDialog | null = null;
  private toastId = 0;
  adminRestaurants: AdminRestaurantSummary[] = [];
  adminDashboard: AdminDashboardSummary | null = null;
  adminRestaurantsLoading = false;
  adminRestaurantsLoaded = false;
  adminRestaurantsError = '';
  adminRestaurantSearch = '';
  archivedRestaurants: AdminRestaurantSummary[] = [];
  archivedRestaurantsLoading = false;
  archivedRestaurantsLoaded = false;
  archivedRestaurantsError = '';
  archivedRestaurantSearch = '';
  showRestaurantModal = false;
  restaurantEditorMode: 'none' | 'create' | 'edit' = 'none';
  restaurantModalLoading = false;
  restaurantSaving = false;
  restaurantImageUploading: 'logo' | 'cover' | null = null;
  restaurantFormError = '';
  restaurantFormSuccess = '';
  restaurantStatusUpdating = new Set<string>();
  restaurantImpersonating = new Set<string>();
  adminQrCodes: Record<string, string> = {};
  adminReadiness: Record<string, AdminRestaurantReadiness> = {};
  qrPrintStyle: 'light' | 'dark' = 'light';
  adminSupportTickets: SupportTicket[] = [];
  adminSupportLoading = false;
  adminSupportLoaded = false;
  adminSupportError = '';
  supportStatusFilter: SupportTicketStatus | 'all' = 'all';
  supportUpdating = new Set<string>();
  auditLogs: AuditLogSummary[] = [];
  auditLogsLoading = false;
  auditLogsLoaded = false;
  auditLogsError = '';
  auditLogSearch = '';
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
  ownerSupportTickets: SupportTicket[] = [];
  ownerSupportLoading = false;
  ownerSupportLoaded = false;
  ownerSupportError = '';
  ownerSupportSaving = false;
  ownerSupportUploading = false;
  ownerSupportSuccess = '';
  supportForm: SupportForm = this.emptySupportForm();
  menuLanguage: MenuLanguage = 'bs';
  private readonly allMenuLanguageOptions: AppSelectOption[] = [
    { value: 'bs', label: 'BS' },
    { value: 'en', label: 'EN' },
    { value: 'de', label: 'DE' },
  ];
  readonly adminLanguageOptions: { value: MenuLanguage; label: string; hint: string; locked?: boolean }[] = [
    { value: 'bs', label: 'Bosanski', hint: 'Glavni jezik menija', locked: true },
    { value: 'en', label: 'Engleski', hint: 'Standardni drugi jezik', locked: true },
    { value: 'de', label: 'Njemački', hint: 'Dodatni jezik za naplatu' },
  ];
  readonly defaultLanguageOptions: AppSelectOption[] = [
    { value: 'bs', label: 'Bosanski' },
    { value: 'en', label: 'Engleski' },
    { value: 'de', label: 'Njemački' },
  ];
  imagePreviews: Partial<Record<UploadTarget, string>> = {};
  private pendingUploads: Partial<Record<UploadTarget, File>> = {};
  drinkLibrary: GlobalDrinkSummary[] = [];
  drinkLibraryLoading = false;
  drinkLibraryError = '';
  drinkLibrarySearch = '';
  drinkLibraryCategoryFilter = 'all';
  drinkLibraryCategoryId = '';
  drinkSelections: Record<string, { drinkId: string; servingSize: string | null; selected: boolean; price: number }> = {};
  categoryForm: CategoryForm = this.emptyCategoryForm();
  productForm: ProductForm = this.emptyProductForm();
  publicSelectedProduct: Product | null = null;
  offerForm: OfferForm = this.emptyOfferForm('Promotion');
  private readonly allowedImageTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);
  private readonly maxImageUploadSize = 5 * 1024 * 1024;
  private publicTrackedProducts = new Set<string>();
  private publicTrackingSessionId = '';

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
  get drinkCategoryOptions(): AppSelectOption[] { return this.drinkCategories.map((category) => ({ value: category, label: category })); }
  get billingStatusOptions(): AppSelectOption[] { return [{ value: 'all', label: 'Svi statusi' }, ...this.subscriptionStatuses]; }
  get supportStatusFilterOptions(): AppSelectOption[] { return [{ value: 'all', label: 'Svi zahtjevi' }, ...this.supportStatusOptions]; }
  get canUseOwnerSupport(): boolean { return this.normalizePlan(this.ownerRestaurant?.plan ?? 'Start') !== 'Start'; }
  get menuLanguageOptions(): AppSelectOption[] {
    const enabled = this.parseEnabledLanguages(this.ownerRestaurant?.enabledLanguages ?? 'bs,en');
    return this.allMenuLanguageOptions.filter((option) => enabled.includes(option.value as MenuLanguage));
  }
  get restaurantFormThemeOptions(): AppSelectOption[] {
    return this.themesForGroup(this.themeGroupForEstablishmentType(this.restaurantForm.type))
      .map((theme) => ({ value: theme.id, label: theme.name }));
  }
  get ownerThemeGroups(): typeof themeGroupOptions {
    return themeGroupOptions.filter((group) => group.id === this.ownerThemeGroupId);
  }
  themesForGroup(group: ThemeGroupId): ThemeOption[] {
    if (group === 'bar') return this.themes.filter((theme) => theme.group === 'bar' || ['premium-gold', 'burgundy-dining', 'urban-espresso', 'modern-dark'].includes(theme.id));
    return this.themes.filter((theme) => theme.group === group);
  }
  private get ownerThemeGroupId(): ThemeGroupId {
    return this.themeGroupForEstablishmentType(this.ownerRestaurant?.type);
  }
  private themeGroupForEstablishmentType(type?: EstablishmentType | null): ThemeGroupId {
    return type === 'Cafe'
      ? 'cafe'
      : type === 'FastFood'
      ? 'fast-food'
      : ['Bar', 'Club', 'ShishaBar'].includes(type ?? '')
        ? 'bar'
        : 'restaurant';
  }
  syncRestaurantTypeTheme(): void {
    const themeOptions = this.restaurantFormThemeOptions;
    if (!themeOptions.some((option) => option.value === this.restaurantForm.themeKey)) {
      this.restaurantForm.themeKey = String(themeOptions[0]?.value ?? 'classic-light');
    }
  }
  get ownerCategoryOptions(): AppSelectOption[] { return this.ownerCategories.map((category) => ({ value: category.id, label: category.name })); }
  get ownerCategoryFilterOptions(): AppSelectOption[] { return [{ value: 'all', label: 'Sve kategorije' }, ...this.ownerCategoryOptions]; }
  get drinkLibraryCategoryOptions(): AppSelectOption[] { return [{ value: '', label: 'Automatski po kategorijama' }, ...this.ownerCategoryOptions]; }
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
  get ownerLast30DaysViews(): number { return this.ownerRestaurant?.analytics.last30DaysViews ?? 0; }
  get ownerTopItems(): { itemId: string; name: string; views: number }[] { return this.ownerRestaurant?.analytics.topItems ?? []; }
  get canUseMenuAnalytics(): boolean { return ['Pro', 'Premium'].includes(this.normalizePlan(this.ownerRestaurant?.plan ?? 'Start')); }
  get canUsePremiumAnalytics(): boolean { return this.normalizePlan(this.ownerRestaurant?.plan ?? 'Start') === 'Premium'; }
  get canUsePremiumProductDetails(): boolean { return this.canUsePremiumAnalytics; }
  get adminTodoItems(): { label: string; value: number; hint: string; tab: AdminTab }[] {
    const dashboard = this.adminDashboard;
    return [
      { label: 'Novi support zahtjevi', value: dashboard?.newSupportRequests ?? 0, hint: 'Zahtjevi vlasnika koji cekaju odgovor', tab: 'support' },
      { label: 'Novi leadovi', value: dashboard?.newLeads ?? 0, hint: 'Upiti s landing stranice za kontakt', tab: 'dashboard' },
      { label: 'Licence uskoro isticu', value: dashboard?.expiringSoon ?? 0, hint: 'Provjeri naplatu u narednih 14 dana', tab: 'billing' },
      { label: 'Bez proizvoda', value: dashboard?.restaurantsMissingProducts ?? 0, hint: 'Objekti kojima meni nije popunjen', tab: 'restaurants' },
      { label: 'Bez logo/cover slike', value: dashboard?.restaurantsMissingImages ?? 0, hint: 'Objekti kojima fali vizuelni identitet', tab: 'restaurants' },
    ];
  }
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
  get filteredArchivedRestaurants(): AdminRestaurantSummary[] {
    const term = this.archivedRestaurantSearch.trim().toLocaleLowerCase();
    return this.archivedRestaurants.filter((item) => !term ||
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
  get filteredAdminSupportTickets(): SupportTicket[] {
    return this.adminSupportTickets.filter((ticket) => this.supportStatusFilter === 'all' || ticket.status === this.supportStatusFilter);
  }
  get filteredAuditLogs(): AuditLogSummary[] {
    const term = this.auditLogSearch.trim().toLocaleLowerCase();
    return this.auditLogs.filter((item) =>
      !term ||
      item.action.toLocaleLowerCase().includes(term) ||
      item.entityType.toLocaleLowerCase().includes(term) ||
      (item.restaurantName ?? '').toLocaleLowerCase().includes(term) ||
      (item.summary ?? '').toLocaleLowerCase().includes(term) ||
      (item.actorEmail ?? '').toLocaleLowerCase().includes(term) ||
      (item.ipAddress ?? '').toLocaleLowerCase().includes(term));
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

  toggleOwnerThemeMode(): void {
    this.ownerDarkMode = !this.ownerDarkMode;
    globalThis.localStorage?.setItem('menispot-owner-theme', this.ownerDarkMode ? 'dark' : 'light');
    this.updateBrowserChromeColor();
  }

  showToast(message: string, type: ToastType = 'success'): void {
    const toast: ToastMessage = { id: ++this.toastId, type, message };
    this.toasts = [...this.toasts, toast];
    setTimeout(() => this.dismissToast(toast.id), 4200);
  }

  dismissToast(id: number): void {
    this.toasts = this.toasts.filter((toast) => toast.id !== id);
  }

  askConfirm(dialog: ConfirmDialog): void {
    this.confirmDialog = dialog;
  }

  closeConfirm(): void {
    this.confirmDialog = null;
  }

  confirmAction(): void {
    const action = this.confirmDialog?.onConfirm;
    this.confirmDialog = null;
    action?.();
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

    if (this.leadForm.website.trim()) {
      this.leadSuccess = 'Upit je poslan. Javit ćemo vam se uskoro na email.';
      this.leadForm = { businessName: '', email: '', phone: '', type: '', message: '', website: '' };
      return;
    }

    this.leadLoading = true;
    this.leadError = '';
    this.leadSuccess = '';
    const payload = {
      businessName,
      email,
      phone: this.leadForm.phone.trim(),
      type,
      message: this.leadForm.message.trim(),
      website: this.leadForm.website.trim(),
    };

    void fetch(`${API_URL}/leads`, {
      method: 'POST',
      body: JSON.stringify(payload),
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    })
      .then((response) => {
        if (!response.ok) throw new Error('Lead request failed.');
        this.leadSuccess = 'Upit je poslan. Javit ćemo vam se uskoro na email.';
        this.leadForm = { businessName: '', email: '', phone: '', type: '', message: '', website: '' };
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
            : 'Prijava trenutno nije dostupna.';
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
          this.showToast('Nova lozinka je uspješno postavljena.');
        },
        error: (error: HttpErrorResponse) => {
          const errors = error.error?.errors;
          this.passwordError = Array.isArray(errors) && errors.length
            ? errors.join(' ')
            : 'Lozinka nije promijenjena. Provjeri trenutnu lozinku i pravila za novu lozinku.';
          this.showToast('Lozinka nije promijenjena.', 'error');
        },
      });
  }

  loadAdminRestaurants(force = false): void {
    if (this.adminRestaurantsLoading || this.adminRestaurantsLoaded && !force) return;
    this.adminRestaurantsLoading = true;
    this.adminRestaurantsError = '';
    forkJoin({
      restaurants: this.adminRestaurantsService.getAll(),
      dashboard: this.adminRestaurantsService.getDashboard(),
      readiness: this.adminRestaurantsService.getReadiness(),
    })
      .pipe(finalize(() => this.adminRestaurantsLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ restaurants, dashboard, readiness }) => {
          this.adminRestaurants = restaurants;
          this.adminDashboard = dashboard;
          this.adminReadiness = Object.fromEntries(readiness.map((item) => [item.restaurantId, item]));
          this.adminRestaurantsLoaded = true;
          void this.generateAdminQrCodes(restaurants);
        },
        error: () => this.adminRestaurantsError = 'Restorani se trenutno ne mogu učitati. Provjeri backend i pokušaj ponovo.',
      });
  }

  loadArchivedRestaurants(force = false): void {
    if (this.archivedRestaurantsLoading || this.archivedRestaurantsLoaded && !force) return;
    this.archivedRestaurantsLoading = true;
    this.archivedRestaurantsError = '';
    this.adminRestaurantsService.getArchived()
      .pipe(finalize(() => this.archivedRestaurantsLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.archivedRestaurants = items;
          this.archivedRestaurantsLoaded = true;
        },
        error: () => this.archivedRestaurantsError = 'Arhiva restorana se trenutno ne moze ucitati. Pokusaj ponovo.',
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

  loadAdminSupport(force = false): void {
    if (this.adminSupportLoading || this.adminSupportLoaded && !force) return;
    this.adminSupportLoading = true;
    this.adminSupportError = '';
    this.adminRestaurantsService.getSupportTickets()
      .pipe(finalize(() => this.adminSupportLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.adminSupportTickets = items;
          this.adminSupportLoaded = true;
        },
        error: () => this.adminSupportError = 'Zahtjevi za podrsku se trenutno ne mogu ucitati.',
      });
  }

  loadAuditLogs(force = false): void {
    if (this.auditLogsLoading || this.auditLogsLoaded && !force) return;
    this.auditLogsLoading = true;
    this.auditLogsError = '';
    this.adminRestaurantsService.getAuditLogs(250)
      .pipe(finalize(() => this.auditLogsLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.auditLogs = items;
          this.auditLogsLoaded = true;
        },
        error: () => this.auditLogsError = 'Logovi se trenutno ne mogu ucitati.',
      });
  }

  loadOwnerSupport(force = false): void {
    if (this.ownerSupportLoading || this.ownerSupportLoaded && !force) return;
    this.ownerSupportLoading = true;
    this.ownerSupportError = '';
    this.ownerService.getSupportTickets()
      .pipe(finalize(() => this.ownerSupportLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.ownerSupportTickets = items;
          this.ownerSupportLoaded = true;
        },
        error: () => this.ownerSupportError = 'Historija podrske se trenutno ne moze ucitati.',
      });
  }

  updateSupportTicket(ticket: SupportTicket, status: SupportTicketStatus, adminNote = ticket.adminNote ?? ''): void {
    if (this.supportUpdating.has(ticket.id)) return;
    this.supportUpdating.add(ticket.id);
    this.adminRestaurantsService.updateSupportTicket(ticket.id, { status, adminNote: this.nullIfEmpty(adminNote) })
      .pipe(finalize(() => this.supportUpdating.delete(ticket.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.adminSupportTickets = this.adminSupportTickets.map((item) => item.id === updated.id ? updated : item);
          this.showToast('Zahtjev je azuriran.');
        },
        error: () => {
          this.adminSupportError = 'Zahtjev nije azuriran. Pokusaj ponovo.';
          this.showToast('Zahtjev nije azuriran.', 'error');
        },
      });
  }

  deleteSupportTicket(ticket: SupportTicket): void {
    if (this.supportUpdating.has(ticket.id)) return;
    const confirmed = window.confirm(`Obrisati zahtjev "${ticket.title}"?`);
    if (!confirmed) return;

    this.supportUpdating.add(ticket.id);
    this.adminRestaurantsService.deleteSupportTicket(ticket.id)
      .pipe(finalize(() => this.supportUpdating.delete(ticket.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.adminSupportTickets = this.adminSupportTickets.filter((item) => item.id !== ticket.id);
          this.showToast('Zahtjev je obrisan.');
        },
        error: () => {
          this.adminSupportError = 'Zahtjev nije obrisan. Pokusaj ponovo.';
          this.showToast('Zahtjev nije obrisan.', 'error');
        },
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
        this.showToast(form.id ? 'Piće je sačuvano.' : 'Novo piće je dodano.');
      },
      error: (error: HttpErrorResponse) => {
        this.adminDrinkFormError = error.error?.title ?? 'Piće nije sačuvano. Provjeri unesene podatke.';
        this.showToast('Piće nije sačuvano.', 'error');
      },
    });
  }

  toggleAdminDrink(item: AdminGlobalDrink): void {
    this.adminDrinksService.update(item.id, this.adminDrinkRequest(item, { isActive: !item.isActive })).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => {
        this.adminDrinks = this.adminDrinks.map((drink) => drink.id === updated.id ? updated : drink);
        this.drinkLibrary = [];
        this.showToast(updated.isActive ? 'Piće je aktivno u biblioteci.' : 'Piće je sakriveno iz biblioteke.');
      },
      error: () => {
        this.adminDrinksError = 'Status pića nije promijenjen.';
        this.showToast('Status pića nije promijenjen.', 'error');
      },
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
          this.showToast('Izmjena je sačuvana.');
        },
        error: () => {
          this.adminDrinksError = 'Brza izmjena pića nije sačuvana.';
          this.showToast('Brza izmjena nije sačuvana.', 'error');
        },
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
  clearProductImage(): void { this.clearPendingImage('product'); this.productForm.imageUrl = ''; }
  clearOfferImage(): void { this.clearPendingImage('offer'); this.offerForm.imageUrl = ''; }

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
    this.askConfirm({
      title: 'Obrisati piće?',
      message: `Piće "${item.name}" će biti trajno obrisano iz globalne biblioteke. Pića koja su vlasnici već dodali ostaju u njihovim menijima.`,
      confirmText: 'Obriši piće',
      tone: 'danger',
      onConfirm: () => this.adminDrinksService.delete(item.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.adminDrinks = this.adminDrinks.filter((drink) => drink.id !== item.id);
          this.drinkLibrary = [];
          this.showToast('Piće je obrisano.');
        },
        error: () => {
          this.adminDrinksError = 'Piće nije obrisano.';
          this.showToast('Piće nije obrisano.', 'error');
        },
      }),
    });
  }

  selectAdminDrinkImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const validationError = this.validateImageFile(file!);
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
        next: ({ url }) => {
          this.adminDrinkForm.imageUrl = url;
          this.showToast('Fotografija je učitana.');
        },
        error: (error: HttpErrorResponse) => {
          this.adminDrinkFormError = error.error?.title ?? 'Fotografija nije učitana. Maksimalna veličina je 5 MB.';
          this.showToast('Fotografija nije učitana.', 'error');
        },
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
        this.showToast('Uplata je evidentirana.');
      },
      error: (error: HttpErrorResponse) => {
        this.paymentError = error.error?.title ?? 'Uplata nije evidentirana.';
        this.showToast('Uplata nije evidentirana.', 'error');
      },
    });
  }

  openCreateRestaurant(): void {
    void this.router.navigate(['/admin', 'restaurants', 'new']);
  }

  private prepareCreateRestaurant(): void {
    if (this.restaurantEditorMode === 'create') return;
    this.restaurantForm = this.emptyRestaurantForm();
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    this.restaurantModalLoading = false;
    this.restaurantEditorMode = 'create';
    this.showRestaurantModal = false;
  }

  openEditRestaurant(item: AdminRestaurantSummary): void {
    void this.router.navigate(['/admin', 'restaurants', item.id, 'edit']);
  }

  private prepareEditRestaurant(restaurantId: string): void {
    if (this.restaurantEditorMode === 'edit' && this.restaurantForm.id === restaurantId && !this.restaurantModalLoading) return;
    const summary = this.adminRestaurants.find((item) => item.id === restaurantId);
    this.restaurantForm = { ...this.emptyRestaurantForm(), id: restaurantId, name: summary?.name ?? '', slug: summary?.slug ?? '', type: summary?.type ?? 'Restaurant' };
    this.showRestaurantModal = false;
    this.restaurantEditorMode = 'edit';
    this.restaurantModalLoading = true;
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    this.adminRestaurantsService.get(restaurantId)
      .pipe(finalize(() => this.restaurantModalLoading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (details) => this.restaurantForm = this.formFromDetails(details),
        error: () => this.restaurantFormError = 'Podaci restorana se ne mogu učitati.',
      });
  }

  closeRestaurantModal(): void {
    if (this.restaurantSaving || this.restaurantImageUploading) return;
    this.restaurantEditorMode = 'none';
    this.showRestaurantModal = false;
    this.restaurantFormError = '';
    this.restaurantFormSuccess = '';
    void this.router.navigate(['/admin', 'restaurants']);
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
            currency: form.currency.trim() || 'BAM', defaultLanguage: form.defaultLanguage.trim() || 'bs', enabledLanguages: form.enabledLanguages, type: form.type,
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
            plan: this.normalizePlan(form.plan), monthlyPrice: form.monthlyPrice, startsOn: form.startsOn,
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
          currency: form.currency.trim() || 'BAM', defaultLanguage: form.defaultLanguage.trim() || 'bs', enabledLanguages: form.enabledLanguages,
          plan: this.normalizePlan(form.plan), monthlyPrice: form.monthlyPrice,
          themeKey: form.themeKey,
        });

    request.pipe(finalize(() => this.restaurantSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (passwordChanged) {
          this.restaurantForm.ownerPassword = '';
          this.restaurantFormSuccess = 'Nova šifra je uspješno postavljena.';
          this.showToast('Nova šifra je uspješno postavljena.');
        } else {
          this.showRestaurantModal = false;
          this.restaurantEditorMode = 'none';
          void this.router.navigate(['/admin', 'restaurants']);
          this.showToast(form.id ? 'Restoran je sačuvan.' : 'Restoran je kreiran.');
        }
        this.loadAdminRestaurants(true);
      },
      error: (error: HttpErrorResponse) => {
        this.restaurantFormError = error.error?.title ?? 'Promjene nisu sačuvane. Provjeri unesene podatke.';
        this.showToast('Promjene nisu sačuvane.', 'error');
      },
    });
  }

  selectAdminRestaurantImage(event: Event, target: 'logo' | 'cover'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploadAdminRestaurantImage(file, target, () => input.value = '');
  }

  dropAdminRestaurantImage(event: DragEvent, target: 'logo' | 'cover'): void {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0];
    if (file) this.uploadAdminRestaurantImage(file, target);
  }

  allowAdminRestaurantImageDrop(event: DragEvent): void {
    event.preventDefault();
  }

  private uploadAdminRestaurantImage(file: File, target: 'logo' | 'cover', cleanup?: () => void): void {
    if (!this.restaurantForm.id) {
      this.restaurantFormError = 'Prvo kreiraj restoran, pa dodaj logo ili naslovnu fotografiju.';
      cleanup?.();
      return;
    }
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.restaurantFormError = validationError;
      cleanup?.();
      return;
    }

    this.restaurantImageUploading = target;
    this.restaurantFormError = '';
    this.adminRestaurantsService.uploadImage(this.restaurantForm.id, file)
      .pipe(finalize(() => { this.restaurantImageUploading = null; cleanup?.(); }), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ url }) => {
          if (target === 'logo') this.restaurantForm.logoUrl = url;
          else this.restaurantForm.coverImageUrl = url;
          this.restaurantFormSuccess = 'Fotografija je učitana. Sačuvaj promjene da ostane vezana za restoran.';
          this.showToast('Fotografija je učitana.');
        },
        error: (error: HttpErrorResponse) => {
          this.restaurantFormError = error.error?.title ?? 'Fotografija nije učitana. Maksimalna veličina je 5 MB.';
          this.showToast('Fotografija nije učitana.', 'error');
        },
      });
  }

  clearAdminRestaurantImage(target: 'logo' | 'cover'): void {
    if (target === 'logo') this.restaurantForm.logoUrl = '';
    else this.restaurantForm.coverImageUrl = '';
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
          this.showToast(status === 'Active' ? 'Restoran je aktiviran.' : 'Restoran je pauziran.');
        },
        error: () => {
          this.adminRestaurantsError = 'Status restorana nije promijenjen. Pokušaj ponovo.';
          this.showToast('Status restorana nije promijenjen.', 'error');
        },
      });
  }

  deleteAdminRestaurant(item: AdminRestaurantSummary): void {
    this.askConfirm({
      title: 'Arhivirati restoran?',
      message: `Restoran "${item.name}" ce biti sklonjen iz aktivnih lista, ali ga mozes vratiti iz arhive.`,
      confirmText: 'Arhiviraj restoran',
      tone: 'danger',
      onConfirm: () => this.adminRestaurantsService.delete(item.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.adminRestaurants = this.adminRestaurants.filter((restaurant) => restaurant.id !== item.id);
          this.archivedRestaurantsLoaded = false;
          this.adminDashboard = null;
          this.adminRestaurantsLoaded = false;
          this.loadAdminRestaurants(true);
          this.showToast('Restoran je obrisan.');
        },
        error: () => {
          this.adminRestaurantsError = 'Restoran nije obrisan. Pokušaj ponovo.';
          this.showToast('Restoran nije obrisan.', 'error');
        },
      }),
    });
  }

  restoreAdminRestaurant(item: AdminRestaurantSummary): void {
    if (this.restaurantStatusUpdating.has(item.id)) return;
    this.restaurantStatusUpdating.add(item.id);
    this.adminRestaurantsService.restore(item.id)
      .pipe(finalize(() => this.restaurantStatusUpdating.delete(item.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.archivedRestaurants = this.archivedRestaurants.filter((restaurant) => restaurant.id !== item.id);
          this.adminRestaurantsLoaded = false;
          this.adminDashboard = null;
          this.loadArchivedRestaurants(true);
          this.loadAdminRestaurants(true);
          this.showToast('Restoran je vracen iz arhive.');
        },
        error: () => {
          this.archivedRestaurantsError = 'Restoran nije vracen iz arhive. Pokusaj ponovo.';
          this.showToast('Restoran nije vracen iz arhive.', 'error');
        },
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
        error: () => {
          this.adminRestaurantsError = 'Nije moguće otvoriti vlasnički panel za ovaj restoran.';
          this.showToast('Vlasnički panel nije moguće otvoriti.', 'error');
        },
      });
  }

  returnToAdmin(): void {
    this.ownerRestaurant = null;
    this.ownerViewRestaurant = null;
    this.auth.stopImpersonation();
  }

  selectAdminTab(tab: AdminTab): void { void this.router.navigate(['/admin', tab]); }
  selectOwnerTab(tab: OwnerTab): void { void this.router.navigate(['/restaurant', this.restaurant.id, tab]); }

  selectSupportAttachment(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.ownerSupportError = validationError;
      input.value = '';
      return;
    }
    this.ownerSupportUploading = true;
    this.ownerSupportError = '';
    this.ownerService.uploadSupportImage(file)
      .pipe(finalize(() => this.ownerSupportUploading = false), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ url }) => {
          this.supportForm.attachmentUrl = url;
          this.showToast('Screenshot je dodan.');
        },
        error: () => {
          this.ownerSupportError = 'Screenshot nije uploadovan. Pokusaj ponovo.';
          this.showToast('Screenshot nije uploadovan.', 'error');
        },
      });
  }

  submitSupportTicket(): void {
    if (!this.canUseOwnerSupport) {
      this.ownerSupportError = 'Podrska kroz panel je dostupna u Pro i Premium paketu.';
      return;
    }
    if (!this.supportForm.title.trim() || !this.supportForm.message.trim()) {
      this.ownerSupportError = 'Naslov i opis zahtjeva su obavezni.';
      return;
    }
    this.ownerSupportSaving = true;
    this.ownerSupportError = '';
    this.ownerSupportSuccess = '';
    this.ownerService.createSupportTicket({
      title: this.supportForm.title.trim(),
      type: this.supportForm.type,
      priority: this.supportForm.priority,
      message: this.supportForm.message.trim(),
      attachmentUrl: this.nullIfEmpty(this.supportForm.attachmentUrl),
    }).pipe(finalize(() => this.ownerSupportSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (ticket) => {
        this.ownerSupportTickets = [ticket, ...this.ownerSupportTickets];
        this.ownerSupportLoaded = true;
        this.supportForm = this.emptySupportForm();
        this.ownerSupportSuccess = 'Zahtjev je poslan. Dobit ces odgovor cim bude pregledan.';
        this.showToast('Zahtjev je poslan.');
      },
      error: (error: HttpErrorResponse) => {
        this.ownerSupportError = typeof error.error === 'string' ? error.error : 'Zahtjev nije poslan. Pokusaj ponovo.';
        this.showToast('Zahtjev nije poslan.', 'error');
      },
    });
  }

  openCategory(category?: OwnerMenuCategory): void {
    this.categoryForm = category
      ? { id: category.id, name: category.name, description: category.description ?? '', nameEn: category.nameEn ?? '', descriptionEn: category.descriptionEn ?? '', nameDe: category.nameDe ?? '', descriptionDe: category.descriptionDe ?? '', type: category.type, sortOrder: category.sortOrder, isVisible: category.isVisible }
      : this.emptyCategoryForm();
    this.showCategoryModal = true;
  }

  saveCategory(): void {
    if (!this.categoryForm.name.trim()) { this.ownerError = 'Naziv kategorije je obavezan.'; return; }
    this.ownerSaving = true;
    this.ownerService.saveCategory(this.categoryForm.id, {
      name: this.categoryForm.name.trim(), description: this.nullIfEmpty(this.categoryForm.description),
      nameEn: this.nullIfEmpty(this.categoryForm.nameEn), descriptionEn: this.nullIfEmpty(this.categoryForm.descriptionEn),
      nameDe: this.nullIfEmpty(this.categoryForm.nameDe), descriptionDe: this.nullIfEmpty(this.categoryForm.descriptionDe),
      type: this.categoryForm.type, sortOrder: this.categoryForm.sortOrder, isVisible: this.categoryForm.isVisible,
    }).pipe(finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showCategoryModal = false;
        this.loadOwnerRestaurant(true);
        this.showToast(this.categoryForm.id ? 'Kategorija je sačuvana.' : 'Kategorija je dodana.');
      },
      error: () => {
        this.ownerError = 'Kategorija nije sačuvana.';
        this.showToast('Kategorija nije sačuvana.', 'error');
      },
    });
  }

  toggleCategory(category: OwnerMenuCategory): void {
    this.ownerService.saveCategory(category.id, { name: category.name, description: category.description, nameEn: category.nameEn, descriptionEn: category.descriptionEn, nameDe: category.nameDe, descriptionDe: category.descriptionDe, type: category.type, sortOrder: category.sortOrder, isVisible: !category.isVisible })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast(category.isVisible ? 'Kategorija je sakrivena.' : 'Kategorija je prikazana.');
        },
        error: () => {
          this.ownerError = 'Vidljivost kategorije nije promijenjena.';
          this.showToast('Vidljivost kategorije nije promijenjena.', 'error');
        },
      });
  }

  deleteCategory(category: OwnerMenuCategory): void {
    this.askConfirm({
      title: 'Obrisati kategoriju?',
      message: `Kategorija "${category.name}" i svi proizvodi u njoj će biti obrisani iz menija.`,
      confirmText: 'Obriši kategoriju',
      tone: 'danger',
      onConfirm: () => this.ownerService.deleteCategory(category.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast('Kategorija je obrisana.');
        },
        error: () => {
          this.ownerError = 'Kategorija nije obrisana.';
          this.showToast('Kategorija nije obrisana.', 'error');
        },
      }),
    });
  }

  openProduct(product?: OwnerMenuItem): void {
    this.productForm = product ? {
      id: product.id, categoryId: product.categoryId, globalDrinkId: product.globalDrinkId, name: product.name, description: product.description ?? '', price: product.price, servingSize: product.servingSize ?? '',
      nameEn: product.nameEn ?? '', descriptionEn: product.descriptionEn ?? '', nameDe: product.nameDe ?? '', descriptionDe: product.descriptionDe ?? '',
      imageUrl: product.globalDrinkId ? '' : product.imageUrl ?? '', allergens: product.allergens ?? '', sortOrder: product.sortOrder, isVisible: product.isVisible,
      ingredients: product.ingredients ?? '', calories: product.calories, protein: product.protein, carbs: product.carbs, fat: product.fat, sugar: product.sugar, salt: product.salt,
      isAvailable: product.isAvailable, isVegetarian: product.isVegetarian, isSpicy: product.isSpicy, isFeatured: product.isFeatured,
    } : this.emptyProductForm();
    this.showProductModal = true;
  }

  saveProduct(): void {
    if (!this.productForm.name.trim() || !this.productForm.categoryId) { this.ownerError = 'Naziv i kategorija proizvoda su obavezni.'; return; }
    this.ownerSaving = true;
    this.uploadPendingImage('product').pipe(concatMap((url) => this.ownerService.saveItem(this.productForm.id, {
      categoryId: this.productForm.categoryId, name: this.productForm.name.trim(), description: this.nullIfEmpty(this.productForm.description),
      nameEn: this.nullIfEmpty(this.productForm.nameEn), descriptionEn: this.nullIfEmpty(this.productForm.descriptionEn),
      nameDe: this.nullIfEmpty(this.productForm.nameDe), descriptionDe: this.nullIfEmpty(this.productForm.descriptionDe),
      price: this.productForm.price, servingSize: this.nullIfEmpty(this.productForm.servingSize), imageUrl: this.nullIfEmpty(url ?? this.productForm.imageUrl), allergens: this.nullIfEmpty(this.productForm.allergens),
      ingredients: this.canUsePremiumProductDetails ? this.nullIfEmpty(this.productForm.ingredients) : null,
      calories: this.canUsePremiumProductDetails ? this.productForm.calories : null,
      protein: this.canUsePremiumProductDetails ? this.productForm.protein : null,
      carbs: this.canUsePremiumProductDetails ? this.productForm.carbs : null,
      fat: this.canUsePremiumProductDetails ? this.productForm.fat : null,
      sugar: this.canUsePremiumProductDetails ? this.productForm.sugar : null,
      salt: this.canUsePremiumProductDetails ? this.productForm.salt : null,
      sortOrder: this.productForm.sortOrder, isVisible: this.productForm.isVisible, isAvailable: this.productForm.isAvailable,
      isVegetarian: this.productForm.isVegetarian, isSpicy: this.productForm.isSpicy, isFeatured: this.productForm.isFeatured,
    })), finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showProductModal = false;
        this.loadOwnerRestaurant(true);
        this.showToast(this.productForm.id ? 'Proizvod je sačuvan.' : 'Proizvod je dodan.');
      },
      error: () => {
        this.ownerError = 'Proizvod nije sačuvan.';
        this.showToast('Proizvod nije sačuvan.', 'error');
      },
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
          this.showToast(`Dodano ${drinks.length} pića u meni.`);
        },
        error: () => {
          this.drinkLibraryError = 'Pića nisu dodana u meni.';
          this.showToast('Pića nisu dodana u meni.', 'error');
        },
      });
  }

  toggleProduct(product: Product): void {
    const source = this.ownerRestaurant?.categories.flatMap((category) => category.items).find((item) => item.id === product.id);
    if (!source) { product.available = !product.available; return; }
    this.ownerService.saveItem(source.id, {
      categoryId: source.categoryId, name: source.name, description: source.description,
      nameEn: source.nameEn, descriptionEn: source.descriptionEn, nameDe: source.nameDe, descriptionDe: source.descriptionDe,
      price: source.price,
      servingSize: source.servingSize, imageUrl: source.globalDrinkId ? null : source.imageUrl, allergens: source.allergens,
      ingredients: source.ingredients, calories: source.calories, protein: source.protein, carbs: source.carbs, fat: source.fat, sugar: source.sugar, salt: source.salt,
      sortOrder: source.sortOrder,
      isVisible: source.isVisible, isAvailable: !source.isAvailable, isVegetarian: source.isVegetarian, isSpicy: source.isSpicy, isFeatured: source.isFeatured,
    })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast(source.isAvailable ? 'Proizvod je sakriven.' : 'Proizvod je dostupan gostima.');
        },
        error: () => {
          this.ownerError = 'Dostupnost proizvoda nije promijenjena.';
          this.showToast('Dostupnost proizvoda nije promijenjena.', 'error');
        },
      });
  }

  deleteProduct(product: Product): void {
    this.askConfirm({
      title: 'Obrisati proizvod?',
      message: `Proizvod "${product.name}" će biti trajno uklonjen iz menija.`,
      confirmText: 'Obriši proizvod',
      tone: 'danger',
      onConfirm: () => this.ownerService.deleteItem(product.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast('Proizvod je obrisan.');
        },
        error: () => {
          this.ownerError = 'Proizvod nije obrisan.';
          this.showToast('Proizvod nije obrisan.', 'error');
        },
      }),
    });
  }

  openOffer(kind: SpecialOfferKind, offer?: OwnerSpecialOffer): void {
    this.offerForm = offer ? {
      id: offer.id, kind: offer.kind, title: offer.title, description: offer.description ?? '', price: offer.price ?? 0,
      titleEn: offer.titleEn ?? '', descriptionEn: offer.descriptionEn ?? '', itemsEn: offer.itemsEn ?? '',
      titleDe: offer.titleDe ?? '', descriptionDe: offer.descriptionDe ?? '', itemsDe: offer.itemsDe ?? '',
      originalPrice: offer.originalPrice ?? 0, imageUrl: offer.imageUrl ?? '', startsAt: this.dateTimeInputValue(offer.startsAt),
      endsAt: this.dateTimeInputValue(offer.endsAt), isVisible: offer.isVisible, items: offer.items ?? '',
    } : this.emptyOfferForm(kind);
    this.showOfferModal = true;
  }

  saveOffer(): void {
    if (!this.offerForm.title.trim()) { this.ownerError = 'Naziv ponude je obavezan.'; return; }
    this.ownerSaving = true;
    this.uploadPendingImage('offer').pipe(concatMap((url) => this.ownerService.saveOffer(this.offerForm.id, {
      title: this.offerForm.title.trim(), description: this.nullIfEmpty(this.offerForm.description),
      titleEn: this.nullIfEmpty(this.offerForm.titleEn), descriptionEn: this.nullIfEmpty(this.offerForm.descriptionEn), itemsEn: this.nullIfEmpty(this.offerForm.itemsEn),
      titleDe: this.nullIfEmpty(this.offerForm.titleDe), descriptionDe: this.nullIfEmpty(this.offerForm.descriptionDe), itemsDe: this.nullIfEmpty(this.offerForm.itemsDe),
      price: this.offerForm.price || null,
      originalPrice: this.offerForm.originalPrice || null, imageUrl: this.nullIfEmpty(url ?? this.offerForm.imageUrl),
      startsAt: this.isoDateTime(this.offerForm.startsAt), endsAt: this.isoDateTime(this.offerForm.endsAt), isVisible: this.offerForm.isVisible,
      kind: this.offerForm.kind, items: this.nullIfEmpty(this.offerForm.items),
    })), finalize(() => this.ownerSaving = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showOfferModal = false;
        this.loadOwnerRestaurant(true);
        this.showToast(this.offerForm.kind === 'DailyMenu' ? 'Dnevni meni je sačuvan.' : 'Ponuda je sačuvana.');
      },
      error: () => {
        this.ownerError = 'Ponuda nije sačuvana.';
        this.showToast('Ponuda nije sačuvana.', 'error');
      },
    });
  }

  toggleOffer(offer: OwnerSpecialOffer): void {
    this.ownerService.saveOffer(offer.id, this.offerRequestFromOffer(offer, !offer.isVisible))
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast(offer.isVisible ? 'Ponuda je sakrivena.' : 'Ponuda je prikazana.');
        },
        error: () => {
          this.ownerError = 'Status ponude nije promijenjen.';
          this.showToast('Status ponude nije promijenjen.', 'error');
        },
      });
  }

  deleteOffer(offer: OwnerSpecialOffer): void {
    this.askConfirm({
      title: 'Obrisati ponudu?',
      message: `Ponuda "${offer.title}" će biti trajno uklonjena iz menija.`,
      confirmText: 'Obriši ponudu',
      tone: 'danger',
      onConfirm: () => this.ownerService.deleteOffer(offer.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast('Ponuda je obrisana.');
        },
        error: () => {
          this.ownerError = 'Ponuda nije obrisana.';
          this.showToast('Ponuda nije obrisana.', 'error');
        },
      }),
    });
  }

  changeTheme(theme: ThemeType): void {
    if (!this.ownerRestaurant) { this.restaurant.theme = theme; return; }
    const selected = this.themes.find((item) => item.id === theme)!;
    const request = { ...this.ownerRestaurant.theme, themeKey: theme, primaryColor: selected.colors[1], accentColor: selected.colors[2] };
    this.ownerService.setTheme(request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadOwnerRestaurant(true);
        this.showToast('Tema je sačuvana.');
      },
      error: () => {
        this.ownerError = 'Tema nije sačuvana.';
        this.showToast('Tema nije sačuvana.', 'error');
      },
    });
  }

  saveOwnerSettings(): void {
    if (!this.ownerRestaurant) return;
    this.ownerSaving = true;
    forkJoin({ logoUrl: this.uploadPendingImage('logo'), coverImageUrl: this.uploadPendingImage('cover') }).pipe(
      concatMap(({ logoUrl, coverImageUrl }) => this.ownerService.updateRestaurant({
        name: this.restaurant.name, description: this.ownerRestaurant!.description, logoUrl: (logoUrl ?? this.restaurant.logo) || null,
        coverImageUrl: (coverImageUrl ?? this.restaurant.cover) || null, address: this.restaurant.address || null, phone: this.restaurant.phone || null,
        email: this.ownerRestaurant!.email, websiteUrl: this.restaurant.website || null, instagramUrl: this.restaurant.instagram || null,
        currency: this.ownerRestaurant!.currency, defaultLanguage: this.ownerRestaurant!.defaultLanguage, enabledLanguages: this.ownerRestaurant!.enabledLanguages, type: this.ownerRestaurant!.type,
        themeKey: this.restaurant.theme,
      })),
      finalize(() => this.ownerSaving = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.loadOwnerRestaurant(true);
        this.showToast('Postavke su sačuvane.');
      },
      error: () => {
        this.ownerError = 'Postavke nisu sačuvane.';
        this.showToast('Postavke nisu sačuvane.', 'error');
      },
    });
  }

  saveBusinessHours(): void {
    if (!this.ownerRestaurant) return;
    this.ownerService.setBusinessHours(this.ownerRestaurant.businessHours)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.loadOwnerRestaurant(true);
          this.showToast('Radno vrijeme je sačuvano.');
        },
        error: () => {
          this.ownerError = 'Radno vrijeme nije sačuvano.';
          this.showToast('Radno vrijeme nije sačuvano.', 'error');
        },
      });
  }

  downloadOwnerQr(): void {
    if (!this.ownerQrCode || !this.ownerRestaurant) return;
    const link = document.createElement('a'); link.href = this.ownerQrCode; link.download = `${this.ownerRestaurant.slug}-qr.png`; link.click();
  }

  printOwnerQr(size: 'A5' | 'A6'): void {
    if (!this.ownerQrCode || !this.ownerRestaurant) return;
    this.printQrDocument({
      qr: this.ownerQrCode,
      size,
      name: this.ownerRestaurant.name,
      logo: this.ownerRestaurant.logoUrl || '/menispot-mark.png',
      url: `${globalThis.location.origin}/menu/${this.ownerRestaurant.slug}?source=qr`,
    });
  }

  selectImage(event: Event, target: UploadTarget): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.stageImage(file, target);
    input.value = '';
  }

  dropImage(event: DragEvent, target: UploadTarget): void {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0];
    if (file) this.stageImage(file, target);
  }

  allowImageDrop(event: DragEvent): void {
    event.preventDefault();
  }

  private stageImage(file: File, target: UploadTarget): void {
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.ownerError = validationError;
      return;
    }
    const previous = this.imagePreviews[target];
    if (previous?.startsWith('blob:')) URL.revokeObjectURL(previous);
    const preview = URL.createObjectURL(file);
    this.pendingUploads[target] = file;
    this.imagePreviews[target] = preview;
    if (target === 'product') this.productForm.imageUrl = preview;
    else if (target === 'offer') this.offerForm.imageUrl = preview;
    else if (target === 'logo') this.restaurant.logo = preview;
    else this.restaurant.cover = preview;
    this.showToast('Pregled fotografije je spreman. Sacuvaj da ostane u meniju.', 'info');
  }

  private uploadPendingImage(target: UploadTarget): Observable<string | null> {
    const file = this.pendingUploads[target];
    if (!file) return of(null);
    return this.ownerService.uploadImage(file).pipe(concatMap(({ url }) => {
      delete this.pendingUploads[target];
      if (this.imagePreviews[target]?.startsWith('blob:')) URL.revokeObjectURL(this.imagePreviews[target]!);
      delete this.imagePreviews[target];
      return of(url);
    }));
  }

  private clearPendingImage(target: UploadTarget): void {
    if (this.imagePreviews[target]?.startsWith('blob:')) URL.revokeObjectURL(this.imagePreviews[target]!);
    delete this.imagePreviews[target];
    delete this.pendingUploads[target];
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
      (!term || this.localizedProductName(product).toLocaleLowerCase().includes(term) || this.localizedProductDescription(product).toLocaleLowerCase().includes(term)),
    );
  }

  nutritionFacts(product: Product): { label: string; value: string }[] {
    const facts = [
      product.calories != null ? { label: 'kcal', value: `${product.calories}` } : null,
      product.protein != null ? { label: this.publicText('protein'), value: `${product.protein}g` } : null,
      product.carbs != null ? { label: this.publicText('carbs'), value: `${product.carbs}g` } : null,
      product.fat != null ? { label: this.publicText('fat'), value: `${product.fat}g` } : null,
      product.sugar != null ? { label: this.publicText('sugar'), value: `${product.sugar}g` } : null,
      product.salt != null ? { label: this.publicText('salt'), value: `${product.salt}g` } : null,
    ];
    return facts.filter(Boolean) as { label: string; value: string }[];
  }

  selectPublicMenuSection(section: 'food' | 'drink'): void {
    this.publicMenuSection = section;
    this.selectedCategory = 'all';
  }

  selectPublicCategory(categoryId: string): void {
    this.selectedCategory = categoryId;
  }

  openPublicProduct(product: Product): void {
    if (!this.canUsePremiumProductDetails || !product.available) return;
    this.publicSelectedProduct = product;
    this.setPublicProductModalOpen(true);
    this.trackPublicProductView(product.id);
  }

  closePublicProduct(): void {
    this.publicSelectedProduct = null;
    this.setPublicProductModalOpen(false);
  }
  setMenuLanguage(language: MenuLanguage): void {
    if (!this.menuLanguageOptions.some((option) => option.value === language)) {
      this.menuLanguage = 'bs';
      return;
    }
    this.menuLanguage = language;
    if (this.ownerRestaurant) this.applyOwnerRestaurant(this.ownerRestaurant, true);
  }

  parseEnabledLanguages(value: string | null | undefined): MenuLanguage[] {
    const allowed: MenuLanguage[] = ['bs', 'en', 'de'];
    const selected = new Set((value ?? 'bs,en').split(',').map((item) => item.trim().toLowerCase()).filter(Boolean));
    selected.add('bs');
    selected.add('en');
    return allowed.filter((language) => selected.has(language));
  }

  isRestaurantLanguageEnabled(language: MenuLanguage): boolean {
    return this.parseEnabledLanguages(this.restaurantForm.enabledLanguages).includes(language);
  }

  toggleRestaurantLanguage(language: MenuLanguage, enabled: boolean): void {
    if (language === 'bs' || language === 'en') return;
    const selected = new Set(this.parseEnabledLanguages(this.restaurantForm.enabledLanguages));
    if (enabled) selected.add(language);
    else selected.delete(language);
    selected.add('bs');
    selected.add('en');
    this.restaurantForm.enabledLanguages = (['bs', 'en', 'de'] as MenuLanguage[]).filter((item) => selected.has(item)).join(',');
  }
  localizedCategoryName(category: Category): string {
    const source = this.ownerRestaurant?.categories.find((item) => item.id === category.id);
    if (!source) return category.name;
    return this.localizedValue(source.name, source.nameEn, source.nameDe);
  }
  localizedProductName(product: Product): string {
    const source = this.ownerItems.find((item) => item.id === product.id);
    if (!source) return product.name;
    return this.localizedValue(source.name, source.nameEn, source.nameDe);
  }
  localizedProductDescription(product: Product): string {
    const source = this.ownerItems.find((item) => item.id === product.id);
    if (!source) return product.description;
    return this.localizedValue(source.description ?? '', source.descriptionEn, source.descriptionDe);
  }
  localizedOfferName(offer: DailyMenu | Offer): string {
    const source = this.ownerRestaurant?.offers.find((item) => item.id === offer.id);
    if (!source) return offer.name;
    return this.localizedValue(source.title, source.titleEn, source.titleDe);
  }
  localizedOfferDescription(offer: Offer): string {
    const source = this.ownerRestaurant?.offers.find((item) => item.id === offer.id);
    if (!source) return offer.description;
    return this.localizedValue(source.description ?? '', source.descriptionEn, source.descriptionDe);
  }
  localizedDailyItems(menu: DailyMenu): string[] {
    const source = this.ownerRestaurant?.offers.find((item) => item.id === menu.id);
    const text = this.localizedValue(source?.items ?? menu.items.join('\n'), source?.itemsEn, source?.itemsDe);
    return text.split('\n').map((item) => item.trim()).filter(Boolean);
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
    const english: Record<BadgeType, string> = { new: 'New', popular: 'Popular', spicy: 'Spicy', vegetarian: 'Veg', 'chefs-choice': 'Chef recommends' };
    const bosnian: Record<BadgeType, string> = { new: 'Novo', popular: 'Popularno', spicy: 'Ljuto', vegetarian: 'Veg', 'chefs-choice': 'Chef preporučuje' };
    return (this.menuLanguage === 'en' ? english : bosnian)[badge];
  }

  publicText(key: string): string {
    const english: Record<string, string> = {
      all: 'All',
      allergens: 'Allergens',
      callUs: 'Call us',
      carbs: 'carbs',
      closed: 'Closed',
      dailyOffer: 'Daily offer',
      detailsFallback: 'Product details are available from the staff.',
      drinks: 'Drinks',
      emptyBody: 'Change the search or choose another category.',
      emptyTitle: 'There are no products to show right now',
      fat: 'fat',
      food: 'Food',
      hours: 'Opening hours',
      ingredients: 'Ingredients',
      nutrition: 'Nutrition facts',
      openNow: 'Open now',
      protein: 'protein',
      salt: 'salt',
      searchPlaceholder: 'Search menu...',
      specialOffer: 'Special offer',
      sugar: 'sugar',
      tapDetails: 'Tap for details',
      unavailable: 'Currently unavailable',
      viewSchedule: 'View schedule',
      website: 'Website',
    };
    const bosnian: Record<string, string> = {
      all: 'Sve',
      allergens: 'Alergeni',
      callUs: 'Pozovi nas',
      carbs: 'ugljikohidrati',
      closed: 'Zatvoreno',
      dailyOffer: 'Dnevna ponuda',
      detailsFallback: 'Detalji proizvoda su dostupni kod osoblja.',
      drinks: 'Pica',
      emptyBody: 'Promijeni pretragu ili odaberi drugu kategoriju.',
      emptyTitle: 'Trenutno nema proizvoda za prikaz',
      fat: 'masti',
      food: 'Hrana',
      hours: 'Radno vrijeme',
      ingredients: 'Sastojci',
      nutrition: 'Nutritivne vrijednosti',
      openNow: 'Otvoreno sada',
      protein: 'proteini',
      salt: 'so',
      searchPlaceholder: 'Pretrazi meni...',
      specialOffer: 'Specijalna ponuda',
      sugar: 'seceri',
      tapDetails: 'Dodirni za detalje',
      unavailable: 'Trenutno nedostupno',
      viewSchedule: 'Pogledaj raspored',
      website: 'Web stranica',
    };
    return (this.menuLanguage === 'en' ? english : bosnian)[key] ?? key;
  }

  publicProductsLabel(count: number): string {
    return this.menuLanguage === 'en' ? (count === 1 ? 'item' : 'items') : 'proizvoda';
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
    return ({ Draft: 'Priprema', Active: 'Aktivan', Suspended: 'Pauziran', Cancelled: 'Otkazan', Archived: 'Arhiviran' })[status];
  }

  subscriptionStatusLabel(status: SubscriptionStatus): string {
    return this.subscriptionStatuses.find((item) => item.value === status)?.label ?? status;
  }

  paymentMethodLabel(method: PaymentMethod): string {
    return this.paymentMethods.find((item) => item.value === method)?.label ?? method;
  }

  supportTypeLabel(type: SupportTicketType): string {
    return this.supportTypeOptions.find((item) => item.value === type)?.label ?? type;
  }

  supportPriorityLabel(priority: SupportTicketPriority): string {
    return this.supportPriorityOptions.find((item) => item.value === priority)?.label ?? priority;
  }

  supportStatusLabel(status: SupportTicketStatus): string {
    return this.supportStatusOptions.find((item) => item.value === status)?.label ?? status;
  }

  restaurantOnboardingItems(): { label: string; ready: boolean; hint: string }[] {
    const languages = this.parseEnabledLanguages(this.restaurantForm.enabledLanguages);
    const subscriptionReady = this.restaurantForm.subscriptionStatus === 'Active' || this.restaurantForm.subscriptionStatus === 'Trial' || this.restaurantForm.subscriptionStatus === 'Overdue';
    return [
      { label: 'Paket', ready: !!this.restaurantForm.plan, hint: this.restaurantForm.plan || 'Odaberi paket' },
      { label: 'Tema', ready: !!this.restaurantForm.themeKey, hint: this.themeLabel(this.restaurantForm.themeKey) },
      { label: 'Jezici', ready: languages.includes('bs') && languages.includes('en'), hint: languages.map((item) => item.toUpperCase()).join(' + ') },
      { label: 'Owner login', ready: !!this.restaurantForm.ownerEmail && (!!this.restaurantForm.id || this.restaurantForm.ownerPassword.length >= 5), hint: 'Email i pristup' },
      { label: 'Licenca', ready: !!this.restaurantForm.expiresOn && subscriptionReady, hint: this.restaurantForm.expiresOn || 'Datum isteka' },
      { label: 'Logo', ready: !!this.restaurantForm.logoUrl, hint: this.restaurantForm.logoUrl ? 'Dodano' : 'Nedostaje' },
      { label: 'Cover', ready: !!this.restaurantForm.coverImageUrl, hint: this.restaurantForm.coverImageUrl ? 'Dodano' : 'Nedostaje' },
      { label: 'QR kod', ready: !!this.restaurantForm.id, hint: this.restaurantForm.id ? 'Generisan' : 'Nakon kreiranja' },
    ];
  }

  readinessForRestaurant(id: string): AdminRestaurantReadiness | null {
    return this.adminReadiness[id] ?? null;
  }

  missingReadinessForRestaurant(id: string): AdminReadinessIssue[] {
    return (this.readinessForRestaurant(id)?.items ?? []).filter((item) => !item.ready && item.key !== 'menu');
  }

  isRestaurantReady(id: string): boolean {
    return this.readinessForRestaurant(id)?.isMenuReady ?? false;
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
    const plan = this.normalizePlan(this.restaurantForm.plan);
    this.restaurantForm.plan = plan;
    this.restaurantForm.monthlyPrice = subscriptionPlanPrices[plan];
  }

  get restaurantPlanFeatures(): string[] {
    return subscriptionPlanFeatures[this.normalizePlan(this.restaurantForm.plan)];
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
    this.markRestaurantQrDownloaded(item.id);
  }

  printRestaurantQr(item: AdminRestaurantSummary, size: 'A5' | 'A6', style = this.qrPrintStyle): void {
    const qr = this.adminQrCodes[item.id];
    if (!qr) return;
    this.printQrDocument({ qr, size, name: item.name, logo: item.logoUrl || '/menispot-mark.png', url: this.publicMenuUrl(item), style });
    this.markRestaurantQrDownloaded(item.id);
  }

  private printQrDocument(input: { qr: string; size: 'A5' | 'A6'; name: string; logo: string; url: string; style?: 'light' | 'dark' }): void {
    const win = window.open('', '_blank', 'width=720,height=900');
    if (!win) return;
    const dark = input.style === 'dark';
    const bodyBg = dark ? '#0f1115' : '#f8fafc';
    const cardBg = dark ? '#1f242c' : '#ffffff';
    const border = dark ? '#3a414d' : '#dbe3ef';
    const ink = dark ? '#f8fafc' : '#111827';
    const muted = dark ? '#c7d1e0' : '#475569';
    const shadow = dark ? '0 24px 70px rgba(0,0,0,.35)' : '0 24px 70px rgba(15, 23, 42, .12)';
    win.document.write(`<!doctype html><html><head><title>${input.name} QR</title><style>
      @page { size: ${input.size}; margin: 14mm; }
      * { box-sizing: border-box; }
      body { margin: 0; font-family: Inter, Arial, sans-serif; color: ${ink}; background: ${bodyBg}; }
      .card { min-height: 100vh; display: grid; place-items: center; padding: 18mm; }
      .inner { width: 100%; border: 1px solid ${border}; border-radius: 28px; background: ${cardBg}; padding: 28px; text-align: center; box-shadow: ${shadow}; }
      .logo { width: 78px; height: 78px; object-fit: cover; border-radius: 22px; margin-bottom: 18px; background: white; padding: 4px; }
      h1 { margin: 0 0 10px; font-size: 30px; line-height: 1.05; }
      p { margin: 0; color: ${muted}; font-size: 15px; }
      .qr { width: min(70vw, 280px); margin: 26px auto; display: block; border-radius: 18px; background: white; padding: 12px; }
      .cta { display: inline-block; border-radius: 999px; background: #84cc16; color: #111827; padding: 12px 18px; font-weight: 800; letter-spacing: .02em; }
      small { display: block; margin-top: 14px; color: ${muted}; word-break: break-all; }
      @media print { body { background: white; } .card { padding: 0; } .inner { box-shadow: none; } }
    </style></head><body><main class="card"><section class="inner"><img class="logo" src="${input.logo}" alt=""><h1>${input.name}</h1><p>Digitalni meni je spreman za pregled.</p><img class="qr" src="${input.qr}" alt="QR"><span class="cta">Skeniraj meni</span><small>${input.url}</small></section></main><script>window.addEventListener('load',()=>setTimeout(()=>window.print(),250));</script></body></html>`);
    win.document.close();
  }

  private markRestaurantQrDownloaded(id: string): void {
    this.adminRestaurantsService.markQrDownloaded(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const current = this.adminReadiness[id];
          if (!current) return;
          this.adminReadiness = {
            ...this.adminReadiness,
            [id]: {
              ...current,
              qrDownloadedAt: new Date().toISOString(),
              items: current.items.map((item) => item.key === 'qr' ? { ...item, ready: true } : item),
            },
          };
        },
      });
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
    if (segments[0] !== 'menu') this.setPublicProductModalOpen(false);
    if (segments[0] === 'auth' && segments[1] === 'login') {
      this.view = 'auth-login';
    } else if (segments[0] === 'admin') {
      this.view = 'super-admin';
      this.adminTab = this.isAdminTab(segments[1]) ? segments[1] : 'dashboard';
      this.loadAdminRestaurants();
      if (this.adminTab === 'restaurants' && segments[2] === 'new') {
        this.prepareCreateRestaurant();
      } else if (this.adminTab === 'restaurants' && segments[2] && segments[3] === 'edit') {
        this.prepareEditRestaurant(segments[2]);
      } else {
        this.restaurantEditorMode = 'none';
        this.showRestaurantModal = false;
      }
      if (this.adminTab === 'billing') this.loadBilling();
      if (this.adminTab === 'archived-restaurants') this.loadArchivedRestaurants();
      if (this.adminTab === 'drink-library') this.loadAdminDrinks();
      if (this.adminTab === 'support') this.loadAdminSupport();
      if (this.adminTab === 'audit-logs') this.loadAuditLogs();
    } else if (segments[0] === 'restaurant') {
      this.view = 'restaurant-owner';
      this.ownerTab = this.isOwnerTab(segments[2]) ? segments[2] : 'dashboard';
      this.loadOwnerRestaurant(true);
      if (this.ownerTab === 'support') this.loadOwnerSupport();
    } else if (segments[0] === 'menu') {
      this.view = 'public-menu';
      this.loadPublicMenu(segments[1] ?? '');
    } else {
      this.view = 'login';
    }

    this.mobileNav = false;
    this.search = '';
    this.selectedCategory = 'all';
    this.updateBrowserChromeColor();
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
      currency: 'BAM', defaultLanguage: 'bs', enabledLanguages: 'bs,en', themeKey: 'classic-light', plan: 'Start', monthlyPrice: 29, subscriptionStatus: 'Trial',
      startsOn: this.dateInputValue(today), expiresOn: this.dateInputValue(expires), gracePeriodEndsOn: '',
    };
  }

  private formFromDetails(item: AdminRestaurantDetails): RestaurantForm {
    return {
      id: item.id, name: item.name, slug: item.slug, type: item.type, status: item.status,
      ownerEmail: item.ownerEmail ?? '', ownerPassword: '', trialDays: 30,
      description: item.description ?? '', logoUrl: item.logoUrl ?? '', coverImageUrl: item.coverImageUrl ?? '',
      address: item.address ?? '', phone: item.phone ?? '', email: item.email ?? '', websiteUrl: item.websiteUrl ?? '',
      instagramUrl: item.instagramUrl ?? '', currency: item.currency, defaultLanguage: item.defaultLanguage, enabledLanguages: item.enabledLanguages ?? 'bs,en', themeKey: item.themeKey,
      plan: this.normalizePlan(item.subscription.plan), monthlyPrice: item.subscription.monthlyPrice, subscriptionStatus: item.subscription.status, startsOn: item.subscription.startsOn,
      expiresOn: item.subscription.expiresOn, gracePeriodEndsOn: item.subscription.gracePeriodEndsOn ?? '',
    };
  }

  private dateInputValue(date: Date): string {
    return date.toISOString().slice(0, 10);
  }

  private nullIfEmpty(value: string): string | null {
    return value.trim() || null;
  }

  private normalizePlan(value: string): SubscriptionPlan {
    if (value === 'Pro' || value === 'Standard') return 'Pro';
    if (value === 'Premium' || value === 'Enterprise') return 'Premium';
    return 'Start';
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

  private emptySupportForm(): SupportForm {
    return { title: '', type: 'MenuChange', priority: 'Normal', message: '', attachmentUrl: '' };
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
    this.publicTrackedProducts = new Set<string>();
    this.publicSelectedProduct = null;
    this.setPublicProductModalOpen(false);
    this.ownerService.getPublicMenu(slug).pipe(finalize(() => this.ownerLoading = false), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ restaurant }) => this.applyOwnerRestaurant(restaurant),
      error: () => { this.ownerRestaurant = null; this.ownerViewRestaurant = null; this.ownerError = 'Ovaj meni trenutno nije dostupan.'; },
    });
  }

  private setPublicProductModalOpen(open: boolean): void {
    globalThis.document?.body.classList.toggle('public-product-modal-open', open);
  }

  private applyOwnerRestaurant(restaurant: OwnerRestaurant, preservePublicMenuPosition = false): void {
    const previousPublicSection = this.publicMenuSection;
    const previousSelectedCategory = this.selectedCategory;
    this.ownerRestaurant = { ...restaurant, businessHours: this.completeBusinessHours(restaurant.businessHours) };
    if (!this.parseEnabledLanguages(restaurant.enabledLanguages).includes(this.menuLanguage)) {
      this.menuLanguage = 'bs';
    }
    const products: Product[] = restaurant.categories.flatMap((category) => category.items.map((item) => ({
      id: item.id, categoryId: item.categoryId, name: this.localizedValue(item.name, item.nameEn, item.nameDe), description: this.localizedValue(item.description ?? '', item.descriptionEn, item.descriptionDe), price: item.price,
      servingSize: item.servingSize,
      image: item.imageUrl || '/menispot-mark.png', available: item.isAvailable && item.isVisible,
      badges: [item.isVegetarian ? 'vegetarian' : null, item.isSpicy ? 'spicy' : null, item.isFeatured ? 'chefs-choice' : null].filter(Boolean) as BadgeType[],
      allergens: (item.allergens ?? '').split(',').map((value) => value.trim()).filter(Boolean),
      ingredients: item.ingredients,
      calories: item.calories,
      protein: item.protein,
      carbs: item.carbs,
      fat: item.fat,
      sugar: item.sugar,
      salt: item.salt,
    })));
    const fallbackCover = '/menispot-mark.png';
    this.ownerViewRestaurant = {
      id: restaurant.id, slug: restaurant.slug, name: restaurant.name, address: restaurant.address ?? '', phone: restaurant.phone ?? '',
      website: restaurant.websiteUrl ?? '', instagram: restaurant.instagramUrl ?? '', cover: restaurant.coverImageUrl || fallbackCover,
      logo: restaurant.logoUrl || '/menispot-mark.png', status: restaurant.status === 'Active' ? 'active' : 'paused', subscription: 'basic',
      theme: restaurant.theme.themeKey as ThemeType, themeColor: restaurant.theme.accentColor,
      categories: restaurant.categories.map((category) => ({ id: category.id, name: this.localizedValue(category.name, category.nameEn, category.nameDe), icon: category.type === 'Drink' ? '🥤' : '🍽', order: category.sortOrder, active: category.isVisible, type: category.type })),
      businessHours: this.completeBusinessHours(restaurant.businessHours).map((hour) => ({ day: this.dayLabel(hour.dayOfWeek), open: hour.opensAt?.slice(0, 5) ?? '', close: hour.closesAt?.slice(0, 5) ?? '', closed: hour.isClosed })),
      dailyMenu: restaurant.offers.filter((offer) => offer.kind === 'DailyMenu' && offer.isVisible).map((offer) => ({ id: offer.id, name: this.localizedValue(offer.title, offer.titleEn, offer.titleDe), items: this.localizedValue(offer.items ?? '', offer.itemsEn, offer.itemsDe).split('\n').map((item) => item.trim()).filter(Boolean), price: offer.price ?? 0, date: this.offerDateLabel(offer), active: offer.isVisible })),
      offers: restaurant.offers.filter((offer) => offer.kind === 'Promotion' && offer.isVisible).map((offer) => ({ id: offer.id, name: this.localizedValue(offer.title, offer.titleEn, offer.titleDe), description: this.localizedValue(offer.description ?? '', offer.descriptionEn, offer.descriptionDe), originalPrice: offer.originalPrice ?? offer.price ?? 0, offerPrice: offer.price ?? 0, image: offer.imageUrl || fallbackCover, validUntil: offer.endsAt ? new Intl.DateTimeFormat('bs-BA').format(new Date(offer.endsAt)) : 'Trajna ponuda', active: offer.isVisible })),
    };
    this.productMap[restaurant.id] = products;
    const drinkCategoryIds = new Set(restaurant.categories.filter((category) => category.type === 'Drink').map((category) => category.id));
    const foodCategoryIds = new Set(restaurant.categories.filter((category) => category.type === 'Food').map((category) => category.id));
    const hasDrinkProducts = products.some((product) => drinkCategoryIds.has(product.categoryId) && product.available);
    const hasFoodProducts = products.some((product) => foodCategoryIds.has(product.categoryId) && product.available);
    const defaultSection = hasDrinkProducts && (!hasFoodProducts || restaurant.type !== 'Restaurant' && restaurant.type !== 'FastFood') ? 'drink' : 'food';
    const canKeepSection = previousPublicSection === 'drink' ? hasDrinkProducts : hasFoodProducts;
    this.publicMenuSection = preservePublicMenuPosition && canKeepSection ? previousPublicSection : defaultSection;

    const activeCategoryIds = new Set(
      restaurant.categories
        .filter((category) =>
          category.isVisible &&
          category.type === (this.publicMenuSection === 'drink' ? 'Drink' : 'Food') &&
          products.some((product) => product.categoryId === category.id && product.available))
        .map((category) => category.id),
    );
    this.selectedCategory = preservePublicMenuPosition && (previousSelectedCategory === 'all' || activeCategoryIds.has(previousSelectedCategory))
      ? previousSelectedCategory
      : 'all';
    this.updateBrowserChromeColor();
    void this.qrCodeService.createDataUrl(`${globalThis.location.origin}/menu/${restaurant.slug}`).then((value) => this.ownerQrCode = value);
  }

  private trackPublicProductView(itemId: string): void {
    if (this.publicTrackedProducts.has(itemId) || !this.ownerRestaurant?.slug) return;
    this.publicTrackedProducts.add(itemId);
    this.ownerService.trackPublicMenuItem(this.ownerRestaurant.slug, itemId, this.getPublicTrackingSessionId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: () => undefined });
  }

  private getPublicTrackingSessionId(): string {
    if (this.publicTrackingSessionId) return this.publicTrackingSessionId;
    const key = 'menispot-public-session-id';
    try {
      const existing = globalThis.sessionStorage?.getItem(key);
      if (existing) return this.publicTrackingSessionId = existing;
      const generated = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
      globalThis.sessionStorage?.setItem(key, generated);
      return this.publicTrackingSessionId = generated;
    } catch {
      return this.publicTrackingSessionId = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    }
  }

  private updateBrowserChromeColor(): void {
    const color = this.browserChromeColor();
    const documentRef = globalThis.document;
    documentRef.querySelector('meta[name="theme-color"]')?.setAttribute('content', color);
    documentRef.body.style.backgroundColor = color;
    documentRef.documentElement.style.backgroundColor = color;
    documentRef.documentElement.style.colorScheme = this.isDarkChromeColor(color) ? 'dark' : 'light';
  }

  private browserChromeColor(): string {
    if (this.view === 'public-menu') return this.publicThemeSurfaceColor(this.restaurant.theme);
    if (this.view === 'super-admin') return '#1f2228';
    if (this.view === 'restaurant-owner') return this.ownerDarkMode ? '#1f2228' : '#f7f8fa';
    if (this.view === 'auth-login') return '#0f172a';
    return '#f7f8fa';
  }

  private publicThemeSurfaceColor(theme: ThemeType): string {
    const colors: Record<ThemeType, string> = {
      'classic-light': '#fdf8f3',
      'premium-gold': '#0d0b08',
      'burgundy-dining': '#18181b',
      'mediterranean-blue': '#eff6ff',
      'olive-linen': '#f7f5ed',
      'ocean-slate': '#0f172a',
      'coffee-cream': '#faf7f2',
      'urban-espresso': '#1c1917',
      'soft-pastel': '#fff7fb',
      'natural-green': '#f3f7f3',
      'rose-latte': '#fff1f2',
      'cocoa-mint': '#211b17',
      'neon-night': '#09090b',
      'royal-violet': '#17112a',
      'warm-orange': '#fff7ed',
      'street-red': '#fff1f2',
      'yellow-pop': '#fefce8',
      'burger-black': '#111827',
      'lime-street': '#f7fee7',
      'modern-dark': '#09090b',
    };
    return colors[theme] ?? '#f7f8fa';
  }

  private isDarkChromeColor(color: string): boolean {
    const hex = color.replace('#', '');
    const r = Number.parseInt(hex.slice(0, 2), 16);
    const g = Number.parseInt(hex.slice(2, 4), 16);
    const b = Number.parseInt(hex.slice(4, 6), 16);
    return (r * 299 + g * 587 + b * 114) / 1000 < 128;
  }

  private localizedValue(base: string, english?: string | null, german?: string | null): string {
    if (this.menuLanguage === 'en' && english?.trim()) return english.trim();
    if (this.menuLanguage === 'de' && german?.trim()) return german.trim();
    return base;
  }

  private emptyCategoryForm(): CategoryForm { return { id: null, name: '', description: '', nameEn: '', descriptionEn: '', nameDe: '', descriptionDe: '', type: 'Food', sortOrder: (this.ownerCategories.at(-1)?.sortOrder ?? 0) + 1, isVisible: true }; }
  private emptyProductForm(): ProductForm {
    return {
      id: null,
      categoryId: this.ownerCategories[0]?.id ?? '',
      globalDrinkId: null,
      name: '',
      description: '',
      nameEn: '',
      descriptionEn: '',
      nameDe: '',
      descriptionDe: '',
      price: 0,
      servingSize: '',
      imageUrl: '',
      allergens: '',
      ingredients: '',
      calories: null,
      protein: null,
      carbs: null,
      fat: null,
      sugar: null,
      salt: null,
      sortOrder: this.ownerItems.length + 1,
      isVisible: true,
      isAvailable: true,
      isVegetarian: false,
      isSpicy: false,
      isFeatured: false,
    };
  }
  private emptyOfferForm(kind: SpecialOfferKind): OfferForm { return { id: null, kind, title: '', description: '', titleEn: '', descriptionEn: '', itemsEn: '', titleDe: '', descriptionDe: '', itemsDe: '', price: 0, originalPrice: 0, imageUrl: '', startsAt: '', endsAt: '', isVisible: true, items: '' }; }
  private dateTimeInputValue(value: string | null): string { return value ? new Date(value).toISOString().slice(0, 16) : ''; }
  private isoDateTime(value: string): string | null { return value ? new Date(value).toISOString() : null; }
  private offerRequestFromOffer(offer: OwnerSpecialOffer, isVisible = offer.isVisible): SpecialOfferRequest {
    return {
      title: offer.title,
      description: offer.description,
      titleEn: offer.titleEn,
      descriptionEn: offer.descriptionEn,
      itemsEn: offer.itemsEn,
      titleDe: offer.titleDe,
      descriptionDe: offer.descriptionDe,
      itemsDe: offer.itemsDe,
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
