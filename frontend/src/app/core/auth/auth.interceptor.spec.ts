import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { authInterceptor } from './auth.interceptor';
import { HttpClient } from '@angular/common/http';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('marks unsafe requests as application-originated and includes credentials', () => {
    http.post('/api/loans', { amount: 1500 }).subscribe();

    const request = httpController.expectOne('/api/loans');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    request.flush({});
  });

  it('includes credentials without adding the write header to safe requests', () => {
    http.get('/api/loans').subscribe();

    const request = httpController.expectOne('/api/loans');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.headers.has('X-Requested-With')).toBeFalse();
    request.flush([]);
  });
});
