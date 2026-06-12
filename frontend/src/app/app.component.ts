import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { finalize } from 'rxjs';

import { AuthService } from './core/auth/auth.service';
import { Loan, LoanListFilters, LoanType, PagedResult } from './loans/loan';
import { LoanDetailsPanelComponent } from './loans/loan-details-panel.component';
import { LoanFiltersComponent } from './loans/loan-filters.component';
import { LoansApiService } from './loans/loans-api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CurrencyPipe,
    FormsModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTableModule,
    LoanDetailsPanelComponent,
    LoanFiltersComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  protected readonly loanProducts: readonly { value: LoanType; label: string }[] = [
    { value: 'personal', label: 'Personal' },
    { value: 'small-business', label: 'Small business' },
    { value: 'bridge', label: 'Bridge' },
  ];
  protected readonly displayedColumns = [
    'amount',
    'currentBalance',
    'type',
    'applicantName',
    'status',
    'payment',
    'details',
  ];
  protected readonly loans = signal<readonly Loan[]>([]);
  protected readonly page = signal<PagedResult<Loan> | null>(null);
  protected readonly selectedLoan = signal<Loan | null>(null);
  protected readonly loadingDetails = signal(false);
  protected readonly filters: LoanListFilters = this.createDefaultFilters();
  protected readonly summary = computed(() => {
    const loans = this.loans();
    const activeLoans = loans.filter((loan) => loan.status === 'active');
    const paidLoans = loans.length - activeLoans.length;
    const outstandingBalance = activeLoans.reduce(
      (total, loan) => total + loan.currentBalance,
      0,
    );
    const highExposureLoans = activeLoans.filter((loan) => loan.currentBalance >= 50_000).length;

    return {
      activeLoans: activeLoans.length,
      paidLoans,
      outstandingBalance,
      highExposureLoans,
    };
  });
  protected readonly loading = signal(false);
  protected readonly savingLoan = signal(false);
  protected readonly savingPaymentId = signal<string | null>(null);
  protected readonly authenticated = signal(false);
  protected readonly canWriteLoans = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected username = '';
  protected password = '';
  protected newLoan = {
    amount: 1500,
    applicantName: '',
    type: 'personal' as LoanType,
  };
  protected paymentAmounts: Record<string, number | null> = {};

  constructor(
    private readonly authService: AuthService,
    private readonly loansApiService: LoansApiService,
  ) {}

  ngOnInit(): void {
    this.loading.set(true);
    this.authService
      .refreshSession()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.authenticated.set(true);
          this.canWriteLoans.set(this.authService.canWriteLoans());
          this.loadLoans();
        },
        error: () => {
          this.authService.clearLocalSession();
          this.authenticated.set(false);
        },
      });
  }

  protected login(): void {
    if (this.username.trim() === '' || this.password === '') {
      this.errorMessage.set('Enter your username and password.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.authService
      .login(this.username.trim(), this.password)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.authenticated.set(true);
          this.canWriteLoans.set(this.authService.canWriteLoans());
          this.password = '';
          this.loadLoans();
        },
        error: () => this.errorMessage.set('Authentication failed. Check your credentials.'),
      });
  }

  protected logout(): void {
    this.authService.logout().subscribe({
      next: () => this.clearSession(),
      error: () => this.clearSession(),
    });
  }

  protected loadLoans(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.loansApiService
      .list(this.filters)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.page.set(result);
          this.loans.set(result.items);
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            this.clearSession();
            this.errorMessage.set('Your session expired. Sign in again.');
            return;
          }

          this.errorMessage.set('Loans could not be loaded. Try again.');
        },
      });
  }

  protected applyFilters(): void {
    this.filters.page = 1;
    this.loadLoans();
  }

  protected resetFilters(): void {
    Object.assign(this.filters, this.createDefaultFilters());
    this.loadLoans();
  }

  protected changePage(direction: -1 | 1): void {
    const page = this.page();
    if (page === null) {
      return;
    }

    const nextPage = page.page + direction;
    if (nextPage < 1 || nextPage > page.totalPages) {
      return;
    }

    this.filters.page = nextPage;
    this.loadLoans();
  }

  protected viewDetails(id: string): void {
    this.loadingDetails.set(true);
    this.errorMessage.set(null);
    this.loansApiService
      .getById(id)
      .pipe(finalize(() => this.loadingDetails.set(false)))
      .subscribe({
        next: (loan) => this.selectedLoan.set(loan),
        error: (error: HttpErrorResponse) => this.handleCommandError(error, 'Loan details could not be loaded.'),
      });
  }

  protected createLoan(): void {
    if (this.newLoan.applicantName.trim() === '' || this.newLoan.amount <= 0) {
      this.errorMessage.set('Enter a valid applicant name and loan amount.');
      return;
    }

    this.savingLoan.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.loansApiService
      .create({
        amount: this.newLoan.amount,
        applicantName: this.newLoan.applicantName.trim(),
        type: this.newLoan.type,
      })
      .pipe(finalize(() => this.savingLoan.set(false)))
      .subscribe({
        next: (loan) => {
          this.loans.update((loans) => [loan, ...loans]);
          this.filters.page = 1;
          this.newLoan = { amount: 1500, applicantName: '', type: 'personal' };
          this.successMessage.set('Loan created successfully.');
          this.loadLoans();
        },
        error: (error: HttpErrorResponse) => this.handleCommandError(error, 'Loan could not be created.'),
      });
  }

  protected makePayment(loan: Loan): void {
    const amount = this.paymentAmounts[loan.id];
    if (amount === null || amount === undefined || amount <= 0) {
      this.errorMessage.set('Enter a valid payment amount.');
      return;
    }

    this.savingPaymentId.set(loan.id);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.loansApiService
      .makePayment(loan.id, { amount })
      .pipe(finalize(() => this.savingPaymentId.set(null)))
      .subscribe({
        next: (updatedLoan) => {
          this.loans.update((loans) =>
            loans.map((item) => item.id === updatedLoan.id ? updatedLoan : item),
          );
          if (this.selectedLoan()?.id === updatedLoan.id) {
            this.selectedLoan.set(updatedLoan);
          }
          this.paymentAmounts[loan.id] = null;
          this.successMessage.set('Payment applied successfully.');
        },
        error: (error: HttpErrorResponse) => this.handleCommandError(error, 'Payment could not be applied.'),
      });
  }

  protected productLabel(type: Loan['type']): string {
    switch (type) {
      case 'small-business':
        return 'Small business';
      case 'bridge':
        return 'Bridge';
      case 'personal':
        return 'Personal';
    }
  }

  protected paidPercentage(loan: Loan): number {
    return loan.amount === 0
      ? 0
      : Math.round(((loan.amount - loan.currentBalance) / loan.amount) * 100);
  }

  private clearSession(): void {
    this.authService.clearLocalSession();
    this.authenticated.set(false);
    this.canWriteLoans.set(false);
    this.loans.set([]);
    this.page.set(null);
    this.selectedLoan.set(null);
    this.paymentAmounts = {};
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  private handleCommandError(error: HttpErrorResponse, fallback: string): void {
    if (error.status === 401) {
      this.clearSession();
      this.errorMessage.set('Your session expired. Sign in again.');
      return;
    }

    const detail = typeof error.error?.detail === 'string' ? error.error.detail : null;
    this.errorMessage.set(detail ?? fallback);
  }

  private createDefaultFilters(): LoanListFilters {
    return {
      page: 1,
      pageSize: 5,
      status: '',
      type: '',
      applicantName: '',
      minimumBalance: null,
      highExposureOnly: false,
    };
  }
}
