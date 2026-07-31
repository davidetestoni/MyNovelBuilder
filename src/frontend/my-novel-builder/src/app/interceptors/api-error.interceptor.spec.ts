import {
  HttpErrorResponse,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ToastrService } from 'ngx-toastr';
import { of, throwError } from 'rxjs';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('apiErrorInterceptor', () => {
  let toastr: jasmine.SpyObj<ToastrService>;
  const request = new HttpRequest('GET', '/api/test');

  beforeEach(() => {
    toastr = jasmine.createSpyObj<ToastrService>('ToastrService', ['error']);
    TestBed.configureTestingModule({
      providers: [{ provide: ToastrService, useValue: toastr }],
    });
  });

  const interceptError = (error: unknown): unknown => {
    let receivedError: unknown;

    TestBed.runInInjectionContext(() =>
      apiErrorInterceptor(request, () => throwError(() => error)),
    ).subscribe({
      error: (received) => {
        receivedError = received;
      },
    });

    return receivedError;
  };

  it('passes successful responses through without showing a notification', () => {
    const response = new HttpResponse({ status: 200, body: { ok: true } });
    let received: HttpResponse<unknown> | undefined;

    TestBed.runInInjectionContext(() =>
      apiErrorInterceptor(request, () => of(response)),
    ).subscribe((event) => {
      if (event instanceof HttpResponse) {
        received = event;
      }
    });

    expect(received).toBe(response);
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('shows a connection message for network failures', () => {
    const error = new HttpErrorResponse({ status: 0 });

    expect(interceptError(error)).toBe(error);
    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Unable to reach the server. Please check your connection and try again.',
    );
  });

  it('shows and trims an API-provided error message', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { code: 'invalid', message: '  Invalid request.  ' },
    });

    expect(interceptError(error)).toBe(error);
    expect(toastr.error).toHaveBeenCalledOnceWith('Invalid request.');
  });

  it('falls back to the status and normalized status text', () => {
    const error = new HttpErrorResponse({
      status: 503,
      statusText: '  Service Unavailable  ',
      error: { code: 'unavailable' },
    });

    interceptError(error);

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Request failed with status 503 (Service Unavailable).',
    );
  });

  it('omits blank status text from the fallback message', () => {
    const error = new HttpErrorResponse({
      status: 500,
      statusText: '   ',
      error: { message: '   ' },
    });

    interceptError(error);

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Request failed with status 500.',
    );
  });

  it('ignores malformed API error fields', () => {
    const error = new HttpErrorResponse({
      status: 422,
      statusText: 'Unprocessable Content',
      error: { message: 42 },
    });

    interceptError(error);

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Request failed with status 422 (Unprocessable Content).',
    );
  });

  it('handles error payloads that are not objects', () => {
    const error = new HttpErrorResponse({
      status: 404,
      statusText: 'Not Found',
      error: 'missing',
    });

    interceptError(error);

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Request failed with status 404 (Not Found).',
    );
  });

  it('reports server errors through the last supported HTTP status', () => {
    const error = new HttpErrorResponse({
      status: 599,
      statusText: 'Network Connect Timeout',
    });

    interceptError(error);

    expect(toastr.error).toHaveBeenCalledOnceWith(
      'Request failed with status 599 (Network Connect Timeout).',
    );
  });

  it('does not notify for non-error HTTP statuses', () => {
    const redirection = new HttpErrorResponse({ status: 399 });
    const unsupported = new HttpErrorResponse({ status: 600 });

    expect(interceptError(redirection)).toBe(redirection);
    expect(interceptError(unsupported)).toBe(unsupported);
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('rethrows non-HTTP errors unchanged without notifying', () => {
    const error = new Error('Unexpected failure');

    expect(interceptError(error)).toBe(error);
    expect(toastr.error).not.toHaveBeenCalled();
  });
});
