import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { LoansApiService } from './loans-api.service';

describe('LoansApiService', () => {
  let service: LoansApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(LoansApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('loads loans from the API', () => {
    service.list({
      page: 1,
      pageSize: 5,
      status: 'active',
      type: 'personal' as const,
      applicantName: 'Maria',
      minimumBalance: 100,
      highExposureOnly: false,
    }).subscribe((result) => expect(result.items[0].applicantName).toBe('Maria Silva'));

    const request = httpController.expectOne((item) =>
      item.url === '/api/loans' &&
      item.params.get('page') === '1' &&
      item.params.get('pageSize') === '5' &&
      item.params.get('status') === 'active' &&
      item.params.get('type') === 'personal' &&
      item.params.get('applicantName') === 'Maria' &&
      item.params.get('minimumBalance') === '100');
    expect(request.request.method).toBe('GET');
    request.flush({
      items: [createLoan()],
      page: 1,
      pageSize: 5,
      totalItems: 1,
      totalPages: 1,
    });
  });

  it('creates loans through the API', () => {
    service
      .create({ amount: 1500, applicantName: 'Maria Silva', type: 'personal' })
      .subscribe((loan) => expect(loan.id).toBe('11111111-1111-1111-1111-111111111111'));

    const request = httpController.expectOne('/api/loans');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      amount: 1500,
      applicantName: 'Maria Silva',
      type: 'personal',
    });
    request.flush({
      ...createLoan(),
      currentBalance: 1500,
    });
  });

  it('applies payments through the API', () => {
    service
      .makePayment('11111111-1111-1111-1111-111111111111', { amount: 100 })
      .subscribe((loan) => expect(loan.currentBalance).toBe(1400));

    const request = httpController.expectOne(
      '/api/loans/11111111-1111-1111-1111-111111111111/payment',
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ amount: 100 });
    request.flush({
      ...createLoan(),
      currentBalance: 1400,
    });
  });

  it('loads a loan by id through the API', () => {
    service
      .getById('11111111-1111-1111-1111-111111111111')
      .subscribe((loan) => expect(loan.applicantName).toBe('Maria Silva'));

    const request = httpController.expectOne('/api/loans/11111111-1111-1111-1111-111111111111');
    expect(request.request.method).toBe('GET');
    request.flush(createLoan());
  });

  function createLoan() {
    return {
      id: '11111111-1111-1111-1111-111111111111',
      amount: 1500,
      currentBalance: 500,
      type: 'personal',
      applicantName: 'Maria Silva',
      status: 'active' as const,
      createdAtUtc: '2026-06-11T12:00:00Z',
    };
  }
});
