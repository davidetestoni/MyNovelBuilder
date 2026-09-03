import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';
import { ImageGenerationModelInfoDto } from '../types/dtos/generate/image-generation-model-info.dto';

@Injectable()
export abstract class GenerateImageService {
  abstract generateImage(request: ImageGenRequestDto): Observable<HttpEvent<Blob>>;
  abstract editImage(image: File, request: ImageGenRequestDto): Observable<HttpEvent<Blob>>;
  abstract getAvailableModels(): Observable<ImageGenerationModelInfoDto[]>;
}
