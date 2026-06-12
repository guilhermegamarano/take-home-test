import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { CreateLoanRequest, Loan, LoanListFilters, MakePaymentRequest, PagedResult } from './loan';

@Injectable({ providedIn: 'root' })
export class LoansApiService {
  constructor(private readonly http: HttpClient) {}

  list(filters: LoanListFilters): Observable<PagedResult<Loan>> {
    return this.http.get<PagedResult<Loan>>('/api/loans', {
      params: {
        page: filters.page,
        pageSize: filters.pageSize,
        ...(filters.status === '' ? {} : { status: filters.status }),
        ...(filters.type === '' ? {} : { type: filters.type }),
        ...(filters.applicantName.trim() === '' ? {} : { applicantName: filters.applicantName.trim() }),
        ...(filters.minimumBalance === null ? {} : { minimumBalance: filters.minimumBalance }),
        ...(filters.highExposureOnly ? { highExposureOnly: true } : {}),
      },
    });
  }

  getById(id: string): Observable<Loan> {
    return this.http.get<Loan>(`/api/loans/${id}`);
  }

  create(request: CreateLoanRequest): Observable<Loan> {
    return this.http.post<Loan>('/api/loans', request);
  }

  makePayment(id: string, request: MakePaymentRequest): Observable<Loan> {
    return this.http.post<Loan>(`/api/loans/${id}/payment`, request);
  }
}
