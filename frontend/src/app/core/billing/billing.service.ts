import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../api.config';
import { BillingOverview, PaymentHistoryItem, RecordManualPaymentRequest } from './billing.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${API_URL}/admin/billing`;

  getOverview(): Observable<BillingOverview> {
    return this.http.get<BillingOverview>(this.endpoint);
  }

  getHistory(restaurantId: string): Observable<PaymentHistoryItem[]> {
    return this.http.get<PaymentHistoryItem[]>(`${this.endpoint}/${restaurantId}/payments`);
  }

  recordPayment(restaurantId: string, request: RecordManualPaymentRequest): Observable<PaymentHistoryItem> {
    return this.http.post<PaymentHistoryItem>(`${this.endpoint}/${restaurantId}/payments`, request);
  }
}
