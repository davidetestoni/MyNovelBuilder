import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { MediaFileDto } from '../types/dtos/media-library/media-file.dto';
import { MediaFolderDto } from '../types/dtos/media-library/media-folder.dto';

@Injectable()
export abstract class MediaLibraryService {
  abstract getFolders(): Observable<MediaFolderDto[]>;
  abstract createFolder(name: string, path: string): Observable<MediaFolderDto>;
  abstract deleteFolder(id: string): Observable<void>;
  abstract getMedia(folderId: string): Observable<MediaFileDto[]>;
  abstract uploadMedia(
    folderId: string,
    name: string,
    file: File,
  ): Observable<MediaFileDto>;
  abstract deleteMedia(folderId: string, fileName: string): Observable<void>;
  abstract getMediaBlob(folderId: string, fileName: string): Observable<Blob>;
}
