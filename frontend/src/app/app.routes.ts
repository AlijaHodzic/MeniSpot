import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', title: 'MeniSpot' },
  { path: 'admin', title: 'MeniSpot Admin' },
  { path: 'admin/:tab', title: 'MeniSpot Admin' },
  { path: 'restaurant/:restaurantId', title: 'MeniSpot Restaurant' },
  { path: 'restaurant/:restaurantId/:tab', title: 'MeniSpot Restaurant' },
  { path: 'menu/:restaurantId', title: 'MeniSpot Menu' },
  { path: '**', redirectTo: '' },
];
