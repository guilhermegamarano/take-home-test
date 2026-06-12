export interface Loan {
  id: string;
  amount: number;
  currentBalance: number;
  type: 'personal' | 'small-business' | 'bridge';
  applicantName: string;
  status: 'active' | 'paid';
  createdAtUtc: string;
}

export type LoanType = Loan['type'];

export interface CreateLoanRequest {
  amount: number;
  applicantName: string;
  type: LoanType;
}

export interface MakePaymentRequest {
  amount: number;
}

export interface LoanListFilters {
  page: number;
  pageSize: number;
  status: '' | 'active' | 'paid';
  type: '' | LoanType;
  applicantName: string;
  minimumBalance: number | null;
  highExposureOnly: boolean;
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
