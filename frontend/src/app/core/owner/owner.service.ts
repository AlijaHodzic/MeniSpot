import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../api.config';
import { UpdateRestaurantRequest } from '../restaurants/admin-restaurants.models';
import { AddLibraryDrinksRequest, CategoryRequest, GlobalDrinkSummary, MenuItemRequest, OwnerBusinessHour, OwnerMenuCategory, OwnerMenuItem, OwnerRestaurant, OwnerSpecialOffer, SpecialOfferRequest } from './owner.models';

@Injectable({ providedIn: 'root' })
export class OwnerService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${API_URL}/restaurant`;

  getRestaurant(): Observable<OwnerRestaurant> { return this.http.get<OwnerRestaurant>(this.endpoint); }
  getPublicMenu(slug: string): Observable<{ restaurant: OwnerRestaurant }> { return this.http.get<{ restaurant: OwnerRestaurant }>(`${API_URL}/public/menus/${slug}`); }
  updateRestaurant(request: UpdateRestaurantRequest): Observable<void> { return this.http.put<void>(this.endpoint, request); }
  saveCategory(id: string | null, request: CategoryRequest): Observable<OwnerMenuCategory> {
    return id ? this.http.put<OwnerMenuCategory>(`${this.endpoint}/categories/${id}`, request) : this.http.post<OwnerMenuCategory>(`${this.endpoint}/categories`, request);
  }
  deleteCategory(id: string): Observable<void> { return this.http.delete<void>(`${this.endpoint}/categories/${id}`); }
  saveItem(id: string | null, request: MenuItemRequest): Observable<OwnerMenuItem> {
    return id ? this.http.put<OwnerMenuItem>(`${this.endpoint}/items/${id}`, request) : this.http.post<OwnerMenuItem>(`${this.endpoint}/items`, request);
  }
  deleteItem(id: string): Observable<void> { return this.http.delete<void>(`${this.endpoint}/items/${id}`); }
  getDrinkLibrary(): Observable<GlobalDrinkSummary[]> { return this.http.get<GlobalDrinkSummary[]>(`${this.endpoint}/drink-library`); }
  addLibraryDrinks(request: AddLibraryDrinksRequest): Observable<OwnerMenuItem[]> { return this.http.post<OwnerMenuItem[]>(`${this.endpoint}/drink-library/items`, request); }
  saveOffer(id: string | null, request: SpecialOfferRequest): Observable<OwnerSpecialOffer> {
    return id ? this.http.put<OwnerSpecialOffer>(`${this.endpoint}/offers/${id}`, request) : this.http.post<OwnerSpecialOffer>(`${this.endpoint}/offers`, request);
  }
  deleteOffer(id: string): Observable<void> { return this.http.delete<void>(`${this.endpoint}/offers/${id}`); }
  setTheme(request: OwnerRestaurant['theme']): Observable<void> { return this.http.put<void>(`${this.endpoint}/theme`, request); }
  setBusinessHours(request: OwnerBusinessHour[]): Observable<void> { return this.http.put<void>(`${this.endpoint}/business-hours`, request); }
  uploadImage(file: File): Observable<{ url: string }> {
    const body = new FormData(); body.append('file', file);
    return this.http.post<{ url: string }>(`${this.endpoint}/images`, body);
  }
}
