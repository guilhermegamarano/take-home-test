import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';

import { LoanListFilters, LoanType } from './loan';

@Component({
  selector: 'app-loan-filters',
  standalone: true,
  imports: [FormsModule, MatButtonModule],
  template: `
    <form class="filters-panel" aria-label="Filter loans" (ngSubmit)="apply.emit()">
      <label>
        Applicant
        <input name="filterApplicant" type="search" [(ngModel)]="filters.applicantName" />
      </label>

      <label>
        Status
        <select name="filterStatus" [(ngModel)]="filters.status">
          <option value="">All</option>
          <option value="active">Active</option>
          <option value="paid">Paid</option>
        </select>
      </label>

      <label>
        Product
        <select name="filterType" [(ngModel)]="filters.type">
          <option value="">All</option>
          @for (product of products; track product.value) {
            <option [ngValue]="product.value">{{ product.label }}</option>
          }
        </select>
      </label>

      <label>
        Min. balance
        <input
          name="filterMinimumBalance"
          type="number"
          min="0"
          step="0.01"
          [(ngModel)]="filters.minimumBalance"
        />
      </label>

      <label class="checkbox-label">
        <input
          name="filterHighExposure"
          type="checkbox"
          [(ngModel)]="filters.highExposureOnly"
        />
        High exposure only
      </label>

      <div class="filter-actions">
        <button mat-flat-button type="submit">Apply</button>
        <button mat-stroked-button type="button" (click)="reset.emit()">Reset</button>
      </div>
    </form>
  `,
})
export class LoanFiltersComponent {
  @Input({ required: true }) filters!: LoanListFilters;
  @Input({ required: true }) products!: readonly { value: LoanType; label: string }[];
  @Output() readonly apply = new EventEmitter<void>();
  @Output() readonly reset = new EventEmitter<void>();
}
