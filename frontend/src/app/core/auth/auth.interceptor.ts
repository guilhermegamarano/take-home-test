import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const isUnsafeMethod = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method);
  const securedRequest = request.clone({
    setHeaders: isUnsafeMethod ? { 'X-Requested-With': 'XMLHttpRequest' } : {},
    withCredentials: true,
  });

  return next(securedRequest);
};
