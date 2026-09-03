import { Injectable, OnDestroy, inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Subscription } from 'rxjs';
import { GenerateAudioService } from '../../services/generate-audio.service';
import { IntegrationsService } from '../../services/integrations.service';
import { LocalStorageService } from '../../services/local-storage.service';
import { ImmersiveTtsRequestDto } from '../../types/dtos/generate/immersive-tts-request.dto';
import { PromptDto } from '../../types/dtos/prompt/prompt.dto';
import { LocalStorageKey } from '../../types/enums/local-storage-key';
import { PromptType } from '../../types/enums/prompt-type';
import {
  STREAMING_WAV_PLAYER_FACTORY,
  StreamingWavPlayerFactory,
} from '../../utils/streaming-wav-player.factory';

export interface ProseTtsRequest {
  novelId: string;
  prompts: PromptDto[];
  chapterIndex: number;
  sectionIndex: number;
  narratorText: string;
}

@Injectable()
export class ProseTtsService implements OnDestroy {
  private readonly generateAudioService = inject(GenerateAudioService);
  private readonly integrationsService = inject(IntegrationsService);
  private readonly localStorageService = inject(LocalStorageService);
  private readonly toastr = inject(ToastrService);
  private readonly createStreamingWavPlayer: StreamingWavPlayerFactory = inject(
    STREAMING_WAV_PLAYER_FACTORY,
  );

  private immersiveEnabled = false;
  private loadingToastId: number | undefined;
  private readonly integrationsSubscription: Subscription;

  constructor() {
    this.integrationsSubscription = this.integrationsService
      .getIntegrationsConfig()
      .subscribe({
        next: (config) => {
          this.immersiveEnabled = config.ttsEnableImmersive;
        },
        error: (error) => {
          console.error('Error loading integrations config for TTS:', error);
          this.immersiveEnabled = false;
        },
      });
  }

  ngOnDestroy(): void {
    this.integrationsSubscription.unsubscribe();
    this.clearLoadingToast();
  }

  async playSection(request: ProseTtsRequest): Promise<void> {
    const timerLabel = `TTS section ${request.chapterIndex}-${request.sectionIndex}`;
    const firstAudioTimerLabel = `TTS first audio ${request.chapterIndex}-${request.sectionIndex}`;
    let firstAudioTimerEnded = false;

    try {
      this.showLoadingToast();
      console.time(timerLabel);
      console.time(firstAudioTimerLabel);
      const immersivePromptId = this.immersiveEnabled
        ? this.getDefaultImmersivePromptId(request.prompts)
        : null;

      if (immersivePromptId) {
        try {
          const response =
            await this.generateAudioService.immersiveTextToSpeechStreamResponse({
              novelId: request.novelId,
              promptId: immersivePromptId,
              chapterIndex: request.chapterIndex,
              sectionIndex: request.sectionIndex,
            } satisfies ImmersiveTtsRequestDto);

          await this.playWavStreamResponse(response, firstAudioTimerLabel, () => {
            firstAudioTimerEnded = true;
            this.clearLoadingToast();
          });
          return;
        } catch (error) {
          console.error('Immersive TTS streaming error:', error);
          this.toastr.warning(
            `Immersive TTS failed${this.formatErrorSuffix(error)}. Falling back to narrator-only playback.`,
          );
        }
      } else if (this.immersiveEnabled) {
        this.toastr.info(
          'No immersive TTS prompt is configured. Falling back to narrator-only playback.',
        );
      }

      const response =
        await this.generateAudioService.textToSpeechStreamResponse({
          message: request.narratorText,
        });
      await this.playWavStreamResponse(response, firstAudioTimerLabel, () => {
        firstAudioTimerEnded = true;
        this.clearLoadingToast();
      });
    } catch (error) {
      console.error('WAV streaming error:', error);
    } finally {
      if (!firstAudioTimerEnded) {
        this.clearLoadingToast();
        console.timeEnd(firstAudioTimerLabel);
      }
      console.timeEnd(timerLabel);
    }
  }

  private async playWavStreamResponse(
    response: Response,
    firstAudioTimerLabel?: string,
    onFirstAudio?: () => void,
  ): Promise<void> {
    const stream = response.body;
    if (!stream) {
      this.toastr.error('No audio stream was returned.');
      return;
    }

    let firstAudioReported = false;
    const player = this.createStreamingWavPlayer(() => {
      if (!firstAudioReported && firstAudioTimerLabel) {
        firstAudioReported = true;
        onFirstAudio?.();
        console.timeEnd(firstAudioTimerLabel);
      }
    });
    const reader = stream.getReader();
    let totalBytes = 0;

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      if (value) {
        totalBytes += value.byteLength;
        player.addChunk(value);
      }
    }

    if (totalBytes <= 44) {
      throw new Error(
        'No playable audio bytes were returned from the TTS stream.',
      );
    }
  }

  private getDefaultImmersivePromptId(prompts: PromptDto[]): string | null {
    const immersivePrompts = prompts.filter(
      (prompt) => prompt.type === PromptType.PrepareImmersiveTts,
    );
    const storedPromptId = this.localStorageService.getNestedStringForKey(
      LocalStorageKey.RecentPrompts,
      PromptType.PrepareImmersiveTts,
    );

    if (
      storedPromptId &&
      immersivePrompts.some((prompt) => prompt.id === storedPromptId)
    ) {
      return storedPromptId;
    }

    if (storedPromptId) {
      this.localStorageService.removeNestedKey(
        LocalStorageKey.RecentPrompts,
        PromptType.PrepareImmersiveTts,
      );
    }

    const fallbackPromptId = immersivePrompts[0]?.id;

    if (fallbackPromptId) {
      this.localStorageService.setNestedStringForKey(
        LocalStorageKey.RecentPrompts,
        PromptType.PrepareImmersiveTts,
        fallbackPromptId,
      );
    }

    return fallbackPromptId ?? null;
  }

  private formatErrorSuffix(error: unknown): string {
    if (!(error instanceof Error) || !error.message) {
      return '';
    }

    return ` (${error.message})`;
  }

  private showLoadingToast(): void {
    this.clearLoadingToast();

    const toast = this.toastr.info('Generating TTS...', '', {
      toastClass: 'ngx-toastr tts-loading-toast',
      positionClass: 'toast-bottom-right',
      closeButton: false,
      tapToDismiss: false,
      progressBar: false,
      timeOut: 0,
      extendedTimeOut: 0,
    });

    this.loadingToastId = toast.toastId;
  }

  private clearLoadingToast(): void {
    if (this.loadingToastId === undefined) {
      return;
    }

    this.toastr.clear(this.loadingToastId);
    this.loadingToastId = undefined;
  }
}
