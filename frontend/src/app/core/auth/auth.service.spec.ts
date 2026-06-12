import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('creates a secure browser session on login', () => {
    service.login('reviewer', 'password').subscribe((response) =>
      expect(response.username).toBe('reviewer'));

    const request = httpController.expectOne('/api/auth/session');
    expect(request.request.method).toBe('POST');
    request.flush({ username: 'reviewer', expiresIn: 1800, permissions: ['loans.read', 'loans.write'] });

    expect(service.hasSession()).toBeTrue();
    expect(service.canWriteLoans()).toBeTrue();
  });

  it('clears the local session flag on logout', () => {
    service.login('reviewer', 'password').subscribe();
    httpController.expectOne('/api/auth/session').flush({
      username: 'reviewer',
      expiresIn: 1800,
      permissions: ['loans.read', 'loans.write'],
    });

    service.logout().subscribe();
    const request = httpController.expectOne('/api/auth/session');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);

    expect(service.hasSession()).toBeFalse();
    expect(service.canWriteLoans()).toBeFalse();
  });

  it('refreshes an existing cookie session', () => {
    service.refreshSession().subscribe((authenticated) => expect(authenticated).toBeTrue());

    const request = httpController.expectOne('/api/auth/session');
    expect(request.request.method).toBe('GET');
    request.flush({ username: 'reviewer', expiresIn: 1800, permissions: ['loans.read'] });

    expect(service.hasSession()).toBeTrue();
    expect(service.canWriteLoans()).toBeFalse();
  });
});
