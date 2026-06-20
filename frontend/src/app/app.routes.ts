import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', title: 'MeniSpot', children: [] },
  { path: 'auth/login', title: 'Sign in | MeniSpot', canActivate: [guestGuard], children: [] },
  { path: 'admin', title: 'MeniSpot Admin', canActivate: [authGuard], data: { roles: ['SuperAdmin'] }, children: [] },
  { path: 'admin/:tab', title: 'MeniSpot Admin', canActivate: [authGuard], data: { roles: ['SuperAdmin'] }, children: [] },
  { path: 'restaurant/:restaurantId', title: 'MeniSpot Restaurant', canActivate: [authGuard], data: { roles: ['RestaurantOwner', 'RestaurantStaff'] }, children: [] },
  { path: 'restaurant/:restaurantId/:tab', title: 'MeniSpot Restaurant', canActivate: [authGuard], data: { roles: ['RestaurantOwner', 'RestaurantStaff'] }, children: [] },
  { path: 'menu/:restaurantId', title: 'MeniSpot Menu', children: [] },
  { path: '**', redirectTo: '' },
];
