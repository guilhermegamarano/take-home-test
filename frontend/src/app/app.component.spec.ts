import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';

import { AppComponent } from './app.component';
import { AuthService } from './core/auth/auth.service';
import { LoansApiService } from './loans/loans-api.service';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let loansApiService: jasmine.SpyObj<LoansApiService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'refreshSession',
      'clearLocalSession',
      'canWriteLoans',
      'login',
      'logout',
    ]);
    loansApiService = jasmine.createSpyObj<LoansApiService>('LoansApiService', [
      'list',
      'getById',
      'create',
      'makePayment',
    ]);
    authService.refreshSession.and.returnValue(throwError(() => ({ status: 401 })));
    authService.login.and.returnValue(of({ username: 'reviewer', expiresIn: 1800, permissions: ['loans.read', 'loans.write'] }));
    authService.logout.and.returnValue(of(undefined));
    authService.canWriteLoans.and.returnValue(true);
    loansApiService.list.and.returnValue(of(paged([])));
    loansApiService.getById.and.returnValue(of(createLoan()));
    loansApiService.create.and.returnValue(of({
      id: '33333333-3333-3333-3333-333333333333',
      amount: 1500,
      currentBalance: 1500,
      type: 'personal',
      applicantName: 'New Applicant',
      status: 'active',
      createdAtUtc: '2026-06-11T12:00:00Z',
    }));
    loansApiService.makePayment.and.returnValue(of({
      id: '11111111-1111-1111-1111-111111111111',
      amount: 1500,
      currentBalance: 400,
      type: 'personal',
      applicantName: 'Maria Silva',
      status: 'active',
      createdAtUtc: '2026-06-11T12:00:00Z',
    }));

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: LoansApiService, useValue: loansApiService },
      ],
    }).compileComponents();
  });

  it('renders the sign-in form when the user is not authenticated', () => {
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sign in');
    expect(fixture.debugElement.query(By.css('#username'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('#password'))).not.toBeNull();
  });

  it('restores an existing session and loads loans', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);

    fixture.detectChanges();

    expect(authService.refreshSession).toHaveBeenCalled();
    expect(loansApiService.list).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Maria Silva');
  });

  it('shows a validation message when credentials are missing', () => {
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(authService.login).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Enter your username and password.');
  });

  it('signs in and loads loans', async () => {
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setCredentials('reviewer', 'password');
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(authService.login).toHaveBeenCalledWith('reviewer', 'password');
    expect(loansApiService.list).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Maria Silva');
    expect(fixture.nativeElement.textContent).toContain('Personal');
    expect(fixture.nativeElement.textContent).toContain('Outstanding balance');
  });

  it('shows an authentication error when sign-in fails', () => {
    authService.login.and.returnValue(throwError(() => ({ status: 401 })));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setCredentials('reviewer', 'wrong-password');

    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Authentication failed. Check your credentials.',
    );
  });

  it('shows a retry message when an authenticated loan request fails', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(throwError(() => ({ status: 500 })));
    fixture = TestBed.createComponent(AppComponent);

    fixture.detectChanges();

    expect(authService.logout).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Loans could not be loaded. Try again.');
  });

  it('signs out and clears the visible loan list', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    fixture.debugElement.query(By.css('button')).triggerEventHandler('click');
    fixture.detectChanges();

    expect(authService.logout).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Sign in');
    expect(fixture.nativeElement.textContent).not.toContain('Maria Silva');
  });

  it('logs out and shows a session-expired message when loading loans returns unauthorized', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(throwError(() => ({ status: 401 })));
    fixture = TestBed.createComponent(AppComponent);

    fixture.detectChanges();

    expect(authService.clearLocalSession).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Your session expired. Sign in again.');
  });

  it('creates a loan from the operational form', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setNewLoan('New Applicant', 1500, 'personal');

    fixture.debugElement.query(By.css('.command-panel')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(loansApiService.create).toHaveBeenCalledWith({
      amount: 1500,
      applicantName: 'New Applicant',
      type: 'personal',
    });
    expect(fixture.nativeElement.textContent).toContain('Loan created successfully.');
  });

  it('does not create a loan when the form is invalid', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setNewLoan('', 0, 'personal');

    fixture.debugElement.query(By.css('.command-panel')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(loansApiService.create).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Enter a valid applicant name and loan amount.');
  });

  it('shows API validation details when creating a loan fails', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    loansApiService.create.and.returnValue(throwError(() => ({
      status: 400,
      error: { detail: 'Amount for personal loans must be between 500.00 and 25000.00.' },
    })));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setNewLoan('New Applicant', 10, 'personal');

    fixture.debugElement.query(By.css('.command-panel')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Amount for personal loans must be between 500.00 and 25000.00.',
    );
  });

  it('applies a payment from the table row', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setPayment('11111111-1111-1111-1111-111111111111', 100);

    fixture.debugElement.query(By.css('.payment-form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(loansApiService.makePayment).toHaveBeenCalledWith(
      '11111111-1111-1111-1111-111111111111',
      { amount: 100 },
    );
    expect(fixture.nativeElement.textContent).toContain('Payment applied successfully.');
  });

  it('does not apply a payment when the amount is invalid', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setPayment('11111111-1111-1111-1111-111111111111', 0);

    fixture.debugElement.query(By.css('.payment-form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(loansApiService.makePayment).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Enter a valid payment amount.');
  });

  it('clears the session when a command returns unauthorized', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    loansApiService.makePayment.and.returnValue(throwError(() => ({ status: 401 })));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    setPayment('11111111-1111-1111-1111-111111111111', 100);

    fixture.debugElement.query(By.css('.payment-form')).triggerEventHandler('ngSubmit');
    fixture.detectChanges();

    expect(authService.clearLocalSession).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Your session expired. Sign in again.');
  });

  it('hides write actions for read-only sessions', () => {
    authService.refreshSession.and.returnValue(of(true));
    authService.canWriteLoans.and.returnValue(false);
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Create loan');
    expect(fixture.nativeElement.textContent).toContain('Read only');
  });

  it('loads loan details from the API when requested from a row', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()])));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    fixture.debugElement.queryAll(By.css('button'))
      .find((button) => button.nativeElement.textContent.includes('View'))!
      .triggerEventHandler('click');
    fixture.detectChanges();

    expect(loansApiService.getById).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111');
    expect(fixture.nativeElement.textContent).toContain('Loan detail');
  });

  it('loads the next page when pagination advances', () => {
    authService.refreshSession.and.returnValue(of(true));
    loansApiService.list.and.returnValue(of(paged([createLoan()], { page: 1, totalItems: 10, totalPages: 2 })));
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    fixture.debugElement.queryAll(By.css('button'))
      .find((button) => button.nativeElement.textContent.includes('Next'))!
      .triggerEventHandler('click');
    fixture.detectChanges();

    expect(loansApiService.list.calls.mostRecent().args[0].page).toBe(2);
  });

  function setCredentials(username: string, password: string): void {
    const component = fixture.componentInstance as unknown as {
      username: string;
      password: string;
    };
    component.username = username;
    component.password = password;
  }

  function setNewLoan(applicantName: string, amount: number, type: 'personal'): void {
    const component = fixture.componentInstance as unknown as {
      newLoan: { applicantName: string; amount: number; type: 'personal' };
    };
    component.newLoan = { applicantName, amount, type };
  }

  function setPayment(id: string, amount: number): void {
    const component = fixture.componentInstance as unknown as {
      paymentAmounts: Record<string, number>;
    };
    component.paymentAmounts[id] = amount;
  }

  function createLoan() {
    return {
      id: '11111111-1111-1111-1111-111111111111',
      amount: 1500,
      currentBalance: 500,
      type: 'personal' as const,
      applicantName: 'Maria Silva',
      status: 'active' as const,
      createdAtUtc: '2026-06-11T12:00:00Z',
    };
  }

  function paged(
    items: ReturnType<typeof createLoan>[],
    overrides: Partial<{ page: number; pageSize: number; totalItems: number; totalPages: number }> = {},
  ) {
    return {
      items,
      page: overrides.page ?? 1,
      pageSize: overrides.pageSize ?? 5,
      totalItems: overrides.totalItems ?? items.length,
      totalPages: overrides.totalPages ?? (items.length === 0 ? 0 : 1),
    };
  }
});
