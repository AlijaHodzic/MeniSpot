import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../api.config';
import { AdminGlobalDrink, GlobalDrinkRequest } from './admin-drinks.models';

@Injectable({ providedIn: 'root' })
export class AdminDrinksService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${API_URL}/admin/drinks`;

  getAll(): Observable<AdminGlobalDrink[]> {
    return this.http.get<AdminGlobalDrink[]>(this.endpoint);
  }

  create(request: GlobalDrinkRequest): Observable<AdminGlobalDrink> {
    return this.http.post<AdminGlobalDrink>(this.endpoint, request);
  }

  update(id: string, request: GlobalDrinkRequest): Observable<AdminGlobalDrink> {
    return this.http.put<AdminGlobalDrink>(`${this.endpoint}/${id}`, request);
  }

  hide(id: string): Observable<void> {
    return this.http.delete<void>(`${this.endpoint}/${id}`);
  }

  uploadImage(file: File): Observable<{ url: string }> {
    const data = new FormData();
    data.append('file', file);
    return this.http.post<{ url: string }>(`${this.endpoint}/images`, data);
  }
}
