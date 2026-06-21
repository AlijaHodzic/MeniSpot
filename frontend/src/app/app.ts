import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { concatMap, filter, finalize, forkJoin, Observable } from 'rxjs';
import {
  LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat, LucideClock, LucideEdit2,
  LucideEye, LucideEyeOff, LucideFlame, LucideGlobe, LucideLeaf, LucideLock, LucideLogIn, LucideLogOut, LucideMail,
  LucideMapPin, LucideMenu, LucidePalette, LucidePercent, LucidePhone, LucidePlus,
  LucidePower, LucideQrCode, LucideSearch, LucideShield, LucideSparkles,
  LucideStar, LucideStore, LucideTrash2, LucideTrendingUp, LucideUtensilsCrossed, LucideX,
} from '@lucide/angular';
import { products as initialProducts, restaurants, themeOptions } from './demo-data';
import { AdminTab, AppView, BadgeType, OwnerTab, Product, Restaurant, ThemeType } from './models';
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

@Component({
  selector: 'app-root',
  imports: [
    CommonModule, FormsModule, LucideArrowLeft, LucideAward, LucideCalendar, LucideChefHat,
    LucideClock, LucideEdit2, LucideEye, LucideFlame, LucideGlobe,
    LucideLeaf, LucideLock, LucideLogIn, LucideLogOut, LucideMail, LucideMapPin, LucideMenu, LucidePalette, LucidePercent,
    LucidePhone, LucidePlus, LucidePower, LucideQrCode, LucideSearch,
    LucideShield, LucideSparkles, LucideStar, LucideStore, LucideTrash2, LucideTrendingUp,
    LucideUtensilsCrossed, LucideX, LucideEyeOff,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly adminRestaurantsService = inject(AdminRestaurantsService);
  private readonly qrCodeService = inject(QrCodeService);
  private readonly billingService = inject(BillingService);
  readonly auth = inject(AuthService);
  readonly restaurants = restaurants;
  readonly themes = themeOptions;
  readonly ownerChartTicks = [240, 180, 120, 60, 0];
  readonly adminTabs: { id: AdminTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'restaurants', label: 'Restorani' },
    { id: 'billing', label: 'Pretplate' },
    { id: 'themes', label: 'Teme' }, { id: 'qr-codes', label: 'QR kodovi' },
  ];
  readonly ownerTabs: { id: OwnerTab; label: string }[] = [
    { id: 'dashboard', label: 'Pregled' }, { id: 'categories', label: 'Kategorije' },
    { id: 'products', label: 'Proizvodi' }, { id: 'daily-menu', label: 'Dnevni meni' },
    { id: 'offers', label: 'Ponude' }, { id: 'settings', label: 'Postavke' }, { id: 'qr', label: 'QR kod' },
  ];
  readonly establishmentTypes: { value: EstablishmentType; label: string }[] = [
    { value: 'Restaurant', label: 'Restoran' }, { value: 'Cafe', label: 'Kafić' },
    { value: 'Bar', label: 'Bar' }, { value: 'Club', label: 'Klub' },
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
  selectedRestaurantId = restaurants[0].id;
  selectedCategory = 'all';
  search = '';
  mobileNav = false;
  showHours = false;
  showProductModal = false;
  showPassword = false;
  loginEmail = '';
  loginPassword = '';
  loginLoading = false;
  loginError = '';
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
  restaurantStatusUpdating = new Set<string>();
  adminQrCodes: Record<string, string> = {};
  billingOverview: BillingOverview | null = null;
  billingLoading = false;
  billingLoaded = false;
  billingError = '';
  billingSearch = '';
  billingStatusFilter = 'all';
  showPaymentModal = false;
  selectedBillingAccount: BillingAccountSummary | null = null;
  paymentHistory: PaymentHistoryItem[] = [];
  paymentHistoryLoading = false;
  paymentSaving = false;
  paymentError = '';
  paymentForm = this.emptyPaymentForm();
  restaurantForm = this.emptyRestaurantForm();
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

  get filteredProducts(): Product[] {
    const term = this.search.trim().toLocaleLowerCase();
    return this.restaurantProducts.filter((product) =>
      (this.selectedCategory === 'all' || product.categoryId === this.selectedCategory) &&
      (!term || product.name.toLocaleLowerCase().includes(term) || product.description.toLocaleLowerCase().includes(term)),
    );
  }

  get ownerWeeklyViews(): { day: string; views: number }[] {
    const data: Record<string, number[]> = {
      'old-town': [118, 146, 132, 174, 168, 221, 198],
      'pizzeria-roma': [142, 171, 158, 196, 187, 234, 218],
      'caffe-central': [74, 96, 89, 112, 108, 146, 131],
    };
    return (data[this.restaurant.id] ?? data['old-town']).map((views, index) => ({
      day: ['Pon', 'Uto', 'Sri', 'Čet', 'Pet', 'Sub', 'Ned'][index],
      views,
    }));
  }

  enter(view: AppView, restaurantId?: string): void {
    const id = restaurantId ?? this.selectedRestaurantId;
    const commands: string[] = view === 'auth-login'
      ? ['/auth/login']
      : view === 'super-admin'
      ? ['/admin', 'dashboard']
      : view === 'restaurant-owner'
        ? ['/restaurant', id, 'dashboard']
        : view === 'public-menu'
          ? ['/menu', id]
          : ['/'];
    void this.router.navigate(commands);
  }

  openLogin(): void {
    this.loginError = '';
    void this.router.navigate(['/auth/login']);
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
    this.auth.logout();
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
    this.showRestaurantModal = true;
  }

  openEditRestaurant(item: AdminRestaurantSummary): void {
    this.restaurantForm = { ...this.emptyRestaurantForm(), id: item.id, name: item.name, slug: item.slug, type: item.type };
    this.showRestaurantModal = true;
    this.restaurantModalLoading = true;
    this.restaurantFormError = '';
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
    const request: Observable<unknown> = form.id
      ? forkJoin([
          this.adminRestaurantsService.update(form.id, {
            name: form.name.trim(), description: this.nullIfEmpty(form.description),
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
        this.showRestaurantModal = false;
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

  selectAdminTab(tab: AdminTab): void { void this.router.navigate(['/admin', tab]); }
  selectOwnerTab(tab: OwnerTab): void { void this.router.navigate(['/restaurant', this.restaurant.id, tab]); }

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

  establishmentLabel(type: EstablishmentType): string {
    return this.establishmentTypes.find((item) => item.value === type)?.label ?? type;
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

  private async generateAdminQrCodes(items: AdminRestaurantSummary[]): Promise<void> {
    const entries = await Promise.all(items.map(async (item) => [item.id, await this.qrCodeService.createDataUrl(this.publicMenuUrl(item))] as const));
    this.adminQrCodes = Object.fromEntries(entries);
  }
}
