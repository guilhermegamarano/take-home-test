import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { Loan } from './loan';

@Component({
  selector: 'app-loan-details-panel',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatProgressSpinnerModule],
  template: `
    @if (loading) {
      <aside class="details-panel" role="status">
        <mat-spinner diameter="28"></mat-spinner>
        <span>Loading loan details...</span>
      </aside>
    } @else if (loan) {
      <aside class="details-panel" aria-label="Loan details">
        <div class="details-header">
          <div>
            <p class="eyebrow">Loan detail</p>
            <h2>{{ loan.applicantName }}</h2>
          </div>
          <button mat-stroked-button type="button" (click)="close.emit()">Close</button>
        </div>

        <dl>
          <div>
            <dt>Loan ID</dt>
            <dd>{{ loan.id }}</dd>
          </div>
          <div>
            <dt>Product</dt>
            <dd>{{ productLabel(loan.type) }}</dd>
          </div>
          <div>
            <dt>Original amount</dt>
            <dd>{{ loan.amount | currency }}</dd>
          </div>
          <div>
            <dt>Current balance</dt>
            <dd>{{ loan.currentBalance | currency }}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd>{{ loan.status }}</dd>
          </div>
          <div>
            <dt>Created</dt>
            <dd>{{ loan.createdAtUtc | date:'medium' }}</dd>
          </div>
        </dl>
      </aside>
    }
  `,
})
export class LoanDetailsPanelComponent {
  @Input() loan: Loan | null = null;
  @Input() loading = false;
  @Output() readonly close = new EventEmitter<void>();

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
}
