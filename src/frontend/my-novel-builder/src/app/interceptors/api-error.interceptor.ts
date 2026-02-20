import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';
import { ApiErrorDto } from '../types/dtos/errors/api-error.dto';

const isApiErrorDto = (payload: object): payload is ApiErrorDto => {
  const value = payload as { code?: unknown; message?: unknown };

  const hasValidCode =
    value.code === undefined || typeof value.code === 'string';
  const hasValidMessage =
    value.message === undefined || typeof value.message === 'string';

  return hasValidCode && hasValidMessage;
};

const extractErrorMessage = (error: HttpErrorResponse): string | null => {
  if (!error.error || typeof error.error !== 'object') {
    return null;
  }

  if (!isApiErrorDto(error.error)) {
    return null;
  }

  const apiError = error.error;
  const message = apiError.message;
  if (typeof message === 'string' && message.trim().length > 0) {
    return message;
  }

  return null;
};

export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastr = inject(ToastrService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error?.status === 0) {
        toastr.error(
          'Unable to reach the server. Please check your connection and try again.',
        );
      } else if (error?.status >= 400 && error.status < 600) {
        const apiMessage = extractErrorMessage(error);
        const statusText = error.statusText
          ? ` (${error.statusText})`
          : '';
        const message =
          apiMessage ??
          `Request failed with status ${error.status}${statusText}.`;
        toastr.error(message);
      }

      return throwError(() => error);
    })
  );
};
