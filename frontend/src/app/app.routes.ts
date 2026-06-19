import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', title: 'MeniSpot', children: [] },
  { path: 'admin', title: 'MeniSpot Admin', children: [] },
  { path: 'admin/:tab', title: 'MeniSpot Admin', children: [] },
  { path: 'restaurant/:restaurantId', title: 'MeniSpot Restaurant', children: [] },
  { path: 'restaurant/:restaurantId/:tab', title: 'MeniSpot Restaurant', children: [] },
  { path: 'menu/:restaurantId', title: 'MeniSpot Menu', children: [] },
  { path: '**', redirectTo: '' },
];
