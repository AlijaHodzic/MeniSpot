import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../api.config';
import { AuthSession } from '../auth/auth.models';
import { SupportTicket, UpdateSupportTicketRequest } from '../owner/owner.models';
import {
  AdminRestaurantDetails,
  AdminRestaurantSummary,
  AdminDashboardSummary,
  CreateRestaurantRequest,
  RestaurantStatus,
  SetSubscriptionRequest,
  UpdateRestaurantRequest,
  UpdateOwnerAccessRequest,
} from './admin-restaurants.models';

@Injectable({ providedIn: 'root' })
export class AdminRestaurantsService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${API_URL}/admin/restaurants`;

  getAll(): Observable<AdminRestaurantSummary[]> {
    return this.http.get<AdminRestaurantSummary[]>(this.endpoint);
  }

  getDashboard(): Observable<AdminDashboardSummary> {
    return this.http.get<AdminDashboardSummary>(`${this.endpoint}/dashboard`);
  }

  get(id: string): Observable<AdminRestaurantDetails> {
    return this.http.get<AdminRestaurantDetails>(`${this.endpoint}/${id}`);
  }

  create(request: CreateRestaurantRequest): Observable<AdminRestaurantDetails> {
    return this.http.post<AdminRestaurantDetails>(this.endpoint, request);
  }

  update(id: string, request: UpdateRestaurantRequest): Observable<void> {
    return this.http.put<void>(`${this.endpoint}/${id}`, request);
  }

  setStatus(id: string, status: RestaurantStatus): Observable<void> {
    return this.http.put<void>(`${this.endpoint}/${id}/status`, JSON.stringify(status), {
      headers: { 'Content-Type': 'application/json' },
    });
  }

  setSubscription(id: string, request: SetSubscriptionRequest): Observable<void> {
    return this.http.put<void>(`${this.endpoint}/${id}/subscription`, request);
  }

  updateOwnerAccess(id: string, request: UpdateOwnerAccessRequest): Observable<void> {
    return this.http.put<void>(`${this.endpoint}/${id}/owner-access`, request);
  }

  uploadImage(id: string, file: File): Observable<{ url: string }> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ url: string }>(`${this.endpoint}/${id}/images`, form);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.endpoint}/${id}`);
  }

  impersonate(id: string): Observable<AuthSession> {
    return this.http.post<AuthSession>(`${this.endpoint}/${id}/impersonate`, {});
  }

  getSupportTickets(): Observable<SupportTicket[]> {
    return this.http.get<SupportTicket[]>(`${API_URL}/admin/support`);
  }

  updateSupportTicket(id: string, request: UpdateSupportTicketRequest): Observable<SupportTicket> {
    return this.http.put<SupportTicket>(`${API_URL}/admin/support/${id}`, request);
  }
}
