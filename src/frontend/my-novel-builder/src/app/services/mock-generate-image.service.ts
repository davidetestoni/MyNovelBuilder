import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ImageGenRequestDto } from '../types/dtos/generate/image-gen-request.dto';
import { ImageGenerationModelInfoDto } from '../types/dtos/generate/image-generation-model-info.dto';
import {
  mockedImageGenerationResponse,
  mockedImageModelInfos,
} from './mocks/mock-generate-image.data';
import { mockObservable } from './mocks/mock-utils';
import { GenerateImageService } from './generate-image.service';

@Injectable()
export class MockGenerateImageService extends GenerateImageService {
  generateImage(_request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    return mockedImageGenerationResponse();
  }

  editImage(_image: File, _request: ImageGenRequestDto): Observable<HttpEvent<Blob>> {
    return mockedImageGenerationResponse();
  }

  getAvailableModels(): Observable<ImageGenerationModelInfoDto[]> {
    return mockObservable<ImageGenerationModelInfoDto[]>(mockedImageModelInfos);
  }
}
