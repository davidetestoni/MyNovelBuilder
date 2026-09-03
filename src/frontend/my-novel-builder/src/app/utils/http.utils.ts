import { HttpResponse } from '@angular/common/http';

export function getFileNameFromResponse(
  response: HttpResponse<any>,
  fallbackFileName: string,
): string {
  const contentDisposition = response.headers.get('Content-Disposition');
  if (!contentDisposition) {
    return fallbackFileName;
  }

  const fileNameRegex = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/;
  const matches = fileNameRegex.exec(contentDisposition);
  if (matches != null && matches[1]) {
    return matches[1].replace(/['"]/g, '');
  }

  return fallbackFileName;
}
