import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { MediaFileDto } from '../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../types/dtos/media-library/media-folder.dto';
import { MediaLibraryService } from './media-library.service';

@Injectable()
export class ApiMediaLibraryService extends MediaLibraryService {
  private http = inject(HttpClient);
  private baseUrl = environment.api.baseUrl;

  getFolders(): Observable<MediaFolderDto[]> {
    return this.http.get<MediaFolderDto[]>(`${this.baseUrl}/media-library/folders`);
  }

  createFolder(name: string, path: string): Observable<MediaFolderDto> {
    return this.http.post<MediaFolderDto>(`${this.baseUrl}/media-library/folder`, {
      name,
      path,
    });
  }

  deleteFolder(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/media-library/folder/${id}`);
  }

  getMedia(folderId: string): Observable<MediaFileDto[]> {
    return this.http.get<MediaFileDto[]>(
      `${this.baseUrl}/media-library/folder/${folderId}/media`,
    );
  }

  uploadMedia(folderId: string, name: string, file: File): Observable<MediaFileDto> {
    const formData = new FormData();
    formData.append('name', name);
    formData.append('file', file);

    return this.http.post<MediaFileDto>(
      `${this.baseUrl}/media-library/folder/${folderId}/media`,
      formData,
    );
  }

  deleteMedia(folderId: string, fileName: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/media-library/folder/${folderId}/media/${encodeURIComponent(fileName)}`,
    );
  }

  getMediaBlob(folderId: string, fileName: string): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/media-library/folder/${folderId}/media/${encodeURIComponent(fileName)}`,
      { responseType: 'blob' },
    );
  }
}
