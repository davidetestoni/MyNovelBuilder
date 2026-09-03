import { TtsProvider } from '../../enums/tts-provider';

export interface ImmersiveTtsRequestDto {
  novelId: string;
  promptId: string;
  chapterIndex: number;
  sectionIndex: number;
  provider?: TtsProvider;
  ttsModelId?: string;
  voiceId?: string;
  textGenerationModelId?: string;
}

export interface ImmersiveTtsDebugResponseDto {
  provider: TtsProvider;
  ttsModelId: string;
  textGenerationModelId: string;
  pauseMs: number;
  chunks: ImmersiveTtsDebugChunkDto[];
}

export interface ImmersiveTtsDebugChunkDto {
  sequence: number;
  speakerKind: string;
  speakerName: string;
  characterRecordId: string | null;
  voiceId: string;
  isNarratorFallback: boolean;
  text: string;
}
