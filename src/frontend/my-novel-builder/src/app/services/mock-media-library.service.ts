import { Injectable } from '@angular/core';
import { from, Observable } from 'rxjs';
import { MediaFileDto } from '../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../types/dtos/media-library/media-folder.dto';
import { MediaLibraryService } from './media-library.service';
import { mockObservable } from './mocks/mock-utils';

@Injectable()
export class MockMediaLibraryService extends MediaLibraryService {
  private folders: MediaFolderDto[] = [
    {
      id: crypto.randomUUID(),
      createdAt: new Date('2026-03-01T10:00:00Z').toISOString(),
      updatedAt: new Date('2026-03-01T10:00:00Z').toISOString(),
      name: 'Trailers',
      path: '/mock/media/trailers',
    },
    {
      id: crypto.randomUUID(),
      createdAt: new Date('2026-03-02T10:00:00Z').toISOString(),
      updatedAt: new Date('2026-03-02T10:00:00Z').toISOString(),
      name: 'Reference Stills',
      path: '/mock/media/reference-stills',
    },
  ];

  private mediaByFolder: Record<string, MediaFileDto[]> = {
    [this.folders[0].id]: [
      {
        fileName: 'launch-trailer.mp4',
        lastModifiedAt: new Date('2026-03-05T10:00:00Z').toISOString(),
        sizeBytes: 18_320_112,
      },
    ],
    [this.folders[1].id]: [
      {
        fileName: 'hero-shot.png',
        lastModifiedAt: new Date('2026-03-06T08:30:00Z').toISOString(),
        sizeBytes: 2_408_221,
      },
      {
        fileName: 'city-panorama.jpg',
        lastModifiedAt: new Date('2026-03-04T12:10:00Z').toISOString(),
        sizeBytes: 4_102_881,
      },
    ],
  };

  getFolders(): Observable<MediaFolderDto[]> {
    return mockObservable([...this.folders]);
  }

  createFolder(name: string, path: string): Observable<MediaFolderDto> {
    const now = new Date().toISOString();
    const folder: MediaFolderDto = {
      id: crypto.randomUUID(),
      createdAt: now,
      updatedAt: now,
      name,
      path,
    };

    this.folders = [...this.folders, folder];
    this.mediaByFolder[folder.id] = [];
    return mockObservable(folder);
  }

  deleteFolder(id: string): Observable<void> {
    this.folders = this.folders.filter((folder) => folder.id !== id);
    delete this.mediaByFolder[id];
    return mockObservable<void>(undefined);
  }

  getMedia(folderId: string): Observable<MediaFileDto[]> {
    return mockObservable([...(this.mediaByFolder[folderId] ?? [])]);
  }

  uploadMedia(folderId: string, name: string, file: File): Observable<MediaFileDto> {
    const media: MediaFileDto = {
      fileName: name,
      lastModifiedAt: new Date().toISOString(),
      sizeBytes: file.size,
    };

    this.mediaByFolder[folderId] = [media, ...(this.mediaByFolder[folderId] ?? [])];
    return mockObservable(media);
  }

  deleteMedia(folderId: string, fileName: string): Observable<void> {
    this.mediaByFolder[folderId] = (this.mediaByFolder[folderId] ?? []).filter(
      (file) => file.fileName !== fileName,
    );
    return mockObservable<void>(undefined);
  }

  getMediaBlob(_folderId: string, fileName: string): Observable<Blob> {
    const seed = encodeURIComponent(`${fileName}-${crypto.randomUUID()}`);
    return from(
      fetch(`https://picsum.photos/seed/${seed}/200/300`).then(async (response) => {
        if (!response.ok) {
          throw new Error(`Could not fetch mock media preview for ${fileName}.`);
        }

        return response.blob();
      }),
    );
  }
}
