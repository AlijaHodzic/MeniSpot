import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../api.config';
import { AuthSession } from '../auth/auth.models';
import { SupportTicket, UpdateSupportTicketRequest } from '../owner/owner.models';
import {
  AdminRestaurantDetails,
  AdminRestaurantReadiness,
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

  getArchived(): Observable<AdminRestaurantSummary[]> {
    return this.http.get<AdminRestaurantSummary[]>(`${this.endpoint}/archived`);
  }

  getDashboard(): Observable<AdminDashboardSummary> {
    return this.http.get<AdminDashboardSummary>(`${this.endpoint}/dashboard`);
  }

  getReadiness(): Observable<AdminRestaurantReadiness[]> {
    return this.http.get<AdminRestaurantReadiness[]>(`${this.endpoint}/readiness`);
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

  markQrDownloaded(id: string): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/${id}/qr-downloaded`, {});
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.endpoint}/${id}`);
  }

  restore(id: string): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/${id}/restore`, {});
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

  deleteSupportTicket(id: string): Observable<void> {
    return this.http.delete<void>(`${API_URL}/admin/support/${id}`);
  }
}
