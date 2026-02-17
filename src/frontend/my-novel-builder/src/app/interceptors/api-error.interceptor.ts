import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';

type ApiErrorDto = {
  code?: string;
  message?: string;
};

const parseApiError = (payload: unknown): ApiErrorDto | null => {
  if (!payload) {
    return null;
  }

  if (typeof payload === 'string') {
    const trimmed = payload.trim();
    if (trimmed.length === 0) {
      return null;
    }

    try {
      const parsed = JSON.parse(trimmed);
      if (parsed && typeof parsed === 'object') {
        return parsed as ApiErrorDto;
      }
    } catch {
      return { message: trimmed };
    }
  }

  if (typeof payload === 'object') {
    return payload as ApiErrorDto;
  }

  return null;
};

const extractErrorMessage = (error: HttpErrorResponse): string | null => {
  const apiError = parseApiError(error.error);
  if (!apiError) {
    return null;
  }

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
